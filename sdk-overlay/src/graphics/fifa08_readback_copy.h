#pragma once
#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstring>

namespace fifa08 {
// Readback buffers and guest RAM are distinct, already synchronized by the
// caller. Preserve the exact bytes while avoiding writes to unchanged guest
// pages (which may invalidate GPU caches / trigger memory-write tracking).
inline size_t CopyChangedReadback(void* destination, const void* source, size_t size) {
  auto* dst = static_cast<uint8_t*>(destination);
  auto* src = static_cast<const uint8_t*>(source);
  size_t written = 0;
  while (size) {
    const size_t page_remaining = 4096 - (reinterpret_cast<uintptr_t>(dst) & 4095);
    const size_t chunk = std::min(page_remaining, size);
    if (std::memcmp(dst, src, chunk) != 0) {
      std::memcpy(dst, src, chunk);
      written += chunk;
    }
    dst += chunk;
    src += chunk;
    size -= chunk;
  }
  return written;
}
}
