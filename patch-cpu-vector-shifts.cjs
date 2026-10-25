// Preserve the byte mapping of VSLO while avoiding variable-size stack copies.
// Patch the pinned SDK input; prepare-sdk-source.cjs mirrors it to the source SDK.
const fs = require('node:fs');
const path = require('node:path');
const filename = path.join(__dirname, 'tools/rexglue-0.10.0/win-amd64/include/rex/ppc/intrinsics.h');
const original = fs.readFileSync(filename, 'utf8');
const start = original.indexOf('inline simde__m128i simde_mm_vslo(');
const end = original.indexOf('// Vector Shift Right by Octet', start);
if (start < 0 || end < 0) throw new Error('Pinned SDK VSLO helper not found');
const body = original.slice(start, end);
const before = `  alignas(16) uint8_t src[16], dst[16];
  simde_mm_store_si128((simde__m128i*)src, a);
  memset(dst, 0, sizeof(dst));
  memcpy(dst + shift_bytes, src, 16 - shift_bytes);
  return simde_mm_load_si128((simde__m128i*)dst);`;
const after = `  // FIFA08 VSLO: PSHUFB zeros lanes whose mask byte has bit 7 set.
  // For shift s in [0,15], output i is zero if i<s, otherwise input i-s.
  alignas(16) static constexpr uint8_t masks[32] = {
      128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,128,
      0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15};
  const auto mask = simde_mm_loadu_si128(
      reinterpret_cast<const simde__m128i*>(masks + 16 - shift_bytes));
  return simde_mm_shuffle_epi8(a, mask);`;
if (!body.includes(after)) {
  if (body.split(before).length !== 2) throw new Error('Unexpected VSLO implementation');
  fs.writeFileSync(filename, original.slice(0, start) + body.replace(before, after) + original.slice(end));
}
console.log('VSLO byte mapping preserved; x86 temporary copies replaced by SIMD shuffle.');
