import fs from 'node:fs/promises';
const target=new URL('../src/vendor/',import.meta.url);
await fs.mkdir(target,{recursive:true});
for(const [source,dest] of [['dist/ical.js','ical.js'],['LICENSE','ICAL-LICENSE.txt']])await fs.copyFile(new URL('../node_modules/ical.js/'+source,import.meta.url),new URL(dest,target));
process.stdout.write('Vendored ICAL.js and its license. Keep the upstream file unmodified.\n');
