#pragma once
#include <dxgi1_5.h>
namespace fifa08 {
// A busy swap chain must not pin the queue while the guest tries to submit its
// next frame. The caller schedules a later UI tick instead of spinning here.
template<class Present>
HRESULT PresentWithoutQueueStall(bool vsync, bool tearing, bool& supported,
                                Present&& present) {
  const UINT interval = vsync ? 1u : 0u;
  const UINT base_flags = vsync ? 0u : DXGI_PRESENT_RESTART |
      (tearing ? DXGI_PRESENT_ALLOW_TEARING : 0u);
  // VSync sets the display interval, not whether the caller may block.
  // Keeping DO_NOT_WAIT with interval 1 avoids pinning the presentation thread
  // in driver waits that can also serialize guest queue submissions.
  const bool nonblocking = supported;
  HRESULT result = present(interval, base_flags |
      (nonblocking ? DXGI_PRESENT_DO_NOT_WAIT : 0u));
  // Unsupported flag combinations retain the prior working path. Busy is
  // deliberately NOT a fallback condition: retrying it here would stall again.
  if (nonblocking && result == DXGI_ERROR_INVALID_CALL) {
    supported = false;
    result = present(interval, base_flags);
  }
  return result;
}
constexpr bool PresentNeedsRetry(HRESULT result) {
  return result == DXGI_ERROR_WAS_STILL_DRAWING;
}
}
