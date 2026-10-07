const {app,BrowserWindow,ipcMain,Notification,Menu,dialog,Tray,nativeImage}=require('electron');
const fs=require('node:fs/promises');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
let window, tray, quitting=false, writeQueue=Promise.resolve();
app.setName('Daymark');
if(process.env.DAYMARK_DATA_DIR)app.setPath('userData',path.resolve(process.env.DAYMARK_DATA_DIR));
const file=()=>path.join(app.getPath('userData'),'calendar.json');
const entry=pathToFileURL(path.join(__dirname,'../src/index.html')).href;
const trusted=event=>event.senderFrame?.url===entry;
const handle=(name,fn)=>ipcMain.handle(name,(event,...args)=>{if(!trusted(event))throw new Error('Unauthorized request');return fn(...args);});
if(!app.requestSingleInstanceLock())app.quit();
else {
app.setAppUserModelId('org.daymark.calendar');
app.on('second-instance',()=>{window?.show();window?.focus();});
app.on('before-quit',()=>quitting=true);
app.whenReady().then(()=>{
  Menu.setApplicationMenu(null);
  window=new BrowserWindow({width:1440,height:940,minWidth:1000,minHeight:700,show:!process.env.DAYMARK_TEST,title:'Daymark',backgroundColor:'#171a20',webPreferences:{preload:path.join(__dirname,'preload.cjs'),contextIsolation:true,nodeIntegration:false,sandbox:true}});
  window.webContents.setWindowOpenHandler(()=>({action:'deny'}));
  window.webContents.on('will-navigate',(event,url)=>{if(url!==entry)event.preventDefault();});
  window.loadFile(path.join(__dirname,'../src/index.html'));
  // Closing the window keeps reminders alive. Quit is available in the tray.
  window.on('close',event=>{if(!quitting){event.preventDefault();window.hide();}});
  // SVG rasterization support differs by Electron platform; use a PNG icon.
  tray=new Tray(nativeImage.createFromPath(path.join(__dirname,'../src/icon.png')));
  tray.setToolTip('Daymark — your day, clearly');
  tray.setContextMenu(Menu.buildFromTemplate([{label:'Open Daymark',click:()=>window.show()},{type:'separator'},{label:'Quit Daymark',click:()=>app.quit()}]));
  tray.on('double-click',()=>window.show());
});
handle('data:load',async()=>{try{return JSON.parse(await fs.readFile(file(),'utf8'));}catch(e){if(e.code==='ENOENT')return null;throw new Error('Your calendar could not be read. Restore a backup before continuing.');}});
handle('data:save',data=>{
  if(!data||data.version!==1||!Array.isArray(data.events)||!Array.isArray(data.tasks)||!Array.isArray(data.calendars))throw new Error('Invalid calendar data');
  const content=JSON.stringify(data,null,2);if(content.length>20_000_000)throw new Error('Calendar data is too large');
  writeQueue=writeQueue.catch(()=>{}).then(async()=>{await fs.mkdir(path.dirname(file()),{recursive:true});const temp=file()+'.tmp';await fs.writeFile(temp,content,'utf8');await fs.copyFile(file(),file()+'.bak').catch(e=>{if(e.code!=='ENOENT')throw e;});await fs.rename(temp,file());});return writeQueue;
});
handle('notify',({title,body})=>{if(typeof title!=='string'||typeof body!=='string')return false;if(Notification.isSupported()){const n=new Notification({title:title.slice(0,200),body:body.slice(0,500)});n.on('click',()=>window.show());n.show();return true;}return false;});
handle('file:export',async({content,kind})=>{if(typeof content!=='string'||content.length>20_000_000)throw new Error('Invalid export');const json=kind==='json';const result=await dialog.showSaveDialog(window,{defaultPath:json?'daymark-backup.json':'daymark-calendar.ics',filters:[{name:json?'Daymark backup':'iCalendar',extensions:[json?'json':'ics']}]});if(result.canceled)return false;await fs.writeFile(result.filePath,content,'utf8');return true;});
}
