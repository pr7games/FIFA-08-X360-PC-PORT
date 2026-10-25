"""Idempotent, anchored SDK adaptations. No game assets or clocks touched."""
from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[1]
sdk=root/'tools/rexglue-source/rexglue-sdk-0.10.0'
def edit(name,marker,action):
 p=sdk/name;s=p.read_text(encoding='utf-8-sig')
 if marker in s:return
 result=action(s)
 if result==s:raise RuntimeError('Patch did not change '+name)
 p.write_text(result,encoding='utf-8')
def replace(s,a,b):
 if s.count(a)!=1:raise RuntimeError('Anchor count '+str(s.count(a))+': '+a[:100])
 return s.replace(a,b,1)
for name in ('graphics_safety_policy.h','guest_frame_diagnostics.h'):
 target=sdk/'include/rex/graphics'/('fifa08_'+name)
 data=(root/'native/src'/name).read_bytes()
 if not target.exists() or target.read_bytes()!=data:target.write_bytes(data)
def common(s):
 s=replace(s,'#include <rex/cvar.h>','#include <rex/cvar.h>\n#include <rex/graphics/fifa08_graphics_safety_policy.h>\n#include <rex/graphics/fifa08_guest_frame_diagnostics.h>')
 s=replace(s,'REXCVAR_DEFINE_BOOL(gpu_log_sync_costs,', 'REXCVAR_DEFINE_BOOL(fifa08_resolve_visibility_fix, true, "GPU", "Execute copies during visibility pixel kill");\n\nREXCVAR_DEFINE_BOOL(gpu_log_sync_costs,')
 s=replace(s,'    if (!(viz_query.viz_query_ena && viz_query.kill_pix_post_hi_z)) {','    // FIFA08_VISIBILITY_COPY_V1: a resolve does not shade pixels.\n    bool copy = register_file_->Get<reg::RB_MODECONTROL>().edram_mode == xenos::EdramMode::kCopy;\n    bool fix = REXCVAR_GET(fifa08_resolve_visibility_fix);\n    if(copy && viz_query.viz_query_ena && viz_query.kill_pix_post_hi_z && fix) fifa08::diagnostics::QueryCopy();\n    if (fifa08::ShouldExecuteVisibilityDraw(viz_query.viz_query_ena, viz_query.kill_pix_post_hi_z, copy, fix)) {')
 return s
edit('src/graphics/command_processor.cpp','FIFA08_VISIBILITY_COPY_V1',common)
def ownership(s):
 s=replace(s,'#include <rex/cvar.h>','#include <rex/cvar.h>\n#include <rex/graphics/fifa08_graphics_safety_policy.h>\n#include <rex/graphics/fifa08_guest_frame_diagnostics.h>')
 s=replace(s,'REXCVAR_DEFINE_BOOL(direct_host_resolve,','REXCVAR_DEFINE_BOOL(edram_ownership_no_wrap, false, "GPU", "Clamp wrapped EDRAM ownership (opt-in)");\n\nREXCVAR_DEFINE_BOOL(direct_host_resolve,')
 anchor='                                       rt_base);'
 s=replace(s,anchor,anchor+'\n    // FIFA08_EDRAM_NO_WRAP_V1: opt-in; preserve the stable default.\n    uint32_t clamped = fifa08::ClampEdramOwnership(rt_base, rt_lengths_tiles[i], xenos::kEdramTileCount, REXCVAR_GET(edram_ownership_no_wrap));\n    if(clamped != rt_lengths_tiles[i]) fifa08::diagnostics::Clamp();\n    rt_lengths_tiles[i] = clamped;')
 return s
edit('src/graphics/pipeline/render_target/cache.cpp','FIFA08_EDRAM_NO_WRAP_V1',ownership)
for api,cls in [('d3d12','D3D12'),('vulkan','Vulkan')]:
 def backend(s,api=api,cls=cls):
  s=replace(s,'#include <rex/graphics/fifa08_frame_meter.h>','#include <rex/graphics/fifa08_frame_meter.h>\n#include <rex/graphics/fifa08_guest_frame_diagnostics.h>')
  # A frame boundary on every exit, including skipped swaps.
  start=s.index('void '+cls+'CommandProcessor::IssueSwap(');brace=s.index('{',start)+1
  device='GetD3D12Provider().GetDevice()' if api=='d3d12' else 'reinterpret_cast<void*>(GetVulkanDevice()->vulkan_instance()->instance())'
  s=s[:brace]+'\n  // FIFA08_GUEST_FRAME_DIAGNOSTICS_V1\n  fifa08::diagnostics::Swap diagnostic_swap("'+api+'", uint32_t(register_file_->GetTextureFetch(0).base_address)<<12, frontbuffer_width, frontbuffer_height, '+device+');'+s[brace:]
  s=replace(s,'  bool surface_pitch_is_zero =','  fifa08::diagnostics::Scope draw_diagnostic(fifa08::diagnostics::Kind::Draw);\n  bool surface_pitch_is_zero =')
  start=s.index('bool '+cls+'CommandProcessor::IssueCopy()');brace=s.index('{',start)+1
  s=s[:brace]+'\n  fifa08::diagnostics::Scope copy_diagnostic(fifa08::diagnostics::Kind::Copy);'+s[brace:]
  old='    return render_target_cache_->Resolve(*memory_, *shared_memory_, *texture_cache_,\n                                         written_address, written_length);'
  s=replace(s,old,'    bool ok = render_target_cache_->Resolve(*memory_, *shared_memory_, *texture_cache_, written_address, written_length);\n    if(ok) fifa08::diagnostics::Resolve(written_address,written_length);\n    return ok;')
  start=s.index('bool '+cls+'CommandProcessor::IssueCopy_ReadbackResolvePath()')
  tail=s[start:]
  tail=replace(tail,'  if (!written_length) {','  fifa08::diagnostics::Resolve(written_address,written_length);\n  if (!written_length) {')
  return s[:start]+tail
 edit('src/graphics/'+api+'/command_processor.cpp','FIFA08_GUEST_FRAME_DIAGNOSTICS_V1',backend)
def exports(s):
 s=replace(s,'#include <rex/graphics/fifa08_frame_meter.h>','#include <rex/graphics/fifa08_frame_meter.h>\n#include <rex/graphics/fifa08_guest_frame_diagnostics.h>')
 return replace(s,'extern "C" REX_GPU_PLUGIN_EXPORT uint64_t Fifa08GpuSubmittedImages()', 'extern "C" REX_GPU_PLUGIN_EXPORT void Fifa08RequestGuestCapture() { fifa08::diagnostics::capture_requested.store(true); }\nextern "C" REX_GPU_PLUGIN_EXPORT void Fifa08ObservedScene(uint32_t flags, uint64_t submission) { fifa08::diagnostics::scene.store(flags); fifa08::diagnostics::cpu_submission.store(submission); }\n\nextern "C" REX_GPU_PLUGIN_EXPORT uint64_t Fifa08GpuSubmittedImages()')
edit('src/graphics/plugin_main.cpp','Fifa08RequestGuestCapture',exports)
print('FIFA09 adaptations: visibility-copy fix, opt-in EDRAM clamp, guest-frame diagnostics/capture')
