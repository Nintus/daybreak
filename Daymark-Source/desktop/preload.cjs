const {contextBridge,ipcRenderer}=require('electron');
contextBridge.exposeInMainWorld('daymark',Object.freeze({load:()=>ipcRenderer.invoke('data:load'),save:data=>ipcRenderer.invoke('data:save',data),notify:data=>ipcRenderer.invoke('notify',data),exportFile:data=>ipcRenderer.invoke('file:export',data)}));
