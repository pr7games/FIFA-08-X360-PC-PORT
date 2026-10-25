// Apply the local STL compatibility patch to the pinned downloaded SDK.
const fs = require('node:fs');
const path = require('node:path');
const target = path.join(__dirname, 'tools/rexglue-0.10.0/win-amd64/include/rex/types.h');
const original = '    return std::bit_cast<T>(std::byteswap(std::bit_cast<uint_t>(value)));';
const patched = `#if defined(__cpp_lib_byteswap) && __cpp_lib_byteswap >= 202110L
${original}
#else
    // Local compatibility fallback for the installed MSVC 2019 STL.
    // Unsigned byte operations preserve float/integer bit patterns alike.
    uint_t input = std::bit_cast<uint_t>(value);
    uint_t output = 0;
    for (size_t i = 0; i < sizeof(T); ++i) {
      output = uint_t((output << 8) | (input & 0xFFu));
      input >>= 8;
    }
    return std::bit_cast<T>(output);
#endif`;
const text = fs.readFileSync(target, 'utf8');
if (!text.includes('Local compatibility fallback for the installed MSVC 2019 STL.')) {
  if (!text.includes(original)) throw new Error('SDK types.h changed; inspect before patching');
  fs.writeFileSync(target, text.replace(original, patched));
  console.log('Applied local byte_swap compatibility patch.');
}

// The recompiler reverses all 16 vector bytes. PPC byte 15 (the least
// significant byte selected by VSL) is consequently host byte 0, not 15.
// Keep this reproducible and reject a different SDK implementation.
const intrinsics = path.join(__dirname, 'tools/rexglue-0.10.0/win-amd64/include/rex/ppc/intrinsics.h');
const shiftOriginal = '  int shift = simde_mm_extract_epi8(b, 15) & 0x7;  // Get low 3 bits from byte 15 (BE: byte 0)';
const shiftPatched = '  int shift = simde_mm_extract_epi8(b, 0) & 0x7;  // FIFA08 fix: PPC byte 15 maps to host byte 0';
const vectorText = fs.readFileSync(intrinsics, 'utf8');
if (!vectorText.includes(shiftPatched)) {
  if (!vectorText.includes(shiftOriginal)) throw new Error('SDK VSL helper changed; inspect before patching');
  fs.writeFileSync(intrinsics, vectorText.replace(shiftOriginal, shiftPatched));
  console.log('Applied VSL byte-order correction.');
}
