#include "generated/default/fifa08_pch.h"
#include <cstddef>
#include <cstdint>
#include <limits>
#include <windows.h>
#include <mmsystem.h>
#include <cstdio>
#include <mutex>
#include "frame_meter.h"
#include "presentation_policy.h"
#include "host_frame_pacer.h"
#include "effect_layer_policy.h"
#include <chrono>
#include <thread>

namespace {
// Diagnostic counters never publish game state. Once saturated, avoid an
// atomic read-modify-write on every subsequent call while retaining eight logs.
bool ShouldLogFirstEight(std::atomic<uint32_t>& counter) {
  auto seen = counter.load(std::memory_order_relaxed);
  while (seen < 8) {
    if (counter.compare_exchange_weak(seen, seen + 1, std::memory_order_relaxed))
      return true;
  }
  return false;
}

bool UsesVulkan() {
  static const bool enabled = [] {
    const char* api = std::getenv("FIFA08_GRAPHICS_API");
    return api && (_stricmp(api, "Vulkan") == 0 || _stricmp(api, "VK") == 0);
  }();
  return enabled;
}
uint32_t AppliedPresentInterval(uint32_t requested) {
  static const std::string mode = [] {
    const char* value = std::getenv("FIFA08_PACING");
    return std::string(value ? value : "guest");
  }();
  if (mode == "original") return requested;
  return 0; // Uncap all present intervals (immediate presentation for gameplay, cutscenes and replays)
}
struct TimingLog {
  std::mutex mutex;
  std::FILE* file = nullptr;
  bool opened = false;
  bool stopped = false;
  bool timer_period = false;
  char buffer[65536]{};
  uint64_t frames = 0;
};
TimingLog timing_log;
void RecordTiming(uint32_t interval_us, uint32_t work_us, uint32_t wait_us, uint32_t swap_sync_us) {
  const uint64_t count = fifa08_frame_submissions.load(std::memory_order_relaxed);
  fifa08_frame_intervals_us[count % fifa08_frame_intervals_us.size()].store(interval_us,
      std::memory_order_relaxed);
  fifa08_last_work_us.store(work_us, std::memory_order_relaxed);
  fifa08_last_limit_wait_us.store(wait_us, std::memory_order_relaxed);
  std::lock_guard lock(timing_log.mutex);
  if (timing_log.stopped) return;
  if (!timing_log.opened) {
    timing_log.opened = true;
    if (const wchar_t* filename = _wgetenv(L"FIFA08_FRAME_LOG")) {
      timing_log.file = _wfopen(filename, L"w");
      if (timing_log.file) {
        std::setvbuf(timing_log.file, timing_log.buffer, _IOFBF, sizeof(timing_log.buffer));
        std::fputs("frame,interval_us,work_and_sync_us,limiter_wait_us,width,height,target_fps,requested_interval,scene_flags,qpc,qpc_frequency,swap_sync_us\n", timing_log.file);
      }
    }
  }
  if (timing_log.file) {
    LARGE_INTEGER qpc, frequency;
    QueryPerformanceCounter(&qpc);
    QueryPerformanceFrequency(&frequency);
    std::fprintf(timing_log.file, "%llu,%u,%u,%u,%u,%u,%u,%u,%u,%lld,%lld,%u\n",
        static_cast<unsigned long long>(count + 1), interval_us, work_us, wait_us,
        fifa08_frontbuffer_width.load() * fifa08_render_scale.load(),
        fifa08_frontbuffer_height.load() * fifa08_render_scale.load(),
        fifa08_target_fps.load(), fifa08_requested_interval.load(),
        fifa08_scene_flags.load(), qpc.QuadPart, frequency.QuadPart, swap_sync_us);
    if (++timing_log.frames % 120 == 0) std::fflush(timing_log.file);
  }
}
}  // namespace

void fifa08_start_timing() {
  // The runtime's guest-vblank worker uses Sleep(1). Request 1 ms granularity
  // for this process while the game is running instead of depending on RTSS.
  timing_log.timer_period = timeBeginPeriod(1) == TIMERR_NOERROR;
  REXLOG_INFO("FIFA08 timing: Windows 1ms timer={}, pacing={}; guest clock unchanged",
      timing_log.timer_period, "launcher cap for gameplay, cutscenes and replays");
}

// Explicitly requested experiment, not a proven presentation-cap removal.
// Change only the loaded image, before the first guest thread is launched.
// The shared constant also participates in animation/simulation calculations.
void fifa08_apply_engine_experiments(uint8_t* base) {
  if (const char* value = std::getenv("FIFA08_EXPERIMENTAL_60HZ");
      !value || std::string_view(value) != "1") {
    REXLOG_INFO("FIFA08 original shared animation constant retained; presentation unlock unchanged");
    return;
  }
  constexpr uint32_t address = 0x82098A18u;
  constexpr uint32_t original = 0x3D088889u;
  constexpr uint32_t replacement = 0x3C888889u;
  if (REX_LOAD_U32(address) != original) {
    throw std::runtime_error("FIFA08: unexpected 1/30 image constant; experiment refused");
  }
  DWORD previous_protection;
  if (!VirtualProtect(base + address, sizeof(uint32_t), PAGE_READWRITE, &previous_protection)) {
    throw std::runtime_error("FIFA08: cannot apply requested 1/30 image experiment");
  }
  REX_STORE_U32(address, replacement);
  DWORD ignored;
  if (!VirtualProtect(base + address, sizeof(uint32_t), previous_protection, &ignored)) {
    throw std::runtime_error("FIFA08: cannot restore image protection after experiment");
  }
  REXLOG_INFO("FIFA08: shared 1/30 constant at 82098A18 changed to 1/60 for unlocked simulation");
}
void fifa08_stop_timing() {
  std::lock_guard lock(timing_log.mutex);
  timing_log.stopped = true;
  if (timing_log.file) {
    std::fclose(timing_log.file);
    timing_log.file = nullptr;
  }
  if (timing_log.timer_period) {
    timeEndPeriod(1);
    timing_log.timer_period = false;
  }
}

void fifa08_trace_crowd_config(const PPCContext& ctx) {
  static std::atomic<uint32_t> calls{0};
  if (ShouldLogFirstEight(calls))
    REXLOG_INFO("FIFA08 crowd decision: DISABLE_CROWD={}", ctx.r3.u32);
}

void fifa08_trace_crowd_setup(const PPCContext& ctx, uint8_t* base) {
  static std::atomic<uint32_t> calls{0};
  if (ShouldLogFirstEight(calls))
    REXLOG_INFO("FIFA08 crowd setup: object={:08X}, success={}, groups={}",
        ctx.r30.u32, ctx.r3.u32, REX_LOAD_U32(ctx.r30.u32 + 12));
}

int fifa08_render_throttle_hz(int original) {
  if (original != 40) return original;
  static const int limit = [] {
    const char* mode = std::getenv("FIFA08_PACING");
    if (mode && std::string_view(mode) == "original") return -1;
    const char* value = std::getenv("FIFA08_FPS_LIMIT");
    if (!value) return 60;
    return std::atoi(value);
  }();
  if (limit < 0) return original;
  const int applied = (limit <= 0) ? std::numeric_limits<int>::max() : limit;
  static std::atomic<uint32_t> calls{0};
  if (ShouldLogFirstEight(calls))
    REXLOG_INFO("FIFA08 engine render throttle reached: original={}Hz, applied={}Hz, callsite=8227EAD0", original, applied);
  return applied;
}

void fifa08_pace_submission() {
  static const int configured_limit = [] {
    const char* text = std::getenv("FIFA08_FPS_LIMIT");
    if (!text) return 60;
    return std::atoi(text);
  }();
  if (configured_limit <= 0) {
    fifa08_target_fps.store(0, std::memory_order_relaxed);
    return;
  }
  const int limit = configured_limit;
  fifa08_target_fps.store(limit, std::memory_order_relaxed);
  static Fifa08HostFramePacer pacer;
  pacer.Wait(limit);
}

static std::pair<int,int> fifa08_guest_size() {
  static const auto size=[]{
    const char* w=std::getenv("FIFA08_GUEST_WIDTH");
    const char* h=std::getenv("FIFA08_GUEST_HEIGHT");
    int width=w?std::atoi(w):0,height=h?std::atoi(h):0;
    if(width>=320&&width<=1280&&width%64==0&&height==width*9/16)
      return std::pair<int,int>{width,height};
    const char* legacy=std::getenv("FIFA08_NATIVE_1080");
    return legacy&&std::string_view(legacy)=="1"?std::pair<int,int>{960,540}:std::pair<int,int>{1280,720};
  }();
  return size;
}
int fifa08_dimension(int original, int native1080) {
  const auto [w,h]=fifa08_guest_size();
  switch(original){
    case 1280:return w;
    case 640:return w/2;
    case 720:return h;
    case 736:return (h+31)&~31;
    case 256:return ((h*256/720+31)/32)*32;
    case 512:return 2*(((h*256/720+31)/32)*32);
    default:return original;
  }
}

void fifa08_scene_state(PPCContext& ctx, uint8_t* base) {
  // Scene-controller booleans are authoritative. r30 is a virtual getter
  // (vtable +28 -> sub_82279C48: byte at object+34), not a GPU query.
  const uint32_t flags = (uint32_t(ctx.r25.u8 != 0) << 0) |
      (uint32_t(ctx.r26.u8 != 0) << 1) | (uint32_t(ctx.r27.u8 != 0) << 2) |
      (uint32_t(ctx.r28.u8 != 0) << 3) | (uint32_t(ctx.r29.u8 != 0) << 4) |
      (uint32_t(ctx.r30.u8 != 0) << 5);
  if (fifa08_scene_flags.exchange(flags, std::memory_order_relaxed) != flags) {
    using Observe=void(*)(uint32_t,uint64_t);
    static auto observe=[]()->Observe {auto module=GetModuleHandleW(L"rexgpu-xenosrd.dll");return module?reinterpret_cast<Observe>(GetProcAddress(module,"Fifa08ObservedScene")):nullptr;}();
    if(observe)observe(flags,fifa08_frame_submissions.load(std::memory_order_relaxed));
    fifa08_pacing_generation.fetch_add(1, std::memory_order_relaxed);
    for (auto& sample : fifa08_frame_intervals_us) sample.store(0, std::memory_order_relaxed);
  }
  static uint32_t previous = UINT32_MAX;
  if (flags != previous) {
    previous = flags;
    REXLOG_INFO("FIFA08 scene flags: mask={:02X}, r25={}, r26={}, r27={}, r28={}, r29={}, r30={}, vtable={:08X}",
        flags, ctx.r25.u8, ctx.r26.u8, ctx.r27.u8, ctx.r28.u8, ctx.r29.u8, ctx.r30.u8,
        REX_LOAD_U32(ctx.r31.u32));
  }
}

void fifa08_effect_layer_test(const PPCContext& ctx, uint8_t* base) {
  static const auto mode = [] {
    const char* value = std::getenv("FIFA08_EFFECT_TEST");
    return fifa08::ParseEffectLayerTest(value ? value : "original");
  }();
  // At this exact callsite r24 is the scene controller and r30 points to
  // the layer-manager singleton. The original constructor resolves the names
  // "glares" and "vbeams" into the masks at offsets 360 and 368 respectively.
  const uint32_t manager = REX_LOAD_U32(ctx.r30.u32);
  if (!manager) return;
  const uint64_t glares = REX_LOAD_U64(ctx.r24.u32 + 360);
  const uint64_t vbeams = REX_LOAD_U64(ctx.r24.u32 + 368);
  const uint64_t original = REX_LOAD_U64(manager + 32);
  const uint64_t applied = fifa08::ApplyEffectLayerTest(original, glares, vbeams, mode);
  // Default mode is observation only. Never persist a diagnostic to assets,
  // player data, launcher settings, or the original executable image.
  if (applied != original) REX_STORE_U64(manager + 32, applied);
  const uint32_t state = (original & glares ? 1u : 0u) | (original & vbeams ? 2u : 0u);
  static uint32_t previous = UINT32_MAX;
  if (state != previous) {
    previous = state;
    REXLOG_INFO("FIFA08 effect layers: test={} original_glares={} original_vbeams={} applied_glares={} applied_vbeams={} glares_mask={:016X} vbeams_mask={:016X}",
        unsigned(mode), bool(original & glares), bool(original & vbeams),
        bool(applied & glares), bool(applied & vbeams), glares, vbeams);
  }
}

void fifa08_select_interval(PPCContext& ctx) {
  static const int fps = [] {
    const char* value = std::getenv("FIFA08_FPS_LIMIT");
    if (!value) return 60;
    return std::atoi(value);
  }();
  // Interval-1 frames use the host cap for either API, avoiding missed vblanks.
  const uint32_t requested = ctx.r4.u32;
  if (requested > 2) return; // Preserve the original setter's invalid-input path.
  const uint32_t applied = AppliedPresentInterval(requested);
  if (applied != requested) ctx.r4.u64 = applied;
  if (fifa08_requested_interval.exchange(requested) != requested)
    fifa08_pacing_generation.fetch_add(1, std::memory_order_relaxed);
  static std::atomic<uint32_t> previous{UINT32_MAX};
  if (previous.exchange(requested) != requested)
    REXLOG_INFO("FIFA08 presentation interval: requested={}, applied={}; scene mask={:02X}, global cap={}",
        requested, applied, fifa08_scene_flags.load(), fps);
}

// sub_826BC9E8 constructs the game's D3D presentation parameters. The original
// ignores the display dimensions and selects 1280x720 in its HDTV path.
// Called at its common exit, before the caller allocates render resources.
void fifa08_configure_render(PPCContext& ctx, uint8_t* base) {
  const uint32_t parameters = ctx.r31.u32;
  const uint32_t initial = REX_LOAD_U32(parameters + 116);
  const uint32_t applied = AppliedPresentInterval(initial);
  if (applied != initial) REX_STORE_U32(parameters + 116, 0x80000000u);
  fifa08_requested_interval.store(initial == 0x80000000u ? 0 : initial);
  fifa08_scene_flags.store(0);
  fifa08_pacing_generation.fetch_add(1);
  // Dimensions are independent of the interval policy and graphics API.
  const auto [guest_w,guest_h]=fifa08_guest_size();
  if(guest_w==1280&&guest_h==720)return;
  if (REX_LOAD_U32(parameters + 68) != 1280 ||
      REX_LOAD_U32(parameters + 72) != 720 ||
      REX_LOAD_U32(parameters + 80) != 1280 ||
      REX_LOAD_U32(parameters + 84) != 720) {
    REX_FATAL("FIFA08: unexpected presentation parameters for native 1080p");
    return;
  }
  // BackBufferWidth/Height and FrontBufferWidth/Height. Keep AA, formats,
  // presentation interval and the remaining original parameters unchanged.
  // Rasterize at exactly 1920x1080 using a 960x540 guest grid and GPU scale 2.
  // The backend scales geometry, viewports and render targets before drawing;
  // this is not a post-render resize of a 540p image. Keep guest EDRAM usage
  // below the console limit. Renderer viewport and tiling are patched too.
  REX_STORE_U32(parameters + 68, guest_w);
  REX_STORE_U32(parameters + 72, guest_h);
  REX_STORE_U32(parameters + 80, guest_w);
  REX_STORE_U32(parameters + 84, guest_h);
  REXLOG_INFO("FIFA08 render parameters: guest={}x{}",guest_w,guest_h);
}

// Forward to the pinned runtime's unchanged implementation. Defining this
// import in the executable lets both direct calls and registered guest calls
// count submissions without modifying the generated files or the runtime DLL.
extern "C" REX_FUNC(__imp__VdSwap) {
  using Swap = void (*)(PPCContext&, uint8_t*);
  static const Swap original = [] {
    if (HMODULE module = GetModuleHandleW(L"rexruntime.dll")) {
      if (auto proc = GetProcAddress(module, "__imp__VdSwap"))
        return reinterpret_cast<Swap>(proc);
    }
    REX_FATAL("FIFA08 frame meter: runtime VdSwap export unavailable");
    return static_cast<Swap>(nullptr);
  }();
  // Read the actual front-buffer dimensions, not the window dimensions.
  // The PPC ABI puts arguments 9 and 10 at SP+0x54 and SP+0x5C.
  const uint32_t width = REX_LOAD_U32(REX_LOAD_U32(ctx.r1.u32 + 0x54));
  const uint32_t height = REX_LOAD_U32(REX_LOAD_U32(ctx.r1.u32 + 0x5C));
  if (fifa08_frontbuffer_width.exchange(width) != width ||
      fifa08_frontbuffer_height.load() != height) {
    REXLOG_INFO("FIFA08 actual frontbuffer: {}x{}; GPU scale={}", width, height,
        fifa08_render_scale.load());
  }
  fifa08_frontbuffer_height.store(height);
  using Clock = std::chrono::steady_clock;
  static thread_local Clock::time_point previous{};
  const auto before_limit = Clock::now();
  fifa08_pace_submission();
  const auto after_limit = Clock::now();
  original(ctx, base);
  const auto complete = Clock::now();
  if (previous != Clock::time_point{}) {
    const auto us = [](auto duration) {
      return uint32_t(std::min<int64_t>(UINT32_MAX,
          std::chrono::duration_cast<std::chrono::microseconds>(duration).count()));
    };
    // Include runtime swap synchronization in work, not just engine work
    // before the cap. The old log omitted this part of the frame interval.
    RecordTiming(us(complete - previous),
        us((before_limit - previous) + (complete - after_limit)),
        us(after_limit - before_limit), us(complete - after_limit));
  }
  previous = complete;
  fifa08_frame_submissions.fetch_add(1, std::memory_order_relaxed);
}

// fanshopitems.fanshopid is 0..155 in this game's metadata. The original
// sub_824890D8 ORs ownership across four profiles. Its callers resolve an
// existing item/category first, then use this result to enable selection.
// Keep the profile bytes untouched, so disabling the override restores the
// genuine earned/purchased status without changing the player's progress.
void fifa08_unlock_fanshop_result(PPCContext& ctx) {
  static const bool enabled = [] {
    const char* value = std::getenv("FIFA08_UNLOCK_CONTENT");
    return !value || std::string_view(value) != "0";
  }();
  if (!enabled || ctx.r4.u32 > 155) return;
  const uint32_t original = ctx.r3.u32;
  ctx.r3.u64 = 1;
  static std::atomic<uint32_t> reports{0};
  if (ShouldLogFirstEight(reports))
    REXLOG_INFO("FIFA08 Fan Shop unlock: item={}, original={}, available=1",
        ctx.r4.u32, original);
}

// Xenia's XUsbcamGetState_entry defines zero as "not connected".
// FIFA can then take its normal path without the optional Xbox USB camera.
extern "C" REX_FUNC(__imp__XUsbcamGetState) {
  ctx.r3.u64 = 0;
}

// No camera configuration is implemented. Return a failure rather than
// claiming that a physical device has been configured successfully.
extern "C" REX_FUNC(__imp__XUsbcamSetConfig) {
  REXLOG_WARN("XUsbcamSetConfig: no USB camera available in this experimental port");
  ctx.r3.u64 = 0xC000009Du;  // STATUS_DEVICE_NOT_CONNECTED
}

// The pinned SDK's spdlog was built with newer MSVC string algorithms.
// These scalar implementations provide the same byte-position ABI while
// retaining the installed 2019 STL. No system CRT files are modified.
// Signatures and npos semantics: microsoft/STL stl/src/vector_algorithms.cpp.
extern "C" size_t __stdcall __std_find_first_of_trivial_pos_1(
    const void* haystack, size_t length, const void* needles, size_t count) noexcept {
  const auto* bytes = static_cast<const uint8_t*>(haystack);
  const auto* set = static_cast<const uint8_t*>(needles);
  for (size_t i = 0; i < length; ++i)
    for (size_t j = 0; j < count; ++j)
      if (bytes[i] == set[j]) return i;
  return std::numeric_limits<size_t>::max();
}

extern "C" size_t __stdcall __std_find_last_of_trivial_pos_1(
    const void* haystack, size_t length, const void* needles, size_t count) noexcept {
  const auto* bytes = static_cast<const uint8_t*>(haystack);
  const auto* set = static_cast<const uint8_t*>(needles);
  for (size_t i = length; i > 0; --i)
    for (size_t j = 0; j < count; ++j)
      if (bytes[i - 1] == set[j]) return i - 1;
  return std::numeric_limits<size_t>::max();
}
