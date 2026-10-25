// Reproducible adaptations of pinned ReXGlue 0.10.0 for the dual-API build.
const fs = require('node:fs');
const path = require('node:path');
const root = path.join(__dirname, 'tools/rexglue-source/rexglue-sdk-0.10.0');
// Restored pass7 word-owned downscale source and bytecode.
for (const [source,target] of [
 ['resolve_downscale.cs.hlsl','resolve_downscale.cs.hlsl'],
 ['resolve_downscale.cs.glsl','resolve_downscale.cs.glsl'],
 ['resolve_downscale_d3d12.h','bytecode/d3d12_5_1/resolve_downscale_cs.h'],
 ['resolve_downscale_vulkan.h','vulkan_spirv/resolve_downscale_cs.h']]) {
 const data=fs.readFileSync(path.join(__dirname,'native/shaders',source));
 const dest=path.join(root,'src/graphics/shaders',target);
 if(!data.equals(fs.readFileSync(dest)))fs.writeFileSync(dest,data);
}
const paintPolicy = fs.readFileSync(path.join(__dirname, 'native/src/paint_coalescing.h'));
const paintPolicyTarget = path.join(root, 'src/ui/fifa08_paint_coalescing.h');
if (!fs.existsSync(paintPolicyTarget) || !paintPolicy.equals(fs.readFileSync(paintPolicyTarget)))
  fs.writeFileSync(paintPolicyTarget, paintPolicy);
const readbackPolicy = fs.readFileSync(path.join(__dirname, 'native/src/readback_copy_policy.h'));
const readbackPolicyTarget = path.join(root, 'src/graphics/fifa08_readback_copy.h');
if (!fs.existsSync(readbackPolicyTarget) || !readbackPolicy.equals(fs.readFileSync(readbackPolicyTarget)))
  fs.writeFileSync(readbackPolicyTarget, readbackPolicy);
// All command-processor submissions use graphics/compute queue 0. Waiting
// for its latest required fence preserves all earlier dependencies.
replace('src/graphics/vulkan/command_processor.cpp',
`    // Await in a blocking way if requested.
    // TODO(Triang3l): Await only one fence. "Fence signal operations that are
    // defined by vkQueueSubmit additionally include in the first
    // synchronization scope all commands that occur earlier in submission
    // order."
    VkResult wait_result =
        dfn.vkWaitForFences(device, uint32_t(await_submission - submission_completed_),
                            submissions_in_flight_fences_.data(), VK_TRUE, UINT64_MAX);`,
`    // Every tracked submission uses graphics/compute queue 0. The requested
    // submission's fence also covers all earlier commands and fence signals
    // in that queue's submission order (Vulkan synchronization specification).
    // Preserve the dependency, but avoid passing every preceding fence to
    // the driver on the measured frame-reuse wait path.
    const size_t await_count = size_t(await_submission - submission_completed_);
    if (await_count > fences_total) {
      REXGPU_ERROR("Vulkan submission fence range is inconsistent");
      return;
    }
    VkResult wait_result = dfn.vkWaitForFences(
        device, 1, &submissions_in_flight_fences_[await_count - 1], VK_TRUE, UINT64_MAX);`);
function replace(file, before, after) {
  const target = path.join(root, file);
  const text = fs.readFileSync(target, 'utf8').replaceAll('\r\n', '\n');
  if (text.includes(after)) return;
  if (!text.includes(before)) throw new Error(`Unexpected SDK source: ${file}`);
  fs.writeFileSync(target, text.replace(before, after));
}
// Archive dependencies carry exact gitlink revisions, recorded by the bootstrap.
replace('thirdparty/CMakeLists.txt',
  'if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/${submodule}/.git")',
  'if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/${submodule}/.git" AND NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/${submodule}/.archive-revision")');
// Keep the same tested bit-level helpers as the packaged SDK used by FIFA 08.
for (const name of ['types.h', 'ppc/intrinsics.h']) {
  const contents = fs.readFileSync(path.join(__dirname, 'tools/rexglue-0.10.0/win-amd64/include/rex', name));
  const target = path.join(root, 'include/rex', name);
  if (!contents.equals(fs.readFileSync(target))) fs.writeFileSync(target, contents);
}
// MSVC 2019 lacks C++23 expected. Use the maintained, licensed implementation
// throughout this source build, never injecting substitutes into namespace std.
for (const file of ['include/rex/result.h','include/rex/system/map_parser.h','src/system/map_parser.cpp']) {
  const target = path.join(root, file);
  const original = fs.readFileSync(target, 'utf8');
  const text = original.replaceAll('#include <expected>', '#include <tl/expected.hpp>')
    .replaceAll('std::expected', 'tl::expected').replaceAll('std::unexpected(', 'tl::make_unexpected(');
  if (text !== original) fs.writeFileSync(target, text);
}
replace('CMakeLists.txt', 'include_directories(${REXGLUE_ROOT}/include)',
  'include_directories(${REXGLUE_ROOT}/include ${REXGLUE_ROOT}/thirdparty/tl-expected/include)');
replace('src/system/CMakeLists.txt', '$<BUILD_INTERFACE:${REXGLUE_ROOT}/include>',
  '$<BUILD_INTERFACE:${REXGLUE_ROOT}/include>\n    $<BUILD_INTERFACE:${REXGLUE_ROOT}/thirdparty/tl-expected/include>');
// Official DirectX headers provide OPTIONS8 with the installed Windows SDK.
replace('CMakeLists.txt', 'add_subdirectory(thirdparty)',
  'include_directories(BEFORE SYSTEM "${REXGLUE_ROOT}/../../directx-headers/DirectX-Headers-1.614.1/include/directx")\nadd_subdirectory(thirdparty)');
// ReStreet v1.2: allow a paced D3D12 swap chain to avoid gating the shared
// graphics queue on display refresh during readback fence waits.
// Other presenter patches can insert declarations between this flag and the
// namespace. Test the declaration itself to keep repeated builds idempotent.
{
 const filename=path.join(root,'src/ui/d3d12/d3d12_presenter.cpp');
 let text=fs.readFileSync(filename,'utf8');
 const declaration='REXCVAR_DEFINE_BOOL(d3d12_paced_present_tearing, true, "UI/D3D12",\n    "Allow paced presentation without throttling the shared graphics queue");';
 const matches=text.split(declaration);
 if(matches.length>2)text=matches[0]+declaration+matches.slice(1).join('');
 else if(matches.length===1)text=text.replace('namespace rex::ui::d3d12 {',declaration+'\n\nnamespace rex::ui::d3d12 {');
 if(text!==fs.readFileSync(filename,'utf8'))fs.writeFileSync(filename,text);
}
replace('src/ui/d3d12/d3d12_presenter.cpp',
  'if (REXCVAR_GET(d3d12_allow_variable_refresh_rate_and_tearing) && dxgi_supports_tearing_) {',
  'if ((REXCVAR_GET(d3d12_allow_variable_refresh_rate_and_tearing) ||\n'
  + '         REXCVAR_GET(d3d12_paced_present_tearing)) && dxgi_supports_tearing_) {');
// Preserve the original completion requirements and check actual HRESULTs.
replace('src/graphics/d3d12/command_processor.cpp',
  'SUCCEEDED(direct_queue->Signal(queue_operations_since_submission_fence_, fence_value) &&\n'
  + '                    SUCCEEDED(queue_operations_since_submission_fence_->SetEventOnCompletion(\n'
  + '                        fence_value, fence_completion_event_)))',
  'SUCCEEDED(direct_queue->Signal(queue_operations_since_submission_fence_, fence_value)) &&\n'
  + '          SUCCEEDED(queue_operations_since_submission_fence_->SetEventOnCompletion(\n'
  + '              fence_value, fence_completion_event_))');
console.log('Prepared pinned SDK: Vulkan + D3D12, paced presenter and checked fence HRESULTs.');
if (!fs.readFileSync(path.join(root, 'src/ui/d3d12/d3d12_presenter.cpp'), 'utf8').includes('FIFA08_NONBLOCKING_PRESENT_V1'))
replace('src/ui/d3d12/d3d12_presenter.cpp',
  '  HRESULT present_result = paint_context_.swap_chain->Present(\n      0, DXGI_PRESENT_RESTART |\n             (paint_context_.swap_chain_allows_tearing ? DXGI_PRESENT_ALLOW_TEARING : 0));',
  '  const bool presentation_vsync = REXCVAR_QUERY(bool, vsync);\n'
  + '  HRESULT present_result = paint_context_.swap_chain->Present(\n'
  + '      presentation_vsync ? 1 : 0, presentation_vsync ? 0 : (DXGI_PRESENT_RESTART |\n'
  + '             (paint_context_.swap_chain_allows_tearing ? DXGI_PRESENT_ALLOW_TEARING : 0)));');
const meter = fs.readFileSync(path.join(__dirname, 'native/src/gpu_frame_meter.h'));
// The SDK's stock unlocked worker signals vblank at 1000 Hz with vsync off.
// Match the tested Xenia configuration's capped guest clock, independently
// of immediate host presentation, so original game intervals remain useful.
replace('src/graphics/graphics_system.cpp',
  'REXCVAR_GET(vsync) ? vsync_interval_ticks : no_vsync_interval_ticks;',
  'vsync_interval_ticks;  // FIFA08: guest clock stays at configured video refresh');
const meterTarget = path.join(root, 'include/rex/graphics/fifa08_frame_meter.h');
if (!fs.existsSync(meterTarget) || !meter.equals(fs.readFileSync(meterTarget))) fs.writeFileSync(meterTarget, meter);
for (const api of ['d3d12', 'vulkan']) {
  const file = `src/graphics/${api}/command_processor.cpp`;
  replace(file, '#include <rex/logging.h>',
    '#include <rex/logging.h>\n#include <rex/graphics/fifa08_frame_meter.h>');
  replace(file, '        EndSubmission(true);\n        return true;\n      });',
    '        EndSubmission(true);\n        fifa08::gpu_meter::Record();\n        return true;\n      });');
}
replace('src/graphics/plugin_main.cpp', '#include <rex/logging.h>',
  '#include <rex/logging.h>\n#include <rex/graphics/fifa08_frame_meter.h>');
replace('src/graphics/plugin_main.cpp', 'extern "C" REX_GPU_PLUGIN_EXPORT uint32_t rex_gpu_abi_version(void) {',
  'extern "C" REX_GPU_PLUGIN_EXPORT uint64_t Fifa08GpuSubmittedImages() {\n'
  + '  return fifa08::gpu_meter::submitted.load(std::memory_order_acquire);\n}\n'
  + 'extern "C" REX_GPU_PLUGIN_EXPORT uint32_t Fifa08GpuFrameIntervalUs(uint32_t index) {\n'
  + '  return index < 240 ? fifa08::gpu_meter::intervals[index].load(std::memory_order_relaxed) : 0;\n}\n\n'
  + 'extern "C" REX_GPU_PLUGIN_EXPORT uint32_t rex_gpu_abi_version(void) {');
