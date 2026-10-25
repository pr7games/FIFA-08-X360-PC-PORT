/**
 * @file        graphics/plugin_main.cpp
 * @brief       rexgpu-xenos plugin entry points
 *
 * @copyright   Copyright (c) 2026 Tom Clay <tomc@tctechstuff.com>
 *              All rights reserved.
 *
 * @license     BSD 3-Clause License
 *              See LICENSE file in the project root for full license text.
 */

#include <string_view>

#include <rex/logging.h>
#include <rex/graphics/fifa08_frame_meter.h>
#include <rex/graphics/fifa08_guest_frame_diagnostics.h>
#include <rex/system/gpu_plugin.h>

#if REX_HAS_D3D12
#include <rex/graphics/d3d12/graphics_system.h>
#endif
#if REX_HAS_VULKAN
#include <rex/graphics/vulkan/graphics_system.h>
#endif

extern "C" REX_GPU_PLUGIN_EXPORT void Fifa08RequestGuestCapture() { fifa08::diagnostics::capture_requested.store(true); }
extern "C" REX_GPU_PLUGIN_EXPORT void Fifa08ObservedScene(uint32_t flags, uint64_t submission) { fifa08::diagnostics::scene.store(flags); fifa08::diagnostics::cpu_submission.store(submission); }

extern "C" REX_GPU_PLUGIN_EXPORT uint64_t Fifa08GpuSubmittedImages() {
  return fifa08::gpu_meter::submitted.load(std::memory_order_acquire);
}
extern "C" REX_GPU_PLUGIN_EXPORT uint32_t Fifa08GpuFrameIntervalUs(uint32_t index) {
  return index < 240 ? fifa08::gpu_meter::intervals[index].load(std::memory_order_relaxed) : 0;
}

extern "C" REX_GPU_PLUGIN_EXPORT uint32_t rex_gpu_abi_version(void) {
  return rex::system::kGpuPluginAbiVersion;
}

extern "C" REX_GPU_PLUGIN_EXPORT rex::system::IGraphicsSystem* rex_gpu_create(
    uint32_t abi_version, const rex::system::GpuCreateInfo* info) {
  if (abi_version != rex::system::kGpuPluginAbiVersion) {
    REXLOG_ERROR("rexgpu-xenos: host requested ABI {}, plugin is ABI {}", abi_version,
                 rex::system::kGpuPluginAbiVersion);
    return nullptr;
  }
  if (!info || info->struct_size < sizeof(rex::system::GpuCreateInfo)) {
    REXLOG_ERROR("rexgpu-xenos: invalid GpuCreateInfo");
    return nullptr;
  }

  std::string_view backend = info->backend ? info->backend : "any";
#if REX_HAS_D3D12
  if (backend == "any" || backend == "d3d12") {
    return new rex::graphics::d3d12::D3D12GraphicsSystem();
  }
#endif
#if REX_HAS_VULKAN
  if (backend == "any" || backend == "vulkan") {
    return new rex::graphics::vulkan::VulkanGraphicsSystem();
  }
#endif
  REXLOG_ERROR("rexgpu-xenos: requested backend '{}' is not compiled into this plugin", backend);
  return nullptr;
}

#include <rex/graphics/native_guest_renderer.h>

extern "C" REX_GPU_PLUGIN_EXPORT void RexSetNativeGuestOutputRenderer(
    rex::graphics::NativeGuestOutputRenderer renderer, void* user_data) {
  rex::graphics::SetNativeGuestOutputRenderer(renderer, user_data);
}
extern "C" REX_GPU_PLUGIN_EXPORT bool RexIsNativeGuestOutputActive() {
  return rex::graphics::IsNativeGuestOutputActive();
}
