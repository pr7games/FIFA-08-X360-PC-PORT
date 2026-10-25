// Observe game decisions without forcing success or changing crowd data.
const fs = require('node:fs'), path = require('node:path');
const root = path.join(__dirname, 'native/generated/default');
const changes = [];
for (const [file, name, marker, call, declaration] of [
  ['fifa08_recomp.146.cpp', 'sub_826AD770',
    '\tctx.lr = 0x826AD7A4;\n\tsub_82287F08(ctx, base);\n',
    '\tfifa08_trace_crowd_config(ctx);\n', 'extern void fifa08_trace_crowd_config(const PPCContext&);'],
  ['fifa08_recomp.24.cpp', 'sub_825F3848', 'loc_825F3D30:\n',
    '\tfifa08_trace_crowd_setup(ctx, base);\n', 'extern void fifa08_trace_crowd_setup(const PPCContext&, uint8_t*);'],
]) {
  const filename = path.join(root, file), source = fs.readFileSync(filename, 'utf8');
  const start = source.indexOf(`DEFINE_REX_FUNC(${name}) {`);
  const end = source.indexOf('\nDEFINE_REX_FUNC(', start + 1);
  if (start < 0 || end < 0) throw new Error(`Missing ${name}`);
  const body = source.slice(start, end).replace(/\r\n/g, '\n');
  if (body.includes(call.trim())) continue;
  if (body.split(marker).length !== 2) throw new Error(`Unexpected trace point in ${name}`);
  changes.push([filename, source.slice(0, start) + declaration + '\n' +
    body.replace(marker, marker + call) + source.slice(end)]);
}
for (const [file, source] of changes) fs.writeFileSync(file, source);
console.log(`FIFA08 crowd observation: ${changes.length} files updated.`);
