import {planImport} from './import-plan.mjs';
import {dateKey,localInput,addDays,startDay,startWeek,uid,validateEvent,occurrences,parseQuick,importICS,exportICS,initialState,demoEvents,wallTime,instantFromWall,occurrenceFor,resolveEventTime,eventWallTime,editOccurrence,skipOccurrence,updateEvent,validateRule} from './calendar.mjs';
const $=id=>document.getElementById(id);
const esc=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let importPreview=null,editorSource=null;
let state,selected=startDay(new Date()),miniMonth=new Date(selected),view='week',editing=null,smallMode='task',fileMode='ics',toastTimer,ready=false;
const fmt=(d,options)=>new Intl.DateTimeFormat('en',options).format(new Date(d));
const time=d=>fmt(d,{hour:'numeric',minute:'2-digit',hour12:!state.settings.clock24});
const color=id=>state.calendars.find(c=>c.id===id)?.color||'#b4a5f5';
const colorStyle=id=>`--event-color:${/^#[a-f0-9]{6}$/i.test(color(id))?color(id):'#b4a5f5'}`;
const visible=e=>state.calendars.find(c=>c.id===e.calendarId)?.visible!==false;
const visibleEvents=()=>state.events.filter(visible);
function toast(message){$('toast').textContent=message;$('toast').classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').classList.remove('show'),4500);}
async function save(){
  if(!ready)return;
  $('save-status').textContent='Saving…';
  try{if(window.daymark)await window.daymark.save(structuredClone(state));else localStorage.setItem('daymark.v1',JSON.stringify(state));$('save-status').textContent='Saved on this device';}
  catch(e){$('save-status').textContent='Save failed — export a backup';toast(e.message);}
}
function applySettings(){document.body.classList.toggle('light',state.settings.theme==='light');$('zone').textContent=Intl.DateTimeFormat().resolvedOptions().timeZone.replace(/_/g,' ');$('settings-zone').textContent=$('zone').textContent;}
function renderMini(){
  const month=new Date(miniMonth.getFullYear(),miniMonth.getMonth(),1),first=startWeek(month,state.settings.monday),weekdays=state.settings.monday?['M','T','W','T','F','S','S']:['S','M','T','W','T','F','S'];
  $('mini-calendar').innerHTML=`<div class="mini-header"><span>${fmt(month,{month:'long',year:'numeric'})}</span><div><button class="icon-button" data-mini="-1" aria-label="Previous month">‹</button><button class="icon-button" data-mini="1" aria-label="Next month">›</button></div></div><div class="mini-grid">${weekdays.map(d=>`<span class="weekday">${d}</span>`).join('')}${Array.from({length:42},(_,i)=>{const d=addDays(first,i);return `<button class="mini-day ${d.getMonth()!==month.getMonth()?'muted':''} ${dateKey(d)===dateKey(selected)?'selected':''} ${dateKey(d)===dateKey(new Date())?'current':''}" data-date="${dateKey(d)}" aria-label="${fmt(d,{dateStyle:'full'})}">${d.getDate()}</button>`;}).join('')}</div>`;
}
function renderCalendars(){
  $('calendars').innerHTML=state.calendars.map(c=>`<div class="calendar-toggle" style="--cal:${/^#[a-f0-9]{6}$/i.test(c.color)?c.color:'#b4a5f5'}"><input type="checkbox" id="cal-${esc(c.id)}" data-calendar="${esc(c.id)}" ${c.visible?'checked':''}><label class="calendar-name" for="cal-${esc(c.id)}">${esc(c.name)}</label><small>${state.events.filter(e=>e.calendarId===c.id).length}</small><button class="icon-button calendar-remove" data-remove-calendar="${esc(c.id)}" aria-label="Remove ${esc(c.name)}">×</button></div>`).join('');
}
function period(){
  if(view==='day')return [startDay(selected),addDays(startDay(selected),1)];
  if(view==='week'){const d=startWeek(selected,state.settings.monday);return [d,addDays(d,7)];}
  if(view==='month'){const d=startWeek(new Date(selected.getFullYear(),selected.getMonth(),1),state.settings.monday);return [d,addDays(d,42)];}
  return [startDay(selected),addDays(startDay(selected),30)];
}
function render(){
  applySettings();renderMini();renderCalendars();
  $('heading').textContent=fmt(selected,{month:'long',year:'numeric'});
  const [from,to]=period(),query=$('search').value.trim().toLowerCase();
  $('date-range').textContent=view==='month'?fmt(selected,{month:'long',year:'numeric'}):`${fmt(from,{month:'short',day:'numeric'})}${view==='day'?'':` – ${fmt(addDays(to,-1),{month:'short',day:'numeric'})}`}`;
  document.querySelectorAll('[data-view]').forEach(b=>b.classList.toggle('active',b.dataset.view===view));
  let events=occurrences(visibleEvents(),from,to);
  if(query){events=occurrences(visibleEvents(),addDays(new Date(),-365),addDays(new Date(),730)).filter(e=>[e.title,e.location,e.notes].some(s=>s?.toLowerCase().includes(query)));renderAgenda(events,`Search results · ${events.length} occurrences`);}
  else if(view==='month')renderMonth(from,events);
  else if(view==='agenda')renderAgenda(events);
  else renderTimeGrid(from,view==='day'?1:7,events);
  renderSide();
}
function eventMarkup(e,style=''){return `<button class="event" data-event="${esc(e.id)}" data-occurrence="${esc(e.recurrenceId||e.occurrenceDate)}" style="${colorStyle(e.calendarId)};${style}" title="${esc(e.title)} · ${esc(time(e.start))}" draggable="${e.repeat==='none'}"><strong>${esc(e.title)}${e.repeat!=='none'?' ↻':''}</strong>${e.allDay?'':`<span class="event-time">${time(e.start)} – ${time(e.end)}</span>${e.location?`<span class="event-location">${esc(e.location)}</span>`:''}`}</button>`;}
function renderTimeGrid(from,days,events){
  const container=$('calendar-view'),oldScroll=container.scrollTop;
  let html=`<div class="week-header" style="--days:${days}"><div class="tz">LOCAL</div>${Array.from({length:days},(_,i)=>{const d=addDays(from,i);return `<div class="day-head ${dateKey(d)===dateKey(new Date())?'current':''}"><span>${fmt(d,{weekday:'short'}).toUpperCase()}</span><strong>${d.getDate()}</strong></div>`;}).join('')}</div>`;
  html+=`<div class="all-day-row" style="--days:${days}"><span class="all-day-label">all day</span>${Array.from({length:days},(_,i)=>{const d=addDays(from,i);return `<div class="all-day-cell" data-drop-date="${dateKey(d)}">${events.filter(e=>e.allDay&&new Date(e.start)<addDays(d,1)&&new Date(e.end)>d).map(e=>eventMarkup(e)).join('')}</div>`;}).join('')}</div>`;
  html+=`<div class="time-grid" style="--days:${days}"><div class="hours">${Array.from({length:23},(_,i)=>{const h=i+1;return `<span class="hour" style="top:${h*60}px">${state.settings.clock24?String(h).padStart(2,'0')+':00':(h%12||12)+(h<12?' AM':' PM')}</span>`;}).join('')}</div>`;
  for(let i=0;i<days;i++){
    const d=addDays(from,i),next=addDays(d,1),items=events.filter(e=>!e.allDay&&new Date(e.start)<next&&new Date(e.end)>d).map(e=>{const s=new Date(e.start),end=new Date(e.end);return {e,top:s<d?0:s.getHours()*60+s.getMinutes(),bottom:end>=next?1440:end.getHours()*60+end.getMinutes()};}).sort((a,b)=>a.top-b.top||b.bottom-a.bottom);
    // Partition overlapping events into clusters, then allocate reusable columns.
    const groups=[];for(const item of items){const group=groups.at(-1);if(!group||item.top>=group.end)groups.push({items:[item],end:item.bottom});else{group.items.push(item);group.end=Math.max(group.end,item.bottom);}}
    for(const group of groups){const ends=[];for(const item of group.items){let col=ends.findIndex(end=>end<=item.top);if(col<0)col=ends.length;ends[col]=item.bottom;item.column=col;}for(const item of group.items)item.columns=ends.length;}
    html+=`<div class="day-column ${[0,6].includes(d.getDay())?'weekend':''}" data-drop-date="${dateKey(d)}">${Array.from({length:48},(_,j)=>`<button class="time-slot" style="top:${j*30}px" data-slot="${dateKey(d)}T${String(Math.floor(j/2)).padStart(2,'0')}:${j%2?'30':'00'}" aria-label="Create event ${fmt(d,{weekday:'long'})} at ${Math.floor(j/2)}:${j%2?'30':'00'}"></button>`).join('')}${items.map(item=>eventMarkup(item.e,`top:${item.top}px;height:${Math.max(22,item.bottom-item.top-2)}px;left:calc(${item.column/item.columns*100}% + 3px);width:calc(${100/item.columns}% - 6px)`)).join('')}${dateKey(d)===dateKey(new Date())?`<div class="now-line" style="top:${new Date().getHours()*60+new Date().getMinutes()}px"></div>`:''}</div>`;
  }
  container.innerHTML=html+'</div>';container.scrollTop=oldScroll||420;
}
function renderMonth(from,events){
  const weekdays=state.settings.monday?['MON','TUE','WED','THU','FRI','SAT','SUN']:['SUN','MON','TUE','WED','THU','FRI','SAT'];
  $('calendar-view').innerHTML=`<div class="month-grid">${weekdays.map(w=>`<div class="month-weekday">${w}</div>`).join('')}${Array.from({length:42},(_,i)=>{const d=addDays(from,i),items=events.filter(e=>new Date(e.start)<addDays(d,1)&&new Date(e.end)>d);return `<div class="month-cell ${d.getMonth()!==selected.getMonth()?'outside':''} ${dateKey(d)===dateKey(new Date())?'current':''}" data-drop-date="${dateKey(d)}"><button class="day-number" data-month-date="${dateKey(d)}" aria-label="Create event on ${fmt(d,{dateStyle:'full'})}">${d.getDate()}</button>${items.slice(0,3).map(e=>`<button class="month-event" data-event="${esc(e.id)}" data-occurrence="${esc(e.recurrenceId||e.occurrenceDate)}" style="${colorStyle(e.calendarId)}" draggable="${e.repeat==='none'}">${e.allDay?'':time(e.start)+' '}${esc(e.title)}</button>`).join('')}${items.length>3?`<button class="month-event" data-date-view="${dateKey(d)}" style="--event-color:var(--muted)">+${items.length-3} more</button>`:''}</div>`;}).join('')}</div>`;
}
function renderAgenda(events,label=''){
  if(!events.length){$('calendar-view').innerHTML=`<div class="empty-view"><div class="empty-icon">✧</div><h2>${$('search').value?'No matching events':'A little room for possibility.'}</h2><p>${$('search').value?'Search covers one year back and two years ahead. Try another title, location, or note.':'Your next 30 days are open. Add your first event, import a calendar, or try a sample week in Preferences.'}</p><button class="primary" data-action="new">＋ Create event</button></div>`;return;}
  let key='',html=label?`<p class="hint">${esc(label)} · past year and next two years</p>`:'';
  for(const e of events){const d=new Date(e.start);if(dateKey(d)!==key){key=dateKey(d);html+=`<div class="agenda-date"><strong>${d.getDate()}</strong>${fmt(d,{weekday:'long',month:'long',year:'numeric'})}</div>`;}html+=`<button class="agenda-event" data-event="${esc(e.id)}" data-occurrence="${esc(e.recurrenceId||e.occurrenceDate)}" style="${colorStyle(e.calendarId)}"><span class="agenda-time">${e.allDay?'All day':time(e.start)+' – '+time(e.end)}</span><span class="event-dot"></span><div><strong>${esc(e.title)}${e.repeat!=='none'?' ↻':''}</strong><small>${esc(state.calendars.find(c=>c.id===e.calendarId)?.name)}${e.location?' · '+esc(e.location):''}</small></div></button>`;}
  $('calendar-view').innerHTML=`<div class="agenda-list">${html}</div>`;
}
function renderSide(){
  const today=startDay(new Date()),tomorrow=addDays(today,1),items=occurrences(visibleEvents(),today,tomorrow),timed=items.filter(e=>!e.allDay),minutes=timed.reduce((sum,e)=>sum+Math.max(0,Math.min(+new Date(e.end),+tomorrow)-Math.max(+new Date(e.start),+today))/60000,0);
  $('day-summary').innerHTML=`<div class="summary-card"><div class="summary-top"><span>${fmt(today,{weekday:'short',month:'short',day:'numeric'})}</span><strong>${items.length} <small style="font-size:10px;color:var(--muted)">events</small></strong></div><div class="summary-bars">${Array.from({length:12},(_,i)=>`<span style="${i<Math.ceil(minutes/60)?'background:var(--accent)':''}"></span>`).join('')}</div><p>${minutes?(minutes/60).toFixed(1)+' hours scheduled · overlaps counted separately':'An open day. Make something of it.'}</p></div>`;
  const now=new Date(),next=occurrences(visibleEvents(),now,addDays(now,7)).slice(0,4);
  $('up-next').innerHTML=next.length?next.map(e=>`<button class="up-event" data-event="${esc(e.id)}" data-occurrence="${esc(e.recurrenceId||e.occurrenceDate)}" style="${colorStyle(e.calendarId)}"><span class="up-time">${dateKey(new Date(e.start))===dateKey(today)?(e.allDay?'All day':time(e.start)):fmt(e.start,{weekday:'short'})}</span><span class="up-detail"><strong>${esc(e.title)}</strong><small>${e.allDay?'All day':Math.round((new Date(e.end)-new Date(e.start))/60000)+' min'}${e.location?' · '+esc(e.location):''}</small></span></button>`).join(''):'<p class="empty-small">Nothing coming up this week.<br>A good moment to look ahead.</p>';
  $('task-count').textContent=state.tasks.filter(t=>!t.done).length;
  const tasksHTML=state.tasks.length?[...state.tasks].sort((a,b)=>a.done-b.done).map(t=>`<div class="task ${t.done?'done':''}"><input type="checkbox" data-task="${esc(t.id)}" aria-label="Complete ${esc(t.title)}" ${t.done?'checked':''}><div class="task-text">${esc(t.title)}${t.due?`<small class="${!t.done&&t.due<dateKey(today)?'overdue':''}">${t.due<dateKey(today)&&!t.done?'Overdue · ':''}${fmt(new Date(t.due+'T12:00'),{month:'short',day:'numeric'})}</small>`:''}</div><button class="icon-button remove-task" data-remove-task="${esc(t.id)}" aria-label="Delete task">×</button></div>`).join(''):'<p class="empty-small">A clear list, a clear head.<br>Add a task with ＋.</p>';
  $('tasks').innerHTML=tasksHTML;$('task-list-items').innerHTML=tasksHTML;
}
function syncRecurrenceFields(){
  const one=!!editing && $('event-scope').value==='one' && !$('scope-label').hidden;
  $('event-repeat').disabled=one;$('event-rule').disabled=one;
  $('rule-label').hidden=$('event-repeat').value!=='custom';
  $('until-label').hidden=['none','custom'].includes($('event-repeat').value)||one;
  $('timezone-label').hidden=$('event-all-day').checked;$('event-timezone').required=!$('event-all-day').checked;
  $('recurrence-hint').textContent=$('scope-label').hidden?'':one?'This change affects only the selected occurrence.':$('event-scope').value==='following'?'Earlier events stay in the original series. The remaining events become a new series.':'This change applies to the entire series. Existing overrides retain their own details.';
}
function fillEditorDates(e){
  const zone=e.timeZone||Intl.DateTimeFormat().resolvedOptions().timeZone;
  $('event-start').value=(e.allDay?(e.startDate||dateKey(new Date(e.start)))+'T00:00':e.startWall||wallTime(e.start,zone)).slice(0,16);
  $('event-end').value=(e.allDay?(e.endDate||dateKey(new Date(e.end)))+'T00:00':eventWallTime(e,e.end,zone)).slice(0,16);
  $('event-timezone').value=zone;
}
function openEvent(event=null,when=null,occurrenceDate=null){
  editing=event?.id?{id:event.id,occurrenceDate}:null;
  let start=when?new Date(when):new Date(selected);if(!when)start.setHours(new Date().getHours()+1,0,0,0);
  const base=event||{title:'',start:start.toISOString(),end:new Date(+start+3600000).toISOString(),calendarId:state.calendars[0]?.id,repeat:'none',reminder:10};
  editorSource=base;
  const recurring=!!event?.id&&base.repeat!=='none'&&!!occurrenceDate;
  const e=recurring?occurrenceFor(base,occurrenceDate):base;
  $('scope-label').hidden=!recurring;$('event-scope').value=recurring?'one':'series';
  $('event-label').textContent=event?.id?'EDIT EVENT':'NEW EVENT';$('event-title').value=e.title;
  $('event-calendar').innerHTML=state.calendars.map(c=>`<option value="${esc(c.id)}">${esc(c.name)}</option>`).join('');$('event-calendar').value=e.calendarId;
  fillEditorDates(e);$('event-all-day').checked=!!e.allDay;
  const zones=[...new Set(['floating','UTC',Intl.DateTimeFormat().resolvedOptions().timeZone,...Intl.supportedValuesOf('timeZone')])];
  $('timezone-options').innerHTML=zones.map(z=>`<option value="${esc(z)}"></option>`).join('');
  $('event-location').value=e.location||'';$('event-notes').value=e.notes||'';$('event-repeat').value=base.repeat;$('event-reminder').value=e.reminder??10;$('event-until').value=base.until||'';$('event-rule').value=base.rrule||'';
  $('delete-event').hidden=!event?.id;$('delete-event').textContent=base.repeat==='none'?'Delete event':'Delete series';$('skip-event').hidden=!recurring;
  syncRecurrenceFields();$('event-error').textContent='';$('event-dialog').showModal();$('event-title').focus();
}
function confirmAction(title,body,action){$('confirm-title').textContent=title;$('confirm-body').textContent=body;$('confirm-dialog').showModal();$('confirm-yes').onclick=async()=>{$('confirm-dialog').close();await action();};}
function quickAdd(){try{const e=parseQuick($('quick').value);if(!e.title)throw new Error('Describe your event first.');openEvent({...e,id:null,calendarId:state.calendars[0].id,reminder:10});editing=null;$('event-label').textContent='REVIEW QUICK EVENT';$('delete-event').hidden=true;$('quick').value='';}catch(e){toast(e.message);}}
function openSmall(mode){smallMode=mode;$('small-heading').textContent=mode==='task'?'One thing at a time.':'A new calendar';$('small-date-label').hidden=mode!=='task';$('small-color-label').hidden=mode!=='calendar';$('small-text').value='';$('small-date').value='';$('small-dialog').showModal();$('small-text').focus();}
async function download(content,kind){try{if(window.daymark){if(await window.daymark.exportFile({content,kind}))toast('Export saved.');}else{const url=URL.createObjectURL(new Blob([content],{type:kind==='ics'?'text/calendar':'application/json'}));const a=document.createElement('a');a.href=url;a.download=kind==='ics'?'daymark-calendar.ics':'daymark-backup.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),2000);}}catch(e){toast(e.message);}}
function chooseFile(mode){fileMode=mode;$('file-input').accept=mode==='ics'?'.ics':'.json';$('file-input').value='';$('file-input').click();}
function renderImportPreview(){
  if(!importPreview)return;
  const plan=planImport(importPreview.events,state.events,$('import-calendar').value);
  $('import-summary').textContent=`${plan.events.length} events to import · ${plan.duplicates} duplicate UIDs skipped`;
  $('confirm-import').disabled=!plan.events.length;
  $('import-preview').innerHTML=plan.events.slice(0,30).map(e=>`<div class="import-preview-row">${esc(e.title)}<small>${esc(e.startWall||e.start)} · ${esc(e.timeZone||'Device timezone')}${e.repeat!=='none'?' · Repeats':''}${e.overrides?.length?' · '+e.overrides.length+' overrides':''}</small></div>`).join('')+(plan.events.length>30?`<div class="import-preview-row">And ${plan.events.length-30} more events.</div>`:'');
}
function bind(){
  document.addEventListener('click',async ev=>{
    const b=ev.target.closest('button');if(!b)return;
    if(b.dataset.close){$(b.dataset.close).close();return;}
    if(b.dataset.event){const e=state.events.find(e=>e.id===b.dataset.event);if(e)openEvent(e,null,b.dataset.occurrence);return;}
    if(b.dataset.view){view=b.dataset.view;$('calendar-view').scrollTop=0;render();return;}
    if(b.dataset.mini){miniMonth=new Date(miniMonth.getFullYear(),miniMonth.getMonth()+Number(b.dataset.mini),1);renderMini();return;}
    if(b.dataset.date){selected=new Date(b.dataset.date+'T12:00');render();return;}
    if(b.dataset.slot){openEvent(null,b.dataset.slot);return;}
    if(b.dataset.monthDate){selected=new Date(b.dataset.monthDate+'T12:00');openEvent();return;}
    if(b.dataset.dateView){selected=new Date(b.dataset.dateView+'T12:00');view='day';render();return;}
    if(b.dataset.action==='new')openEvent();
    if(b.dataset.removeTask){state.tasks=state.tasks.filter(t=>t.id!==b.dataset.removeTask);await save();renderSide();}
    if(b.dataset.removeCalendar){if(state.calendars.length===1){toast('Keep at least one calendar.');return;}const c=state.calendars.find(c=>c.id===b.dataset.removeCalendar);confirmAction('Remove '+c.name+'?', 'This also removes all events in this calendar. Save a backup first if you need them.',async()=>{state.events=state.events.filter(e=>e.calendarId!==c.id);state.calendars=state.calendars.filter(x=>x.id!==c.id);await save();render();});}
  });
  document.addEventListener('change',async ev=>{if(ev.target.dataset.calendar){state.calendars.find(c=>c.id===ev.target.dataset.calendar).visible=ev.target.checked;await save();render();}if(ev.target.dataset.task){state.tasks.find(t=>t.id===ev.target.dataset.task).done=ev.target.checked;await save();renderSide();}});
  $('new-event').onclick=()=>openEvent();$('new-task').onclick=()=>openSmall('task');$('add-calendar').onclick=()=>openSmall('calendar');
  $('show-tasks').onclick=()=>$('tasks-dialog').showModal();$('dialog-new-task').onclick=()=>openSmall('task');
  $('today').onclick=()=>{selected=startDay(new Date());miniMonth=new Date(selected);render();};
  const navigate=n=>{if(view==='month')selected=new Date(selected.getFullYear(),selected.getMonth()+n,1);else selected=addDays(selected,n*(view==='week'?7:view==='agenda'?30:1));miniMonth=new Date(selected);$('calendar-view').scrollTop=0;render();};
  $('prev').onclick=()=>navigate(-1);$('next').onclick=()=>navigate(1);
  $('quick-add').onclick=quickAdd;$('quick').addEventListener('keydown',e=>{if(e.key==='Enter')quickAdd();});
  $('search').addEventListener('input',()=>render());
  $('theme').onclick=async()=>{state.settings.theme=state.settings.theme==='dark'?'light':'dark';await save();applySettings();};
  $('accounts').onclick=()=>$('accounts-dialog').showModal();
  $('settings').onclick=()=>{$('setting-theme').value=state.settings.theme;$('setting-week').value=state.settings.monday?'monday':'sunday';$('setting-clock').value=state.settings.clock24?'24':'12';$('settings-dialog').showModal();};
  for(const id of ['setting-theme','setting-week','setting-clock'])$(id).onchange=async()=>{state.settings.theme=$('setting-theme').value;state.settings.monday=$('setting-week').value==='monday';state.settings.clock24=$('setting-clock').value==='24';await save();render();};
  $('event-repeat').onchange=syncRecurrenceFields;
  $('event-scope').onchange=()=>{const base=state.events.find(e=>e.id===editing?.id);if(!base)return;fillEditorDates($('event-scope').value==='series'?base:occurrenceFor(base,editing.occurrenceDate));syncRecurrenceFields();};
  $('event-start').onchange=()=>{const s=new Date($('event-start').value),end=new Date($('event-end').value);if(end<=s)$('event-end').value=localInput(new Date(+s+3600000));};
  $('event-all-day').onchange=()=>{if($('event-all-day').checked){const s=startDay(new Date($('event-start').value));$('event-start').value=localInput(s);$('event-end').value=localInput(addDays(s,1));}syncRecurrenceFields();};
  $('event-form').onsubmit=async ev=>{
    ev.preventDefault();try{
      const old=editing?state.events.find(e=>e.id===editing.id):null;
      const allDay=$('event-all-day').checked,zone=allDay?'floating':$('event-timezone').value.trim();
      let startWall=$('event-start').value+':00',endWall=$('event-end').value+':00';
      if(allDay){startWall=startWall.slice(0,10);endWall=endWall.slice(0,10);if(endWall<=startWall)endWall=dateKey(addDays(new Date(startWall+'T12:00'),1));}
      const start=resolveEventTime(old,startWall,zone),end=resolveEventTime(old,endWall,zone);
      const repeat=$('event-repeat').value;
      const changes={title:$('event-title').value.trim(),calendarId:$('event-calendar').value,start,end,startWall,endWall,startDate:allDay?startWall:undefined,endDate:allDay?endWall:undefined,allDay,timeZone:zone,endTimeZone:zone,location:$('event-location').value.trim(),notes:$('event-notes').value,reminder:Number($('event-reminder').value)};
      const scope=$('scope-label').hidden?'series':$('event-scope').value;
      if(scope!=='one'){
        changes.repeat=repeat;
        if(repeat==='custom'){const rule=$('event-rule').value.trim().toUpperCase();validateRule(rule);if(!rule&&!old?.ical)throw new Error('Enter a recurrence rule.');changes.rrule=rule;}
        else if(!old||repeat!==old.repeat||$('event-until').value!==(old.until||'')){changes.rrule=undefined;changes.count=null;changes.interval=1;changes.until=repeat==='none'?'':$('event-until').value;}
      }
      validateEvent({...old,...changes,id:old?.id||uid(),repeat:scope==='one'?'none':changes.repeat||old?.repeat||'none',until:scope==='one'?'':changes.until??old?.until??''});
      if(old){const replacements=old.repeat!=='none'&&editing.occurrenceDate?editOccurrence(old,editing.occurrenceDate,changes,scope):[updateEvent(old,changes)];const i=state.events.findIndex(e=>e.id===old.id);state.events.splice(i,1,...replacements);}
      else state.events.push(updateEvent({id:uid(),repeat:'none',reminder:10},changes));
      await save();$('event-dialog').close();render();toast(old?'Event updated.':'A little more shape to your day.');
    }catch(e){$('event-error').textContent=e.message;}
  };
  $('delete-event').onclick=()=>{if(!editing)return;const id=editing.id;confirmAction('Delete this '+(state.events.find(e=>e.id===id).repeat==='none'?'event':'series')+'?','This removes it from your local calendar.',async()=>{state.events=state.events.filter(e=>e.id!==id);await save();$('event-dialog').close();render();toast('Event deleted.');});};
  $('skip-event').onclick=async()=>{if(!editing?.occurrenceDate)return;const i=state.events.findIndex(e=>e.id===editing.id);state.events[i]=skipOccurrence(state.events[i],editing.occurrenceDate);await save();$('event-dialog').close();render();toast('Occurrence skipped.');};
  $('small-form').onsubmit=async ev=>{ev.preventDefault();const name=$('small-text').value.trim();if(!name)return;if(smallMode==='task')state.tasks.push({id:uid(),title:name,due:$('small-date').value,done:false});else state.calendars.push({id:uid(),name,color:$('small-color').value,visible:true});await save();$('small-dialog').close();render();};
  $('import-ics').onclick=()=>chooseFile('ics');$('account-import').onclick=()=>chooseFile('ics');$('restore').onclick=()=>chooseFile('json');
  $('export-ics').onclick=()=>download(exportICS(state.events),'ics');$('backup').onclick=()=>download(JSON.stringify(state,null,2),'json');
  $('load-demo').onclick=async()=>{if(state.calendars.some(c=>c.id==='sample')){toast('The sample calendar is already present.');return;}state.calendars.push({id:'sample',name:'Sample week',color:'#8ed8c2',visible:true});state.events.push(...demoEvents().map(e=>({...e,calendarId:'sample'})));await save();selected=startDay(new Date());miniMonth=new Date(selected);view='week';$('settings-dialog').close();render();toast('Sample week added. Remove its calendar whenever you like.');};
  $('file-input').onchange=async()=>{const file=$('file-input').files[0];if(!file)return;try{if(file.size>20_000_000)throw new Error('Choose a file under 20 MB.');const text=await file.text();if(fileMode==='ics'){const incoming=importICS(text,state.calendars[0].id);importPreview={events:incoming,fileName:file.name};$('import-file-name').textContent=file.name;$('import-calendar').innerHTML=state.calendars.map(c=>`<option value="${esc(c.id)}">${esc(c.name)}</option>`).join('');renderImportPreview();$('import-dialog').showModal();}else{const incoming=JSON.parse(text);validateState(incoming);confirmAction('Replace your calendar with this backup?','Your current events, tasks, and preferences will be replaced. Save a backup first if you need them.',async()=>{state=incoming;await save();render();toast('Backup restored.');});}}catch(e){toast(e.message);}};
  $('import-calendar').onchange=renderImportPreview;
  $('confirm-import').onclick=async()=>{if(!importPreview)return;const plan=planImport(importPreview.events,state.events,$('import-calendar').value);state.events.push(...plan.events);await save();$('import-dialog').close();importPreview=null;render();toast(plan.events.length+' events imported.');};
  $('confirm-no').onclick=()=>$('confirm-dialog').close();
  document.addEventListener('keydown',e=>{if(document.querySelector('dialog[open]')||['INPUT','TEXTAREA','SELECT'].includes(document.activeElement.tagName)||e.ctrlKey||e.altKey||e.metaKey)return;const k=e.key.toLowerCase();if(k==='n')openEvent();if(k==='t')$('today').click();if(k==='/'){e.preventDefault();$('search').focus();}if(['1','2','3','4'].includes(k)){view=['day','week','month','agenda'][+k-1];render();}if(e.key==='ArrowLeft')navigate(-1);if(e.key==='ArrowRight')navigate(1);});
  let dragged=null;document.addEventListener('dragstart',ev=>{const el=ev.target.closest('[data-event]');if(!el)return;const e=state.events.find(e=>e.id===el.dataset.event);if(e.repeat!=='none'){ev.preventDefault();return;}dragged=e.id;ev.dataTransfer.effectAllowed='move';ev.dataTransfer.setData('text/plain',e.id);});
  document.addEventListener('dragover',ev=>{if(dragged&&ev.target.closest('[data-drop-date]')){ev.preventDefault();ev.dataTransfer.dropEffect='move';}});
  document.addEventListener('drop',async ev=>{const cell=ev.target.closest('[data-drop-date]');if(!cell||!dragged)return;ev.preventDefault();const e=state.events.find(e=>e.id===dragged);dragged=null;const old=new Date(e.start),duration=new Date(e.end)-old,s=new Date(cell.dataset.dropDate+'T00:00');s.setHours(old.getHours(),old.getMinutes());if(cell.classList.contains('day-column')&&!e.allDay){const minutes=Math.max(0,Math.min(1410,Math.floor((ev.clientY-cell.getBoundingClientRect().top)/30)*30));s.setHours(Math.floor(minutes/60),minutes%60);}const oldEnd=new Date(e.end);e.start=s.toISOString();e.end=(e.allDay?addDays(s,Math.round((Date.UTC(oldEnd.getFullYear(),oldEnd.getMonth(),oldEnd.getDate())-Date.UTC(old.getFullYear(),old.getMonth(),old.getDate()))/86400000)):new Date(+s+duration)).toISOString();state.events[state.events.findIndex(x=>x.id===e.id)]=updateEvent(e,{start:e.start,end:e.end});await save();render();toast('Event moved.');});
  document.addEventListener('dragend',()=>dragged=null);
}
function validateState(s){
  if(!s||s.version!==1||!Array.isArray(s.events)||!Array.isArray(s.tasks)||!Array.isArray(s.calendars)||!s.calendars.length||s.calendars.length>100||!s.settings)throw new Error('This is not a valid Daymark backup.');
  const ids=new Set();for(const c of s.calendars){if(typeof c.id!=='string'||ids.has(c.id)||typeof c.name!=='string'||!/^#[a-f0-9]{6}$/i.test(c.color)||typeof c.visible!=='boolean')throw new Error('Invalid calendar in backup.');ids.add(c.id);}
  const eventIds=new Set();for(const e of s.events){validateEvent(e);if(typeof e.id!=='string'||eventIds.has(e.id)||!ids.has(e.calendarId)||!Number.isFinite(e.reminder)||typeof e.allDay!=='boolean'||(e.interval&&(!Number.isInteger(e.interval)||e.interval<1))||(e.count&&(!Number.isInteger(e.count)||e.count<1))||(e.exceptions&&!Array.isArray(e.exceptions)))throw new Error('Invalid event in backup.');if(e.rrule)validateRule(e.rrule);if(e.ical||e.overrides)occurrences([e],new Date(e.start),new Date(+new Date(e.start)+86400000));eventIds.add(e.id);}
  const taskIds=new Set();for(const t of s.tasks){if(typeof t.id!=='string'||taskIds.has(t.id)||typeof t.title!=='string'||typeof t.done!=='boolean'||(t.due&&!/^\d{4}-\d{2}-\d{2}$/.test(t.due)))throw new Error('Invalid task in backup.');taskIds.add(t.id);}
  if(!['dark','light'].includes(s.settings.theme)||typeof s.settings.monday!=='boolean'||typeof s.settings.clock24!=='boolean')throw new Error('Invalid preferences in backup.');
  if(!Array.isArray(s.notified))s.notified=[];return s;
}
async function reminders(){
  if(!ready||!window.daymark)return;
  const now=Date.now(),range=occurrences(state.events,new Date(now-86400000),new Date(now+2*86400000));let changed=false;
  for(const e of range){if(e.reminder<0||e.reminder==null)continue;const at=+new Date(e.start)-e.reminder*60000,key=e.instanceId+':'+at;if(at<=now&&at>now-60000&&!state.notified.includes(key)){const shown=await window.daymark.notify({title:e.title,body:(e.allDay?'All day':time(e.start))+(e.location?' · '+e.location:'')});if(shown){state.notified.push(key);changed=true;}}}
  if(changed){state.notified=state.notified.slice(-1000);await save();}
}
try{
  const stored=window.daymark?await window.daymark.load():JSON.parse(localStorage.getItem('daymark.v1')||'null');state=stored?validateState(stored):initialState();state.events=state.events.map(e=>e.timeZone?e:updateEvent(e,{timeZone:e.allDay?'floating':state.settings.zone||Intl.DateTimeFormat().resolvedOptions().timeZone}));ready=true;bind();render();await save();
  if(!stored)setTimeout(()=>toast('Welcome to Daymark. Create an event or try a sample week in Preferences.'),500);
  await reminders();setInterval(reminders,15000);setInterval(()=>{if(!document.querySelector('dialog[open]'))renderSide();},60000);
}catch(e){$('calendar-view').innerHTML=`<div class="empty-view"><h2>Your data needs attention.</h2><p>${esc(e.message)}</p><p>No data has been overwritten. Your desktop data is in %APPDATA%/Daymark/calendar.json; the previous save is calendar.json.bak.</p></div>`;toast(e.message);}
