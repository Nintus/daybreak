import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {initialState} from '../src/calendar.mjs';

test('desktop enforces IPC origin and isolated renderer, serializes writes, keeps backup, and rejects corrupt reads',async()=>{
  const temp=await fs.mkdtemp(path.join(os.tmpdir(),'daymark-test-'));
  const handlers=new Map(),windowEvents=new Map(),webEvents=new Map();let options,popup;
  class MockWindow {
    constructor(config){options=config;this.webContents={setWindowOpenHandler:fn=>popup=fn,on:(name,fn)=>webEvents.set(name,fn)};}
    loadFile(){} on(name,fn){windowEvents.set(name,fn);} show(){} focus(){} hide(){}
  }
  const electron={app:{setName(){},setPath(){},getPath:()=>temp,requestSingleInstanceLock:()=>true,setAppUserModelId(){},on(){},whenReady:()=>Promise.resolve(),quit(){}},BrowserWindow:MockWindow,ipcMain:{handle:(name,fn)=>handlers.set(name,fn)},Notification:class{static isSupported(){return false;}},Menu:{setApplicationMenu(){},buildFromTemplate:items=>items},dialog:{showSaveDialog:async()=>({canceled:true})},Tray:class{setToolTip(){}setContextMenu(){}on(){}},nativeImage:{createFromPath:()=>({})}};
  const dirname=fileURLToPath(new URL('../desktop/',import.meta.url));
  const source=await fs.readFile(path.join(dirname,'main.cjs'),'utf8');
  const context=vm.createContext({require:name=>name==='electron'?electron:name==='node:fs/promises'?fs:name==='node:path'?path:name==='node:url'?{pathToFileURL}:null,__dirname:dirname,process:{env:{}}});
  vm.runInContext(source,context);await Promise.resolve();
  assert.equal(options.webPreferences.nodeIntegration,false);assert.equal(options.webPreferences.contextIsolation,true);assert.equal(options.webPreferences.sandbox,true);assert.equal(popup().action,'deny');
  let prevented=false;webEvents.get('will-navigate')({preventDefault:()=>prevented=true},'https://attacker.example');assert.equal(prevented,true);
  const trusted={senderFrame:{url:pathToFileURL(path.join(dirname,'../src/index.html')).href}},untrusted={senderFrame:{url:'https://attacker.example'}};
  assert.throws(()=>handlers.get('data:load')(untrusted),/Unauthorized/);
  const state=initialState();assert.equal(await handlers.get('data:load')(trusted),null);
  await handlers.get('data:save')(trusted,state);
  const changed={...state,tasks:[{id:'task',title:'Saved task',done:false,due:''}]};
  await Promise.all([handlers.get('data:save')(trusted,changed),handlers.get('data:save')(trusted,{...changed,settings:{...changed.settings,theme:'light'}})]);
  assert.equal((await handlers.get('data:load')(trusted)).settings.theme,'light');
  const backup=JSON.parse(await fs.readFile(path.join(temp,'calendar.json.bak'),'utf8'));assert.equal(backup.tasks[0].title,'Saved task');assert.equal(backup.settings.theme,'dark');
  assert.throws(()=>handlers.get('data:save')(trusted,{events:[]}),/Invalid/);
  assert.equal(await handlers.get('file:export')(trusted,{content:'test',kind:'ics'}),false);
  assert.equal(await handlers.get('notify')(trusted,{title:'Test',body:'Reminder'}),false);
  await fs.writeFile(path.join(temp,'calendar.json'),'{bad json');await assert.rejects(()=>handlers.get('data:load')(trusted),/could not be read/);
});
