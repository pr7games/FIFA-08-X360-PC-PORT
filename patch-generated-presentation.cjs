// Checked, reproducible changes to the game's render setup and swap interval.
const fs = require('node:fs'), path = require('node:path');
const root = path.join(__dirname, 'native/generated/default');
const updates = new Map();
function change(file, name, transform, declaration) {
  const filename = path.join(root, file);
  let source = updates.get(filename) ?? fs.readFileSync(filename, 'utf8');
  const start = source.indexOf(`DEFINE_REX_FUNC(${name}) {`);
  const end = source.indexOf('\nDEFINE_REX_FUNC(', start + 1);
  if (start < 0 || end < 0) throw new Error(`Missing ${name}`);
  const body = source.slice(start, end).replace(/\r\n/g, '\n');
  const corrected = transform(body);
  if (corrected === body) return;
  source = source.slice(0, start) + declaration + '\n' + corrected + source.slice(end);
  updates.set(filename, source);
}
change('fifa08_recomp.77.cpp', 'sub_826BCC58', body => {
  if (body.includes('fifa08_select_interval(ctx);')) return body;
  const marker = '\tREX_FUNC_PROLOGUE();\n';
  if (body.split(marker).length !== 2 || !body.includes('REX_STORE_U32(ctx.r3.u32 + 116, ctx.r11.u32);'))
    throw new Error('Unexpected presentation interval setter');
  return body.replace(marker, marker + '\tfifa08_select_interval(ctx);\n');
}, 'extern void fifa08_select_interval(PPCContext&);');
change('fifa08_recomp.188.cpp', 'sub_8260DD90', body => {
  if (body.includes('fifa08_scene_state(ctx, base);')) return body;
  const marker = '\tctx.r28.u64 = REX_LOAD_U8(ctx.r11.u32 + 4);';
  if (body.split(marker).length !== 2) throw new Error('Unexpected scene flags');
  return body.replace(marker, marker + '\n\tfifa08_scene_state(ctx, base);');
}, 'extern void fifa08_scene_state(PPCContext&, uint8_t*);');
change('fifa08_recomp.188.cpp', 'sub_8260DD90', body => {
  if (body.includes('fifa08_effect_layer_test(ctx, base);')) return body;
  const marker = 'loc_8260DF34:\n';
  if (body.split(marker).length !== 2) throw new Error('Unexpected effect-layer update exit');
  return body.replace(marker, marker + '\tfifa08_effect_layer_test(ctx, base);\n');
}, 'extern void fifa08_effect_layer_test(const PPCContext&, uint8_t*);');
change('fifa08_recomp.188.cpp', 'sub_8260DD90', body => {
  if (body.includes('// Bypass 30 Hz cutscene interval')) return body;
  const target = 'loc_8260DED0:\n\t// lbz r11,346(r24)\n\tctx.r11.u64 = REX_LOAD_U8(ctx.r24.u32 + 346);\n\t// cmplwi cr6,r11,1\n\tctx.cr6.compare<uint32_t>(ctx.r11.u32, 1, ctx.xer);\n\t// bne cr6,0x8260df18\n\tif (!ctx.cr6.eq) goto loc_8260DF18;\n\t// lis r11,-31995\n\tctx.r11.s64 = -2096824320;\n\t// li r4,2\n\tctx.r4.s64 = 2;\n\t// lwz r3,228(r11)\n\tctx.r3.u64 = REX_LOAD_U32(ctx.r11.u32 + 228);\n\t// bl 0x826bcc58\n\tctx.lr = 0x8260DEEC;\n\tsub_826BCC58(ctx, base);\n\t// li r11,0\n\tctx.r11.s64 = 0;\n\t// b 0x8260df14\n\tgoto loc_8260DF14;';
  if (!body.includes(target)) throw new Error('Unexpected loc_8260DED0 sequence');
  return body.replace(target, 'loc_8260DED0:\n\t// Bypass 30 Hz cutscene interval and simulation mode\n\tgoto loc_8260DEF4;');
}, '// Uncap cutscenes/replays');

change('fifa08_recomp.197.cpp', 'sub_826C5BF8', body => {
  if (body.includes('fifa08_dimension(')) return body;
  const markers = ['ctx.r10.s64 = 720;', 'ctx.r11.s64 = 1280;'];
  for (const marker of markers) if (body.split(marker).length !== 2)
    throw new Error('Unexpected renderer viewport initializer');
  return body.replace(markers[0], 'ctx.r10.s64 = fifa08_dimension(720, 540);')
    .replace(markers[1], 'ctx.r11.s64 = fifa08_dimension(1280, 960);');
}, 'extern int fifa08_dimension(int, int);');
change('fifa08_recomp.121.cpp', 'sub_826C03D8', body => {
  if (body.includes('fifa08_dimension(')) return body;
  // HDTV tiling has two horizontal slices for one format and three vertical
  // slices for the other. Scale their rectangles together with the viewport.
  const values = {640:480, 720:540, 736:544, 1280:960, 256:192, 512:384};
  let count = 0;
  body = body.replace(/ctx\.(r\d+)\.s64 = (640|720|736|1280|256|512);/g,
    (_, reg, value) => { count++; return `ctx.${reg}.s64 = fifa08_dimension(${value}, ${values[value]});`; });
  if (count !== 7) throw new Error(`Expected 7 tiling dimensions, got ${count}`);
  return body;
}, 'extern int fifa08_dimension(int, int);');
change('fifa08_recomp.55.cpp', 'sub_8227EA48', body => {
  if (body.includes('fifa08_render_throttle_hz(40)')) return body;
  const marker = '\t// li r4,40\n\tctx.r4.s64 = 40;';
  if (body.split(marker).length !== 2 || !body.includes('sub_82276D50(ctx, base);'))
    throw new Error('Unexpected engine render throttle');
  return body.replace(marker, '\t// li r4,40: original engine render-thread throttle\n\tctx.r4.s64 = fifa08_render_throttle_hz(40);');
}, 'extern int fifa08_render_throttle_hz(int);');
change('fifa08_recomp.16.cpp', 'sub_82276D50', body => {
  if (body.includes('fifa08_render_throttle_hz(ctx.r4.s32)')) return body;
  const marker = '\t// extsw r8,r4\n\tctx.r8.s64 = ctx.r4.s32;';
  if (body.split(marker).length !== 2) throw new Error('Unexpected deadline throttle setup');
  return body.replace(marker, '\tctx.r4.s64 = fifa08_render_throttle_hz(ctx.r4.s32);\n' + marker);
}, 'extern int fifa08_render_throttle_hz(int);');

change('fifa08_recomp.175.cpp', 'sub_8248B998', body => {
  if (body.includes('fifa08_scale_radar(ctx, base);')) return body;
  const width='ctx.r4.u64 = ctx.r11.u32 & 0xFFFF;';
  const height='ctx.r3.u64 = ctx.r9.u32 & 0xFFFF;';
  const exit='loc_8248C250:\n';
  for (const marker of [width,height,exit]) if(body.split(marker).length!==2)
    throw new Error('Unexpected radar layout initializer');
  return body.replace(width,'ctx.r4.u64 = fifa08_radar_dimension(ctx.r11.u32 & 0xFFFF, false);')
    .replace(height,'ctx.r3.u64 = fifa08_radar_dimension(ctx.r9.u32 & 0xFFFF, true);')
    .replace(exit,exit+'\tfifa08_scale_radar(ctx, base);\n');
}, 'extern int fifa08_radar_dimension(int, bool);\nextern void fifa08_scale_radar(PPCContext&, uint8_t*);');

for (const [file, source] of updates) fs.writeFileSync(file, source);
console.log(`FIFA08 presentation: ${updates.size} generated files updated.`);
