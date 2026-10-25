#include "fifa08_native_render.h"

#include <rex/cvar.h>
#include <rex/logging.h>
#include <rex/graphics/native_rhi.h>

#include <atomic>
#include <chrono>

#if defined(_WIN32)
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#endif

REXCVAR_DEFINE_BOOL(fifa08_native_render, true, "FIFA 08",
                    "Enable the FIFA 08 native Direct3D 12 / Vulkan renderer")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

REXCVAR_DEFINE_INT32(fifa08_native_render_mode, 0, "FIFA 08",
                     "0 = passthrough to emulated Xenos, 1 = native RHI direct frame, 2 = scene pipeline")
    .range(0, 2)
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

REXCVAR_DEFINE_BOOL(fifa08_native_render_suppress_emulated, false, "FIFA 08",
                    "Skip emulated Xenos EDRAM resolves and draws when native renderer presents")
    .lifecycle(rex::cvar::Lifecycle::kHotReload);

namespace fifa08::native_render {
namespace {

std::atomic<bool> g_installed{false};
std::atomic<uint64_t> g_native_frames{0};
std::atomic<uint64_t> g_fallback_frames{0};

}  // namespace

bool IsActive() {
  return REXCVAR_GET(fifa08_native_render) && g_installed.load(std::memory_order_relaxed);
}

bool RenderScene(const rex::graphics::NativeGuestOutputRenderContext& context, void* user_data) {
  if (!REXCVAR_GET(fifa08_native_render)) {
    g_fallback_frames.fetch_add(1, std::memory_order_relaxed);
    return false;
  }

  const int32_t mode = REXCVAR_GET(fifa08_native_render_mode);
  if (mode == 0) {
    // Mode 0: Seamless passthrough to emulated Xenos frame
    g_fallback_frames.fetch_add(1, std::memory_order_relaxed);
    return false;
  }

  if (!context.cmd || !context.guest_output) {
    g_fallback_frames.fetch_add(1, std::memory_order_relaxed);
    return false;
  }

  // Mode 1: Direct native RHI presentation (Direct3D 12 / Vulkan hardware path)
  // Transition guest_output to render target
  context.cmd->Barrier(context.guest_output,
                       rex::graphics::nrhi::ResourceState::kGuestOutput,
                       rex::graphics::nrhi::ResourceState::kRenderTarget);
  context.cmd->FlushBarriers();

  // Clear or composite native frame
  const float clear_color[4] = { 0.05f, 0.25f, 0.08f, 1.0f }; // FIFA pitch green
  context.cmd->ClearRenderTarget(context.guest_output, clear_color);

  // Transition back to guest output state for presenter
  context.cmd->Barrier(context.guest_output,
                       rex::graphics::nrhi::ResourceState::kRenderTarget,
                       rex::graphics::nrhi::ResourceState::kGuestOutput);
  context.cmd->FlushBarriers();

  const uint64_t frame = g_native_frames.fetch_add(1, std::memory_order_relaxed);
  if (frame == 0 || (frame % 300) == 0) {
    REXLOG_INFO("FIFA 08 native renderer: rendered frame {} via RHI ({}x{})",
                frame, context.guest_output_width, context.guest_output_height);
  }

  return true;
}

void Install() {
  // The scene pipeline is not implemented: modes 1/2 only clear the image.
  // Keep the complete game renderer and avoid native-RHI setup per swap.
  if (!REXCVAR_GET(fifa08_native_render) || REXCVAR_GET(fifa08_native_render_mode) == 0) {
    REXLOG_INFO("FIFA 08 renderer: Xenos passthrough; native RHI callback not installed");
    return;
  }
  if (g_installed.exchange(true)) {
    return;
  }

#if defined(_WIN32)
  HMODULE hGpu = GetModuleHandleA("rexgpu-xenosrd.dll");
  if (!hGpu) {
    hGpu = GetModuleHandleA("rexgpu-xenos.dll");
  }

  if (hGpu) {
    using SetRendererFn = void (*)(rex::graphics::NativeGuestOutputRenderer, void*);
    auto set_fn = reinterpret_cast<SetRendererFn>(
        GetProcAddress(hGpu, "RexSetNativeGuestOutputRenderer"));
    if (set_fn) {
      set_fn(&RenderScene, nullptr);
      REXLOG_INFO("FIFA 08 native renderer: registered with GPU backend plugin");
    } else {
      REXLOG_WARN("FIFA 08 native renderer: RexSetNativeGuestOutputRenderer not exported");
    }
  } else {
    REXLOG_WARN("FIFA 08 native renderer: GPU plugin module not found in process");
  }
#endif
}

}  // namespace fifa08::native_render
