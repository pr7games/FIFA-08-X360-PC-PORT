// Semantic correction for the pinned SDK's in-place FLOAT16_4 VPKD3D emitter.
// Keep the input register alive until all reads, including sign bits, finish.
// Applied after codegen; never suppresses unresolved branches or fatal checks.
const fs = require('node:fs'), path = require('node:path');
const root = path.join(__dirname, 'native/generated/default');
let instructions = 0, changedFiles = 0;
const updates = [];
for (const file of fs.readdirSync(root).filter(n => /^fifa08_recomp\.\d+\.cpp$/.test(n))) {
  const filename = path.join(root, file);
  const original = fs.readFileSync(filename, 'utf8');
  const patched = original.replace(
    /(\t\/\/ vpkd3d128 (v\d+),\2,5,2,2\r?\n)([\s\S]*?)(?=\t\/\/|\nloc_|\n})/g,
    (match, comment, register, body) => {
      instructions++;
      if (body.includes('fifa08_pack_source')) return match;
      const source = `ctx.${register}.u32[`;
      const reads = body.split(source).length - 1;
      if (reads !== 16 || (body.match(/\.u16\[\d\] = /g) || []).length !== 4)
        throw new Error(`${file}: unexpected FLOAT16_4 emitter; inspect before changing`);
      const corrected = body.split(source).join('fifa08_pack_source.u32[');
      return comment + `\t{ // FIFA08: preserve VPKD3D input when destination aliases it\n` +
          `\tconst auto fifa08_pack_source = ctx.${register};\n` + corrected + '\t}\n';
    });
  if (patched !== original) updates.push([filename, patched]);
}
if (instructions !== 9) throw new Error(`Expected 9 in-place FLOAT16_4 instructions, found ${instructions}; inspect codegen`);
// Validate every affected body before writing any file.
for (const [filename, contents] of updates) { fs.writeFileSync(filename, contents); changedFiles++; }
console.log(`VMX FLOAT16_4: ${instructions} instructions inspected; ${changedFiles} files corrected.`);
