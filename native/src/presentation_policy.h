#pragma once
#include <cstdint>

// The rendering thread must not wait for the next guest vblank for
// interval-1 frames: missing that deadline quantizes a 60 Hz scene to 30/20.
// The host limiter already supplies the selected cap for immediate frames.
// Cutscenes and replays also use immediate presentation for this FPS test.
// All scenes remain subject to the frame limit selected in the launcher.
constexpr uint32_t fifa08_apply_present_interval(uint32_t requested, bool /*vulkan*/) {
  return requested == 1 || requested == 2 ? 0 : requested;
}
static_assert(fifa08_apply_present_interval(1, true) == 0);
static_assert(fifa08_apply_present_interval(2, true) == 0);
static_assert(fifa08_apply_present_interval(0, true) == 0);
static_assert(fifa08_apply_present_interval(1, false) == 0);
static_assert(fifa08_apply_present_interval(2, false) == 0);
static_assert(fifa08_apply_present_interval(3, true) == 3);
static_assert(fifa08_apply_present_interval(0x80000000u, false) == 0x80000000u);
