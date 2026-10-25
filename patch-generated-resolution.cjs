// Apply after codegen. Patch the presentation parameters before resource creation,
// not the final window or an already allocated texture.
const fs = require('node:fs'), path = require('node:path');
const filename = path.join(__dirname, 'native/generated/default/fifa08_recomp.71.cpp');
const original = fs.readFileSync(filename, 'utf8');
if (original.includes('fifa08_configure_render(ctx, base);')) {
  console.log('FIFA08 render parameters: already patched.');
} else {
  const start = original.indexOf('DEFINE_REX_FUNC(sub_826BC9E8) {');
  const end = original.indexOf('\nDEFINE_REX_FUNC(', start + 1);
  if (start < 0 || end < 0) throw new Error('Missing render parameter initializer');
  let body = original.slice(start, end);
  const marker = 'loc_826BCB3C:\n';
  // Normalize CRLF only within this function to make the checked insertion stable.
  body = body.replace(/\r\n/g, '\n');
  if (body.split(marker).length !== 2 || !body.includes('REX_STORE_U32(ctx.r31.u32 + 84, ctx.r11.u32);'))
    throw new Error('Render initializer changed; inspect before patching');
  body = body.replace(marker, marker + '\tfifa08_configure_render(ctx, base);\n');
  const declaration = 'extern void fifa08_configure_render(PPCContext&, uint8_t*);\n';
  // PCH definitions must precede this declaration.
  const prefix = original.slice(0, start);
  fs.writeFileSync(filename, prefix + declaration + body + original.slice(end));
  console.log('FIFA08 render parameters: initializer patched.');
}
