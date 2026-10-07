import ICAL from './vendor/ical.js';

const copy = value => structuredClone(value);
const pad = n => String(n).padStart(2,'0');
const localZone = () => Intl.DateTimeFormat().resolvedOptions().timeZone;
const key = d => `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}`;
const wallString = t => t.toString().replace(/Z$/,'');
const formatters = new Map();
export function isTimeZone(zone) {
  if(['floating','UTC'].includes(zone)) return true;
  try { new Intl.DateTimeFormat('en',{timeZone:zone}); return true; } catch { return false; }
}
export function wallTime(instant, zone=localZone()) {
  const d=new Date(instant);
  if(zone==='floating')return `${key(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
  if(!formatters.has(zone))formatters.set(zone,new Intl.DateTimeFormat('en-CA',{timeZone:zone,year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',second:'2-digit',hourCycle:'h23'}));
  const p=Object.fromEntries(formatters.get(zone).formatToParts(d).map(x=>[x.type,x.value]));
  return `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}:${p.second}`;
}
export function instantFromWall(wall,zone=localZone(),root=null) {
  if(wall.length===10)wall+='T00:00:00';
  if(wall.length===16)wall+=':00';
  const m=wall.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})$/);
  if(!m)throw new Error('Choose a valid date and time.');
  const [y,mo,d,h,mi,s]=m.slice(1).map(Number);
  if(mo<1||mo>12||d<1||d>new Date(y,mo,0).getDate()||h>23||mi>59||s>59)throw new Error('Invalid calendar date.');
  if(zone==='floating')return new Date(y,mo-1,d,h,mi,s).toISOString();
  const embedded=root?.getTimeZoneByID(zone);
  if(embedded && zone!=='UTC') {const t=ICAL.Time.fromString(wall);t.zone=embedded;return t.toJSDate().toISOString();}
  if(!isTimeZone(zone))throw new Error(`Unknown timezone: ${zone}. Include its VTIMEZONE definition.`);
  const target=Date.UTC(y,mo-1,d,h,mi,s);
  if(zone==='UTC')return new Date(target).toISOString();
  // Probe both sides of offset transitions. The earlier instant wins during a fold.
  const candidates=new Set();
  for(const delta of [-36,-12,0,12,36]) {
    const probe=target+delta*3600000;
    const parts=wallTime(probe,zone).match(/\d+/g).map(Number);
    const offset=Date.UTC(parts[0],parts[1]-1,parts[2],parts[3],parts[4],parts[5])-probe;
    const candidate=target-offset;
    if(wallTime(candidate,zone)===wall)candidates.add(candidate);
  }
  if(!candidates.size)throw new Error(`This time does not exist in ${zone} because of daylight saving. Choose another time.`);
  return new Date(Math.min(...candidates)).toISOString();
}
function zoneOf(prop) { const t=prop.getFirstValue();return t.isDate?'floating':prop.getParameter('tzid')||(t.zone===ICAL.Timezone.utcTimezone?'UTC':'floating'); }
function instant(t,prop,root) { return instantFromWall(wallString(t),zoneOf(prop),root); }
function zonedWall(iso,zone,root) {
  const embedded=root?.getTimeZoneByID(zone);
  if(embedded&&zone!=='UTC')return wallString(ICAL.Time.fromJSDate(new Date(iso),true).convertToZone(embedded));
  return wallTime(iso,zone);
}
function recurrenceKey(comp,e,root) {
  const p=comp.getFirstProperty('recurrence-id');if(!p)throw new Error('An override is missing RECURRENCE-ID.');
  const t=p.getFirstValue();return t.isDate?wallString(t):zonedWall(instant(t,p,root),e.timeZone||localZone(),root);
}
function newRoot(timezones=[]) {
  const root=new ICAL.Component(['vcalendar',[],[]]);root.updatePropertyWithValue('version','2.0');root.updatePropertyWithValue('prodid','-//Daymark//Calendar 0.2//EN');
  for(const zone of timezones)root.addSubcomponent(new ICAL.Component(copy(zone)));
  return root;
}
function setTime(comp,name,wall,zone,isDate=false) {
  comp.removeAllProperties(name);const prop=new ICAL.Property(name);
  const t=ICAL.Time.fromString(isDate?wall.slice(0,10):wall.replace(/Z$/,'')+(zone==='UTC'?'Z':''));
  prop.setValue(t);if(!isDate&&!['floating','UTC'].includes(zone))prop.setParameter('tzid',zone);comp.addProperty(prop);
  return prop;
}
function simpleRule(e) {
  if(e.repeat==='none')return '';
  let rule=`FREQ=${e.repeat.toUpperCase()}`;
  if(e.interval>1)rule+=`;INTERVAL=${e.interval}`;
  if(e.count)rule+=`;COUNT=${e.count}`;
  if(e.until)rule+=`;UNTIL=${e.allDay?e.until.replace(/-/g,''):instantFromWall(e.until+'T23:59:59',e.timeZone||localZone()).replace(/[-:]/g,'').replace(/\.\d{3}/,'')}`;
  return rule;
}
export function validateRule(rule) {
  if(!rule)return;
  const recur=ICAL.Recur.fromString(rule);
  if(!['DAILY','WEEKLY','MONTHLY','YEARLY'].includes(recur.freq))throw new Error('Use DAILY, WEEKLY, MONTHLY, or YEARLY recurrence.');
  if(!Number.isInteger(recur.interval)||recur.interval<1)throw new Error('Repeat interval must be positive.');
  if(recur.count!=null&&(!Number.isInteger(recur.count)||recur.count<1))throw new Error('Repeat count must be positive.');
  if(recur.count&&recur.until)throw new Error('Choose COUNT or UNTIL, not both.');
  const supported=['FREQ','INTERVAL','COUNT','UNTIL','WKST','BYDAY','BYMONTHDAY','BYMONTH','BYSETPOS','BYYEARDAY','BYWEEKNO','BYHOUR','BYMINUTE','BYSECOND'];
  for(const part of rule.split(';'))if(!supported.includes(part.split('=')[0]))throw new Error('Unsupported recurrence field: '+part.split('=')[0]);
}
function rootFor(e) {
  const root=newRoot(e.timezones),comp=e.ical?new ICAL.Component(copy(e.ical)):new ICAL.Component(['vevent',[],[]]);root.addSubcomponent(comp);
  const zone=e.timeZone||localZone();
  comp.updatePropertyWithValue('uid',e.uid||e.id+'@daymark');comp.updatePropertyWithValue('summary',e.title);
  if(!comp.hasProperty('dtstamp'))comp.updatePropertyWithValue('dtstamp',ICAL.Time.fromJSDate(new Date(),true));
  comp.updatePropertyWithValue('location',e.location||'');comp.updatePropertyWithValue('description',e.notes||'');
  const startWall=e.allDay?(e.startDate||key(new Date(e.start))):e.startWall||zonedWall(e.start,zone,root);
  const endZone=e.endTimeZone||zone;
  const endWall=e.allDay?(e.endDate||key(new Date(e.end))):e.endWall||zonedWall(e.end,endZone,root);
  setTime(comp,'dtstart',startWall,zone,e.allDay);setTime(comp,'dtend',endWall,endZone,e.allDay);comp.removeAllProperties('duration');
  comp.removeAllProperties('rrule');const rule=e.rrule??simpleRule(e);if(rule){validateRule(rule);comp.addPropertyWithValue('rrule',ICAL.Recur.fromString(rule));}
  // The raw component retains RDATE and imported EXDATE; local skips are added.
  for(const value of e.exceptions||[]) {
    const wall=value.length===10&&!e.allDay?value+startWall.slice(10):value;
    const t=ICAL.Time.fromString(e.allDay?wall.slice(0,10):wall+(zone==='UTC'&&!wall.endsWith('Z')?'Z':''));
    const p=new ICAL.Property('exdate');p.setValue(t);if(!e.allDay&&!['floating','UTC'].includes(zone))p.setParameter('tzid',zone);comp.addProperty(p);
  }
  for(const override of e.overrides||[])root.addSubcomponent(new ICAL.Component(copy(override)));
  return root;
}
function normalize(comp,root,calendarId,extras={}) {
  const event=new ICAL.Event(comp),startProp=comp.getFirstProperty('dtstart');if(!startProp)throw new Error('An event has no start date.');
  const start=event.startDate,end=event.endDate,zone=zoneOf(startProp),endProp=comp.getFirstProperty('dtend')||startProp;
  const rule=comp.getFirstPropertyValue('rrule'),rrule=rule?.toString()||'',hasDates=comp.hasProperty('rdate');validateRule(rrule);
  for(const p of comp.getAllProperties('rdate'))if(p.type==='period')throw new Error('RDATE periods are not supported yet. Use date-time values.');
  if(comp.getAllProperties('rrule').length>1)throw new Error('Multiple recurrence rules on one event are not supported.');
  const result={id:crypto.randomUUID(),uid:event.uid||crypto.randomUUID(),title:event.summary||'Untitled event',start:instant(start,startProp,root),end:instant(end,endProp,root),allDay:start.isDate,timeZone:zone,startWall:wallString(start),endWall:wallString(end),startDate:start.isDate?wallString(start):undefined,endDate:end.isDate?wallString(end):undefined,repeat:rule?(Object.keys(rule.parts).length===0&&rule.interval===1&&!rule.count?rule.freq.toLowerCase():'custom'):hasDates?'custom':'none',rrule,until:rule?.until?wallString(rule.until).slice(0,10):'',calendarId,location:event.location||'',notes:event.description||'',reminder:10,exceptions:[],ical:copy(comp.toJSON()),timezones:root.getAllSubcomponents('vtimezone').map(x=>copy(x.toJSON())),overrides:[],...extras};
  if(new Date(result.end)<=new Date(result.start))throw new Error('The end must be after the start.');
  result.endTimeZone=zoneOf(endProp);
  return result;
}
function fromTime(t,comp,root,name='dtstart') { return instant(t,comp.getFirstProperty(name)||comp.getFirstProperty('dtstart'),root); }
export function occurrences(events,from,to) {
  const result=[];
  for(const e of events) {
    const root=rootFor(e),comp=root.getFirstSubcomponent('vevent'),event=new ICAL.Event(comp);
    const rule=comp.getFirstPropertyValue('rrule');
    const eventZone=zoneOf(comp.getFirstProperty('dtstart'));
    if(rule?.until?.zone===ICAL.Timezone.utcTimezone&&!['UTC','floating'].includes(eventZone)&&!root.getTimeZoneByID(eventZone))rule.until=ICAL.Time.fromString(wallTime(rule.until.toJSDate(),eventZone));
    // ICAL.js normalizes implicit yearly leap days to March 1; explicit filters retain RFC semantics.
    if(rule?.freq==='YEARLY'&&event.startDate.month===2&&event.startDate.day===29&&!Object.keys(rule.parts).length){rule.setComponent('BYMONTH',[2]);rule.setComponent('BYMONTHDAY',[29]);}
    const iterator=event.iterator();
    const overrides=new Map(root.getAllSubcomponents('vevent').slice(1).map(c=>[recurrenceKey(c,e,root),c]));
    const seen=new Set();let steps=0,next;
    // Expand wall-clock recurrence; conversion to an instant happens per occurrence.
    while((next=iterator.next())) {
      if(++steps>100000)throw new Error('This series is too large to expand safely. Narrow its recurrence rule.');
      const recurrenceId=wallString(next),override=overrides.get(recurrenceId);
      seen.add(recurrenceId);
      let start,end,actual=override?new ICAL.Event(override):event,details;
      if(override)details={startDate:actual.startDate,endDate:actual.endDate};
      else {const endTime=next.clone();endTime.addDuration(event.duration);details={startDate:next,endDate:endTime};}
      try {start=fromTime(details.startDate,actual.component,root);end=!override&&!details.startDate.isDate?new Date(+new Date(start)+(new Date(e.end)-new Date(e.start))).toISOString():fromTime(details.endDate,actual.component,root,'dtend');}
      catch(error){if(error.message.includes('does not exist')&&!override)continue;throw error;}
      if(new Date(start)>=to&&!override)break;
      if(actual.component.getFirstPropertyValue('status')==='CANCELLED')continue;
      if(new Date(start)<to&&new Date(end)>from)result.push({...e,title:actual.summary||e.title,location:actual.location||'',notes:actual.description||'',allDay:details.startDate.isDate,timeZone:zoneOf(actual.component.getFirstProperty('dtstart')),start,end,startWall:wallString(details.startDate),endWall:details.startDate.isDate?wallString(details.endDate):zonedWall(end,e.endTimeZone||e.timeZone||localZone(),root),occurrenceDate:recurrenceId.slice(0,10),recurrenceId,instanceId:`${e.id}:${recurrenceId}`});
    }
    // Moved exceptions can enter the requested range even if their original date is outside it or excluded.
    for(const [recurrenceId,c] of overrides)if(!seen.has(recurrenceId)) {
      const actual=new ICAL.Event(c);if(c.getFirstPropertyValue('status')==='CANCELLED')continue;
      const start=fromTime(actual.startDate,c,root),end=fromTime(actual.endDate,c,root,'dtend');
      if(new Date(start)<to&&new Date(end)>from)result.push({...e,title:actual.summary||e.title,location:actual.location||'',notes:actual.description||'',allDay:actual.startDate.isDate,timeZone:zoneOf(c.getFirstProperty('dtstart')),start,end,startWall:wallString(actual.startDate),endWall:wallString(actual.endDate),occurrenceDate:recurrenceId.slice(0,10),recurrenceId,instanceId:`${e.id}:${recurrenceId}`});
    }
  }
  return result.sort((a,b)=>new Date(a.start)-new Date(b.start));
}
export function importICS(text,calendarId='personal') {
  if(!/^BEGIN:VCALENDAR\r?$/m.test(text)||!/^END:VCALENDAR\r?$/m.test(text))throw new Error('Choose a complete iCalendar (.ics) file.');
  const parsed=ICAL.parse(text);if(parsed[0]!=='vcalendar')throw new Error('Choose an iCalendar file.');
  for(const raw of parsed[2].filter(c=>c[0]==='vevent'))for(const p of raw[1])if(['dtstart','dtend','recurrence-id','exdate','rdate'].includes(p[0])&&p[2]!=='period')for(const value of p.slice(3)){if(typeof value==='string')instantFromWall(value.replace(/Z$/,''),'UTC');}
  const root=new ICAL.Component(parsed),groups=new Map(),events=[];
  for(const comp of root.getAllSubcomponents('vevent')){const id=comp.getFirstPropertyValue('uid')||crypto.randomUUID();if(!groups.has(id))groups.set(id,[]);groups.get(id).push(comp);}
  for(const [id,group] of groups) {
    const masters=group.filter(c=>!c.hasProperty('recurrence-id'));if(masters.length!==1)throw new Error('Each series needs one master event. Import its complete calendar export.');
    const comp=masters[0];if(comp.getFirstPropertyValue('status')==='CANCELLED')continue;
    const e=normalize(comp,root,calendarId,{uid:id});
    for(const c of group.filter(c=>c.hasProperty('recurrence-id'))) {
      if(c.getFirstProperty('recurrence-id').getParameter('range'))throw new Error('RANGE recurrence overrides are not supported yet.');
      if(c.getFirstPropertyValue('status')!=='CANCELLED')normalize(c,root,calendarId);
      else if(!c.hasProperty('dtstart')) {const rid=c.getFirstPropertyValue('recurrence-id');setTime(c,'dtstart',wallString(rid),e.timeZone,e.allDay);const end=rid.clone();end.addDuration(new ICAL.Event(comp).duration);setTime(c,'dtend',wallString(end),e.timeZone,e.allDay);}
      e.overrides.push(copy(c.toJSON()));
    }
    events.push(e);
  }
  // Validate expansion before returning anything so an import is atomic.
  for(const e of events)occurrences([e],new Date(e.start),new Date(+new Date(e.start)+32*86400000));
  return events;
}
export function exportICS(events) {
  const root=newRoot(),zones=new Map();
  for(const e of events){const source=rootFor(e);for(const zone of source.getAllSubcomponents('vtimezone')){const id=zone.getFirstPropertyValue('tzid'),serialized=zone.toString();if(zones.has(id)&&zones.get(id)!==serialized)throw new Error(`Conflicting definitions for ${id}. Export these calendars separately.`);if(!zones.has(id)){zones.set(id,serialized);root.addSubcomponent(new ICAL.Component(copy(zone.toJSON())));}}for(const comp of source.getAllSubcomponents('vevent'))root.addSubcomponent(new ICAL.Component(copy(comp.toJSON())));}
  // ICAL.js folds by characters; enforce the RFC's UTF-8 octet limit too.
  const lines=root.toString().replace(/\r?\n[ \t]/g,'').split(/\r?\n/);
  return lines.map(line=>{const chunks=[];let part='',bytes=0;for(const c of line){const n=new TextEncoder().encode(c).length;if(bytes+n>75){chunks.push(part);part=' ';bytes=1;}part+=c;bytes+=n;}chunks.push(part);return chunks.join('\r\n');}).join('\r\n')+'\r\n';
}
export function updateEvent(e,changes) {
  const updated={...e,...changes};
  if('start' in changes&&!('startWall' in changes)){delete updated.startWall;delete updated.startDate;}
  if('end' in changes&&!('endWall' in changes)){delete updated.endWall;delete updated.endDate;}
  const root=rootFor(updated);return normalize(root.getFirstSubcomponent('vevent'),root,updated.calendarId,{id:updated.id,reminder:updated.reminder,overrides:updated.overrides||[],exceptions:[]});
}
export function occurrenceFor(e,recurrenceId) {
  const root=rootFor(e),master=root.getFirstSubcomponent('vevent');
  const override=root.getAllSubcomponents('vevent').slice(1).find(c=>recurrenceKey(c,e,root)===recurrenceId);
  const comp=override||master,event=new ICAL.Event(comp);
  const start=override?event.startDate:ICAL.Time.fromString(recurrenceId+(e.timeZone==='UTC'&&!e.allDay?'Z':'')),end=override?event.endDate:start.clone();
  if(!override)end.addDuration(event.duration);
  const startISO=fromTime(start,comp,root),endISO=!override&&!start.isDate?new Date(+new Date(startISO)+(new Date(e.end)-new Date(e.start))).toISOString():fromTime(end,comp,root,'dtend');
  return {...e,title:event.summary||e.title,notes:event.description||'',location:event.location||'',allDay:start.isDate,timeZone:zoneOf(comp.getFirstProperty('dtstart')),start:startISO,end:endISO,startWall:wallString(start),endWall:start.isDate?wallString(end):zonedWall(endISO,zoneOf(comp.getFirstProperty('dtstart')),root),startDate:start.isDate?wallString(start):undefined,endDate:end.isDate?wallString(end):undefined,recurrenceId};
}
export function resolveEventTime(e,wall,zone) {return instantFromWall(wall,zone,newRoot(e?.timezones));}
export function eventWallTime(e,iso,zone=e.timeZone||localZone()){return zonedWall(iso,zone,newRoot(e?.timezones));}
export function editOccurrence(e,recurrenceId,changes,scope='one') {
  if(scope==='series')return [updateEvent(e,changes)];
  const root=rootFor(e),comp=root.getFirstSubcomponent('vevent'),zone=e.timeZone||localZone();
  if(scope==='one') {
    const overrides=(e.overrides||[]).filter(raw=>recurrenceKey(new ICAL.Component(raw),e,root)!==recurrenceId);
    const edited=updateEvent({...e,repeat:'none',rrule:'',overrides:[],exceptions:[],ical:copy(comp.toJSON())},changes);
    const child=rootFor(edited).getFirstSubcomponent('vevent');for(const p of ['rrule','rdate','exdate'])child.removeAllProperties(p);
    setTime(child,'recurrence-id',recurrenceId,zone,e.allDay);child.updatePropertyWithValue('uid',e.uid||e.id+'@daymark');
    overrides.push(copy(child.toJSON()));return [{...e,overrides}];
  }
  if(scope!=='following')throw new Error('Choose how much of the series to edit.');
  const selected=occurrenceFor(e,recurrenceId);
  changes={start:selected.start,end:selected.end,...changes};
  if(comp.hasProperty('rdate')||!comp.hasProperty('rrule'))throw new Error('This and following edits need a single recurrence rule. Edit one occurrence or the series instead.');
  const countingComp=new ICAL.Component(copy(comp.toJSON()));countingComp.removeAllProperties('exdate');
  const iterator=new ICAL.Event(countingComp).iterator();let n,before=0,steps=0;
  while((n=iterator.next())){if(++steps>100000)throw new Error('This series is too large to split.');if(wallString(n)===recurrenceId)break;before++;}
  if(!n)throw new Error('This occurrence is no longer part of the series.');
  if(before===0)return [updateEvent(e,{...changes,overrides:(e.overrides||[]).filter(raw=>recurrenceKey(new ICAL.Component(raw),e,root)!==recurrenceId)})];
  const oldRule=comp.getFirstPropertyValue('rrule').clone(),newRule=oldRule.clone(),originalCount=oldRule.count;
  oldRule.count=before;oldRule.until=null;
  if(originalCount)newRule.count=originalCount-before;
  const pastOverrides=(e.overrides||[]).filter(raw=>recurrenceKey(new ICAL.Component(raw),e,root)<recurrenceId);
  const futureOverrides=(e.overrides||[]).filter(raw=>recurrenceKey(new ICAL.Component(raw),e,root)>recurrenceId);
  const past=updateEvent(e,{rrule:oldRule.toString(),overrides:pastOverrides});
  const nextId=crypto.randomUUID(),future=updateEvent({...e,id:nextId,uid:nextId+'@daymark',overrides:futureOverrides},{...changes,rrule:changes.rrule===undefined||changes.rrule===e.rrule?newRule.toString():changes.rrule});
  // Rebase exception identities when the future series moves to a new wall-clock start.
  const wallMillis=wall=>Date.parse((wall.length===10?wall+'T00:00:00':wall)+'Z');
  const delta=wallMillis(future.startWall)-wallMillis(recurrenceId);
  const shift=wall=>new Date(wallMillis(wall)+delta).toISOString().slice(0,future.allDay?10:19);
  const futureComp=new ICAL.Component(copy(future.ical)),excluded=[];
  for(const p of futureComp.getAllProperties('exdate'))for(const t of p.getValues()){
    const original=t.isDate?wallString(t):zonedWall(instant(t,p,root),zone,root);
    if(original>=recurrenceId)excluded.push(shift(original));
  }
  futureComp.removeAllProperties('exdate');
  for(const wall of excluded){const p=new ICAL.Property('exdate');p.setValue(ICAL.Time.fromString(wall+(future.timeZone==='UTC'&&!future.allDay?'Z':'')));if(!future.allDay&&!['UTC','floating'].includes(future.timeZone))p.setParameter('tzid',future.timeZone);futureComp.addProperty(p);}
  future.ical=futureComp.toJSON();
  future.overrides=future.overrides.map(raw=>{const c=new ICAL.Component(copy(raw)),original=recurrenceKey(c,e,root);setTime(c,'recurrence-id',shift(original),future.timeZone,future.allDay);c.updatePropertyWithValue('uid',future.uid);return c.toJSON();});
  return [past,future];
}
export function skipOccurrence(e,recurrenceId) {
  const root=rootFor(e),comp=root.getFirstSubcomponent('vevent'),zone=e.timeZone||localZone();
  const overrides=(e.overrides||[]).filter(raw=>recurrenceKey(new ICAL.Component(raw),e,root)!==recurrenceId);
  const p=new ICAL.Property('exdate');const t=ICAL.Time.fromString(e.allDay?recurrenceId.slice(0,10):recurrenceId+(zone==='UTC'?'Z':''));p.setValue(t);if(!e.allDay&&!['floating','UTC'].includes(zone))p.setParameter('tzid',zone);comp.addProperty(p);
  return {...e,ical:copy(comp.toJSON()),exceptions:[],overrides};
}
