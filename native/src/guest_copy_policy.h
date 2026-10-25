#pragma once
#include <bit>
#include <cstdint>
#include <cstring>

#ifndef REX_RAW_ADDR
#error Guest copies must use the runtime's guest-to-host address mapping.
#endif

// The crowd sorter swaps four-byte references through the guest memcpy.
// Accelerate the original short-copy path, including its observable PPC state.
// Overlap and special mappings retain the original forward byte accesses.
template<class Context>
inline bool fifa08_copy_short(Context& ctx, uint8_t* base) {
  const uint32_t count = ctx.r5.u32;
  const uint64_t dst = ctx.r3.u32, src = ctx.r4.u32;
  if (!count || count >= 64 || src < 65536 || dst < 65536 ||
      (src >> 29) != (dst >> 29) ||
      src + count > 0x100000000ull || dst + count > 0x100000000ull ||
      (src < dst + count && dst < src + count) ||
      (src >> 29) != ((src + count - 1) >> 29) ||
      (dst >> 29) != ((dst + count - 1) >> 29) ||
      (src < 0x80000000ull && src + count > 0x7fc00000ull) ||
      (dst < 0x80000000ull && dst + count > 0x7fc00000ull)) return false;
  auto* source = REX_RAW_ADDR(static_cast<uint32_t>(src));
  auto* destination = REX_RAW_ADDR(static_cast<uint32_t>(dst));
  const uint8_t final_byte = source[count - 1];
  std::memcpy(destination, source, count);
  ctx.r10.u64 = ctx.r3.u64 - ctx.r4.u64;
  ctx.r11.u64 = ctx.r4.u64 + count;
  ctx.r9.u64 = final_byte;
  ctx.r5.s64 -= count;
  ctx.xer.ca = true;
  ctx.cr0.template compare<int32_t>(ctx.r5.s32, 0, ctx.xer);
  // The short path's last CR6 compare is against zero, before the loop.
  ctx.cr6.template compare<uint32_t>(count, 0, ctx.xer);
  return true;
}

// Replace only complete iterations of the original aligned 16-byte loop.
// Leave its prologue, tail, CR comparisons and XER updates in translated code.
template<class Context>
inline bool fifa08_copy_aligned_bulk(Context& ctx, uint8_t* base) {
  const uint32_t count = ctx.r5.u32 & ~15u;
  const uint64_t dst = ctx.r10.u32, src = ctx.r11.u32;
  if (count < 64 || count > 64u * 1024u * 1024u ||
      src < 65536 || dst < 65536 || (src >> 29) != (dst >> 29) ||
      src + count > 0x100000000ull || dst + count > 0x100000000ull ||
      (src < dst + count && dst < src + count)) return false;
  // The host mapping is not linear over every guest address boundary. Keep
  // the translated per-access path for copies crossing a 512 MiB region.
  if ((src >> 29) != ((src + count - 1) >> 29) ||
      (dst >> 29) != ((dst + count - 1) >> 29)) return false;
  // Keep the original accesses for the guest MMIO aperture.
  if ((src < 0x80000000ull && src + count > 0x7fc00000ull) ||
      (dst < 0x80000000ull && dst + count > 0x7fc00000ull)) return false;
  uint32_t final_word;
  // Windows maps guest E0000000..FFFFFFFF with an additional host 4 KiB
  // offset. base+guest silently accesses the preceding bytes in that window.
  auto* source = REX_RAW_ADDR(static_cast<uint32_t>(src));
  auto* destination = REX_RAW_ADDR(static_cast<uint32_t>(dst));
  std::memcpy(&final_word, source + count - 4, 4);
  std::memcpy(destination, source, count);
  ctx.r9.u64 = __builtin_bswap32(final_word);
  ctx.r5.s64 -= count;
  ctx.r10.s64 += count;
  ctx.r11.s64 += count;
  return true;
}
