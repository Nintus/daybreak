import test from 'node:test';
import assert from 'node:assert/strict';
import {planImport} from '../src/import-plan.mjs';
test('preview deduplicates within destination calendar and within the file',()=>{const input=[{uid:'a',title:'A'},{uid:'b',title:'B'},{uid:'b',title:'Another B'}],existing=[{uid:'a',calendarId:'work'},{uid:'b',calendarId:'personal'}];const plan=planImport(input,existing,'work');assert.equal(plan.events.length,1);assert.equal(plan.duplicates,2);assert.equal(plan.events[0].calendarId,'work');assert.equal(input[1].calendarId,undefined);});
