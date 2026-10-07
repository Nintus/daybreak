export const pad = n => String(n).padStart(2, '0');
export const dateKey = d => `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}`;
export const localInput = d => `${dateKey(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
export const addDays = (d,n) => { const r=new Date(d); r.setDate(r.getDate()+n); return r; };
export const startDay = d => new Date(d.getFullYear(),d.getMonth(),d.getDate());
export const startWeek = (d,monday=true) => addDays(startDay(d),-((d.getDay()+(monday?6:0))%7));
export const uid = () => globalThis.crypto.randomUUID();
export const calendars = [
  {id:'personal',name:'Personal',color:'#b4a5f5',visible:true},
  {id:'work',name:'Work',color:'#8ed8c2',visible:true},
  {id:'focus',name:'Focus time',color:'#f3c98b',visible:true}
];
export function validateEvent(e) {
  if (!e.title?.trim()) throw new Error('Give your event a title.');
  if (!Number.isFinite(new Date(e.start).getTime()) || !Number.isFinite(new Date(e.end).getTime())) throw new Error('Choose valid start and end dates.');
  if (new Date(e.end)<=new Date(e.start)) throw new Error('The end must be after the start.');
  if (!['none','daily','weekly','monthly','yearly','custom'].includes(e.repeat)) throw new Error('This recurrence is not supported.');
  if (e.until && e.until < dateKey(new Date(e.start))) throw new Error('The repeat end date must be after the event starts.');
  return e;
}
export function parseQuick(text, now=new Date()) {
  let title=text.trim(), start=new Date(now); start.setSeconds(0,0); start.setMinutes(0); start.setHours(start.getHours()+1);
  const day=title.match(/\b(today|tomorrow|next (?:monday|tuesday|wednesday|thursday|friday|saturday|sunday))\b/i);
  if(day){
    start=new Date(now);start.setSeconds(0,0);start.setHours(Math.min(23,now.getHours()+1),0);
    if(day[1].toLowerCase()==='tomorrow') start=addDays(start,1);
    else if(day[1].toLowerCase().startsWith('next ')) {const index=['sunday','monday','tuesday','wednesday','thursday','friday','saturday'].indexOf(day[1].slice(5).toLowerCase());start=addDays(start,(index-start.getDay()+7)%7||7);}
    title=title.replace(day[0],'');
  }
  const time=title.match(/\b(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)\b/i);
  if(time){const h=+time[1],m=+(time[2]||0);if(h<1||h>12||m>59)throw new Error('Use a valid time, such as 2:30pm.');start.setHours(h%12+(time[3].toLowerCase()==='pm'?12:0),m);title=title.replace(time[0],'');}
  const length=title.match(/\bfor\s+(\d+)\s*(minutes?|mins?|hours?|hrs?)\b/i);
  let minutes=60;if(length){minutes=+length[1]*(length[2].toLowerCase().startsWith('h')?60:1);title=title.replace(length[0],'');}
  const repeat=title.match(/\b(?:every day|daily|every week|weekly|every month|monthly)\b/i);
  let frequency='none';if(repeat){frequency=/day|daily/i.test(repeat[0])?'daily':/week/i.test(repeat[0])?'weekly':'monthly';title=title.replace(repeat[0],'');}
  const location=title.match(/\s+@\s*(.+)$/);if(location)title=title.replace(location[0],'');
  return {title:title.replace(/\s+/g,' ').trim(),start:start.toISOString(),end:new Date(+start+minutes*60000).toISOString(),repeat:frequency,location:location?.[1]||''};
}
export {occurrences,importICS,exportICS,wallTime,instantFromWall,editOccurrence,skipOccurrence,updateEvent,occurrenceFor,resolveEventTime,eventWallTime,validateRule,isTimeZone} from './ical-engine.mjs';
export function initialState() {
  return {version:1,calendars:structuredClone(calendars),events:[],tasks:[],settings:{theme:'dark',monday:true,clock24:false,zone:Intl.DateTimeFormat().resolvedOptions().timeZone},notified:[]};
}
export function demoEvents(now=new Date()) {
  const base=startWeek(now);const make=(offset,h,m,title,calendarId,length,extra={})=>{let s=addDays(base,offset);s.setHours(h,m);return {id:uid(),title,start:s.toISOString(),end:new Date(+s+length*60000).toISOString(),calendarId,repeat:'none',allDay:false,reminder:10,notes:'',location:'',...extra};};
  return [make(0,9,0,'Weekly planning','work',45,{repeat:'weekly'}),make(1,10,0,'Design workshop','work',90,{location:'Studio / Teams'}),make(2,9,0,'Deep work','focus',120),make(2,12,30,'Lunch with Alex','personal',60,{location:'The corner café'}),make(3,10,0,'Product review','work',60),make(4,14,0,'A little space to think','focus',90),make(5,10,0,'Farmers market','personal',60)];
}
