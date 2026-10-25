// Retain the guest's spin-loop conditions and memory accesses. Translate each
// consecutive group of discarded PPC delay hints into one host CPU pause.
const fs = require('node:fs'), path = require('node:path');
const root = path.join(__dirname, 'native/generated/default');
const updates = [];
let hints = 0, groups = 0;
for (const file of fs.readdirSync(root).filter(n => /^fifa08_recomp\.\d+\.cpp$/.test(n))) {
  const filename = path.join(root, file);
  const original = fs.readFileSync(filename, 'utf8');
  let text = original.replace(/(?:\t\/\/ db16cyc[^\S\r\n]*\r?\n)+/g, (block, offset) => {
    hints += (block.match(/db16cyc/g) || []).length;
    groups++;
    if (original.slice(offset + block.length).startsWith('\tfifa08_cpu_relax();')) return block;
    return block + '\tfifa08_cpu_relax();\n';
  });
  if (text.includes('\tfifa08_cpu_relax();') && !text.includes('#include "cpu_wait_policy.h"')) {
    text = text.replace(/^(#include "fifa08_funcs\.\d+\.h"\r?\n)/, '$1#include "cpu_wait_policy.h"\n');
    if (!text.includes('#include "cpu_wait_policy.h"')) throw new Error(`Unexpected header: ${file}`);
  }
  if (text !== original) updates.push([filename, text]);
}
if (hints !== 12 || groups !== 5) throw new Error(`Unexpected hint inventory: ${hints} hints, ${groups} groups`);
for (const [filename, text] of updates) fs.writeFileSync(filename, text);
console.log(`CPU waits: ${hints} PPC hints -> ${groups} host pause sites; ${updates.length} files updated.`);

// CPU sampling identified this deadline wait as consuming almost one core.
// Keep the original comparison and all register updates, yielding only while
// the already-computed deadline is still in the future.
const deadlineFile = path.join(root, 'fifa08_recomp.16.cpp');
let deadlineText = fs.readFileSync(deadlineFile, 'utf8');
const deadlineOriginal = deadlineText;
const anchor = '\t// blt cr6,0x82276d8c\n\tif (ctx.cr6.lt) goto loc_82276D8C;';
const patched = '\t// blt cr6,0x82276d8c\n\tif (ctx.cr6.lt) {\n' +
  '\t\tfifa08_wait_guest_deadline(ctx.r29.u64 - ctx.r11.u64);\n' +
  '\t\tgoto loc_82276D8C;\n\t}';
deadlineText = deadlineText.replace(/\r\n/g, '\n');
const oldPatched = '\t// blt cr6,0x82276d8c\n\tif (ctx.cr6.lt) {\n' +
  '\t\tif (!fifa08_is_uncapped()) {\n' +
  '\t\t\tfifa08_wait_guest_deadline(ctx.r29.u64 - ctx.r11.u64);\n' +
  '\t\t\tgoto loc_82276D8C;\n' +
  '\t\t}\n\t}';
if (deadlineText.includes(oldPatched)) {
  deadlineText = deadlineText.replace(oldPatched, patched);
} else if (!deadlineText.includes(patched)) {
  if (deadlineText.split(anchor).length !== 2) throw new Error('Deadline wait anchor mismatch');
  deadlineText = deadlineText.replace(anchor, patched);
}
if (!deadlineText.includes('#include "guest_deadline_wait.h"'))
  deadlineText = deadlineText.replace('#include "fifa08_funcs.16.h"',
    '#include "fifa08_funcs.16.h"\n#include "guest_deadline_wait.h"');
if (deadlineText !== deadlineOriginal) fs.writeFileSync(deadlineFile, deadlineText);
console.log('Measured guest deadline polling: bounded host sleeps, original deadline unchanged.');

const gpuFile = path.join(root, 'fifa08_recomp.103.cpp');
const gpuOriginal = fs.readFileSync(gpuFile, 'utf8');
let gpuText = gpuOriginal.replace(/\r\n/g, '\n');
const gpuAnchor = '\t// beq cr6,0x82264480\n\tif (ctx.cr6.eq) goto loc_82264480;';
const gpuPatched = '\t// beq cr6,0x82264480\n\tif (ctx.cr6.eq) {\n' +
  '\t\tfifa08_gpu_poll_yield(ctx.r11.u32, ctx.r10.u32);\n\t\tgoto loc_82264480;\n\t}';
gpuText = gpuText.replace('fifa08_gpu_poll_yield();',
  'fifa08_gpu_poll_yield(ctx.r11.u32, ctx.r10.u32);');
if (!gpuText.includes(gpuPatched)) {
  if (gpuText.split(gpuAnchor).length !== 2) throw new Error('GPU progress poll anchor mismatch');
  gpuText = gpuText.replace(gpuAnchor, gpuPatched);
}
if (gpuText !== gpuOriginal) fs.writeFileSync(gpuFile, gpuText);
console.log('Measured unchanged GPU progress: periodic scheduler yield; all guest conditions retained.');
