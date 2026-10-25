#pragma once

#include <cstdint>
#include <rex/graphics/native_guest_renderer.h>

namespace fifa08::native_render {

// Install and register the native renderer callback with the GPU plugin
void Install();

// Query whether native renderer is active
bool IsActive();

// The main native scene rendering entry point called on each present swap
bool RenderScene(const rex::graphics::NativeGuestOutputRenderContext& context, void* user_data);

}  // namespace fifa08::native_render
