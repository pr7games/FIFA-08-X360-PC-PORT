// Fan Shop ownership query used by boots, accessories and kit selectors.
// Preserve the original query and override only its boolean result. No save edits.
const fs = require('node:fs'), path = require('node:path');
const filename = path.join(__dirname, 'native/generated/default/fifa08_recomp.181.cpp');
const source = fs.readFileSync(filename, 'utf8');
const start = source.indexOf('DEFINE_REX_FUNC(sub_824890D8) {');
const end = source.indexOf('\nDEFINE_REX_FUNC(', start + 1);
if (start < 0 || end < 0) throw new Error('Missing Fan Shop ownership query');
const body = source.slice(start, end).replace(/\r\n/g, '\n');
const call = '\tfifa08_unlock_fanshop_result(ctx);\n';
if (!body.includes(call.trim())) {
  for (const marker of ['ctx.r10.s64 = 4;', 'ctx.r11.s64 = ctx.r11.s64 + 1796;',
      'ctx.r11.s64 = ctx.r11.s64 + 3672;', 'ctx.r9.u64 = REX_LOAD_U8(ctx.r9.u32 + ctx.r4.u32);'])
    if (body.split(marker).length !== 2) throw new Error('Unexpected ownership query: ' + marker);
  const exit = '\t// blr \n\treturn;';
  if (body.split(exit).length !== 2) throw new Error('Unexpected ownership query exit');
  fs.writeFileSync(filename, source.slice(0, start) +
    'extern void fifa08_unlock_fanshop_result(PPCContext&);\n' +
    body.replace(exit, call + exit) + source.slice(end));
  console.log('FIFA08 Fan Shop unlock override applied.');
} else console.log('FIFA08 Fan Shop unlock override already applied.');
