export function planImport(incoming,existing,calendarId) {
  const uids=new Set(existing.filter(e=>e.calendarId===calendarId).map(e=>e.uid).filter(Boolean));
  const events=[];let duplicates=0;
  for(const event of incoming){if(uids.has(event.uid)){duplicates++;continue;}uids.add(event.uid);events.push({...event,calendarId});}
  return {events,duplicates,total:incoming.length};
}
