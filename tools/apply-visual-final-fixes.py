from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[1];sdk=root/'tools/rexglue-source/rexglue-sdk-0.10.0';backup=root/'analysis/visual-final-fixes/before';backup.mkdir(parents=True,exist_ok=True)
def edit(name,marker,fn):
 p=sdk/name;s=p.read_text()
 if marker in s:return
 result=fn(s);dest=backup/name.replace('/','_')
 if not dest.exists():shutil.copyfile(p,dest)
 p.write_text(result,newline='\n')
def once(s,a,b):
 if s.count(a)!=1:raise RuntimeError('Anchor '+str(s.count(a))+' '+a[:100])
 return s.replace(a,b,1)
for api,cls,rb in [('d3d12','D3D12','rb'),('vulkan','Vulkan','readback')]:
 edit('include/rex/graphics/'+api+'/command_processor.h','FIFA08_RESOLVE_LAYOUT_V1',lambda s:once(s,'    uint32_t written_size[2] = {0, 0};','    uint32_t written_size[2] = {0, 0};\n    // FIFA08_RESOLVE_LAYOUT_V1\n    std::array<uint32_t, 4> resolve_layout[2]{};'))
 def resolve(s,cls=cls,rb=rb):
  start=s.index('bool '+cls+'CommandProcessor::IssueCopy_ReadbackResolvePath()');end=s.index('\nvoid '+cls+'CommandProcessor::',start);b=s[start:end];a='  bool snapshot_consumed = false;'
  b=once(b,a,a+'\n  // FIFA08_RESOLVE_LAYOUT_V1: addresses and lengths may be recycled for other surfaces.\n  const std::array<uint32_t,4> layout = {register_file_->Get<reg::RB_COPY_DEST_INFO>().value,\n      register_file_->Get<reg::RB_COPY_DEST_PITCH>().value, register_file_->Get<reg::RB_SURFACE_INFO>().value,\n      (texture_cache_->draw_resolution_scale_x()<<16)|texture_cache_->draw_resolution_scale_y()};')
  for i in range(2):
   a=f'{rb}.written_size[{i}] >= written_length';b=once(b,a,a+f' && {rb}.resolve_layout[{i}] == layout')
  a=f'  {rb}.written_size[write_index] = written_length;';b=once(b,a,a+f'\n  {rb}.resolve_layout[write_index] = layout;');return s[:start]+b+s[end:]
 edit('src/graphics/'+api+'/command_processor.cpp','FIFA08_RESOLVE_LAYOUT_V1',resolve)
def header(s):
 s=once(s,'static constexpr uint32_t kVersion = 12;','static constexpr uint32_t kVersion = 13; // FIFA08_FSI_QUERY_COVERAGE_V1')
 s=once(s,'      DepthStencilMode depth_stencil_mode : 3;','      DepthStencilMode depth_stencil_mode : 3;\n      uint32_t fsi_query_coverage : 1;')
 return once(s,'  spv::Id output_fragment_sample_mask_;','  spv::Id output_fragment_sample_mask_;\n  spv::Id var_main_fsi_query_mask_ = spv::NoResult;')
edit('include/rex/graphics/pipeline/shader/spirv_translator.h','FIFA08_FSI_QUERY_COVERAGE_V1',header)
def translator(s):
 a='modification.pixel.depth_stencil_mode = depth_stencil_mode;';s=once(s,a,a+'\n  modification.pixel.fsi_query_coverage = edram_fragment_shader_interlock_;')
 a='void SpirvShaderTranslator::StartFragmentShaderInMain() {'
 s=once(s,a,a+'''
  // FIFA08_FSI_QUERY_COVERAGE_V1: private storage preserves coverage across early/kill branches.
  var_main_fsi_query_mask_ = spv::NoResult;
  if (edram_fragment_shader_interlock_ && GetSpirvShaderModification().pixel.fsi_query_coverage) {
    var_main_fsi_query_mask_ = builder_->createVariable(spv::NoPrecision,
        spv::StorageClassFunction, type_uint_, "xe_fsi_query_coverage", const_uint_0_);
  }
''')
 a='  if (!edram_fragment_shader_interlock_) {\n    // Framebuffer color attachment outputs.'
 return once(s,a,'''  if (edram_fragment_shader_interlock_ && GetSpirvShaderModification().pixel.fsi_query_coverage) {
    output_fragment_sample_mask_ = builder_->createVariable(spv::NoPrecision, spv::StorageClassOutput,
        builder_->makeArrayType(type_int_, builder_->makeUintConstant(1), 0), "gl_SampleMask");
    builder_->addDecoration(output_fragment_sample_mask_, spv::DecorationBuiltIn, spv::BuiltInSampleMask);
    main_interface_.push_back(output_fragment_sample_mask_);
  }
'''+a)
edit('src/graphics/pipeline/shader/spirv_translator.cpp','FIFA08_FSI_QUERY_COVERAGE_V1',translator)
def rb(s):
 a='    if (color_write_depth_stencil_condition != spv::NoResult) {'
 s=once(s,a,'''    // FIFA08_FSI_QUERY_COVERAGE_V1
    if (var_main_fsi_query_mask_ != spv::NoResult) {
      builder_->createStore(builder_->createBinOp(spv::OpBitwiseAnd, type_uint_,
          main_fsi_sample_mask_, builder_->makeUintConstant(15)), var_main_fsi_query_mask_);
    }
'''+a)
 a='    builder_->createNoResultOp(spv::OpEndInvocationInterlockEXT);'
 return once(s,a,a+'''
    if (var_main_fsi_query_mask_ != spv::NoResult) {
      spv::Id mask = builder_->createLoad(var_main_fsi_query_mask_, spv::NoPrecision);
      auto bits = [&](uint32_t v) {return builder_->createBinOp(spv::OpBitwiseAnd, type_uint_, mask, builder_->makeUintConstant(v));};
      auto shift = [&](spv::Id v, uint32_t n, bool left) {return builder_->createBinOp(left?spv::OpShiftLeftLogical:spv::OpShiftRightLogical, type_uint_, v, builder_->makeUintConstant(n));};
      spv::Id mask_2 = native_2x_msaa_no_attachments_
          ? builder_->createBinOp(spv::OpBitwiseOr, type_uint_, shift(bits(1),1,true),shift(bits(2),1,false))
          : builder_->createBinOp(spv::OpBitwiseOr, type_uint_, bits(1),shift(bits(2),2,true));
      spv::Id mask_4 = builder_->createBinOp(spv::OpBitwiseOr, type_uint_, bits(9),
          builder_->createBinOp(spv::OpBitwiseOr, type_uint_,shift(bits(2),1,true),shift(bits(4),1,false)));
      spv::Id samples=LoadMsaaSamplesFromFlags();
      spv::Id host_mask=builder_->createTriOp(spv::OpSelect,type_uint_,
          builder_->createBinOp(spv::OpIEqual,type_bool_,samples,builder_->makeUintConstant(uint32_t(xenos::MsaaSamples::k4X))),mask_4,bits(1));
      host_mask=builder_->createTriOp(spv::OpSelect,type_uint_,
          builder_->createBinOp(spv::OpIEqual,type_bool_,samples,builder_->makeUintConstant(uint32_t(xenos::MsaaSamples::k2X))),mask_2,host_mask);
      id_vector_temp_.clear();id_vector_temp_.push_back(const_int_0_);
      builder_->createStore(builder_->createUnaryOp(spv::OpBitcast,type_int_,host_mask),
          builder_->createAccessChain(spv::StorageClassOutput,output_fragment_sample_mask_,id_vector_temp_));
    }
''')
edit('src/graphics/pipeline/shader/spirv_translator_rb.cpp','FIFA08_FSI_QUERY_COVERAGE_V1',rb)
def cp(s):
 a='    // Translate the shaders now to obtain the sampler bindings.'
 return once(s,a,'''    // FIFA08_FSI_QUERY_COVERAGE_V1: normal shading retains its original translation.
    pixel_shader_modification.pixel.fsi_query_coverage = active_occlusion_query_.valid &&
        render_target_cache_->GetPath() == RenderTargetCache::Path::kPixelShaderInterlock;

'''+a)
edit('src/graphics/vulkan/command_processor.cpp','FIFA08_FSI_QUERY_COVERAGE_V1',cp)
def sampler(s):
 return once(s,'    assert_false(parameters.mip_linear);\n    sampler_create_info.maxLod = sampler_create_info.minLod + 0.25f;','''    // FIFA08_VULKAN_BASEMAP_PARITY_V1: match D3D12 base-map anisotropic clamp.
    if(parameters.aniso_filter == xenos::AnisoFilter::kDisabled) assert_false(parameters.mip_linear);
    sampler_create_info.maxLod = sampler_create_info.minLod +
        (parameters.aniso_filter == xenos::AnisoFilter::kDisabled ? 0.25f : 0.f);''')
edit('src/graphics/vulkan/texture_cache.cpp','FIFA08_VULKAN_BASEMAP_PARITY_V1',sampler)
print('Visual fixes: resolve layout identity, query-only FSI coverage, base-map sampler parity.')
# Preserve a logical guest query across command-buffer splits without extra waits.
def query_header(s):
 s=once(s,'  bool AcquireOcclusionQueryIndex(uint32_t& host_index_out);','  bool AcquireOcclusionQueryIndex(uint32_t& host_index_out);\n  bool ResumeGuestOcclusionQuery(); // FIFA08_QUERY_SEGMENTS_V1\n  std::vector<uint32_t> occlusion_query_segments_;')
 return s
def query_cp(s):
 # The commands must both be outside the render pass when the query began outside it.
 start=s.index('void VulkanCommandProcessor::DisableHostOcclusionQueries()');end=s.index('\nbool VulkanCommandProcessor::BeginGuestOcclusionQuery',start)
 s=s[:start]+'''void VulkanCommandProcessor::DisableHostOcclusionQueries() {
  if(active_occlusion_query_.valid && active_occlusion_query_.host_index != UINT32_MAX && occlusion_query_pool_ != VK_NULL_HANDLE && submission_open_) {
    EndRenderPass();
    deferred_command_buffer_.CmdVkEndQuery(occlusion_query_pool_,active_occlusion_query_.host_index);
  }
  active_occlusion_query_={};occlusion_query_segments_.clear();occlusion_query_cursor_=0;occlusion_query_resources_available_=false;
}
// FIFA08_QUERY_SEGMENTS_V1: a split ends a host segment, not the guest query.
bool VulkanCommandProcessor::ResumeGuestOcclusionQuery() {
  if(!active_occlusion_query_.valid || active_occlusion_query_.host_index != UINT32_MAX)return true;
  if(occlusion_query_segments_.size() >= kMaxOcclusionQueries){DisableHostOcclusionQueries();return false;}
  uint32_t index;if(!AcquireOcclusionQueryIndex(index))return false;
  EndRenderPass();deferred_command_buffer_.CmdVkResetQueryPool(occlusion_query_pool_,index,1);
  deferred_command_buffer_.CmdVkBeginQuery(occlusion_query_pool_,index,GetVulkanDevice()->properties().occlusionQueryPrecise?VK_QUERY_CONTROL_PRECISE_BIT:0);
  active_occlusion_query_.host_index=index;return true;
}
'''+s[end:]
 start=s.index('bool VulkanCommandProcessor::BeginGuestOcclusionQuery');end=s.index('\nbool VulkanCommandProcessor::EndGuestOcclusionQuery',start);body=s[start:end];body=once(body,'  uint32_t host_index = 0;','  occlusion_query_segments_.clear();\n  uint32_t host_index = 0;');s=s[:start]+body+s[end:]
 start=s.index('bool VulkanCommandProcessor::EndGuestOcclusionQuery');end=s.index('\nuint64_t VulkanCommandProcessor::NormalizeOcclusionSamples',start);body=s[start:end];body=once(body,'  uint32_t host_index = active_occlusion_query_.host_index;\n  active_occlusion_query_ = {};\n\n  if (!BeginSubmission(true)) {\n    return false;\n  }','  if (!BeginSubmission(true)) return false;\n  uint32_t host_index = active_occlusion_query_.host_index;\n  active_occlusion_query_ = {};\n  occlusion_query_segments_.push_back(host_index);')
 body=once(body,'  uint64_t samples = NormalizeOcclusionSamples(results[host_index]);','  uint64_t samples=0;for(uint32_t index:occlusion_query_segments_)samples+=results[index];\n  occlusion_query_segments_.clear();samples=NormalizeOcclusionSamples(samples);');s=s[:start]+body+s[end:]
 a='''    if (active_occlusion_query_.valid && occlusion_query_pool_ != VK_NULL_HANDLE) {
      deferred_command_buffer_.CmdVkEndQuery(occlusion_query_pool_,
                                             active_occlusion_query_.host_index);
      active_occlusion_query_ = {};
    }

    EndRenderPass();'''
 b='''    EndRenderPass();
    if (active_occlusion_query_.valid && active_occlusion_query_.host_index != UINT32_MAX && occlusion_query_pool_ != VK_NULL_HANDLE) {
      uint32_t index=active_occlusion_query_.host_index;
      deferred_command_buffer_.CmdVkEndQuery(occlusion_query_pool_,index);
      deferred_command_buffer_.CmdVkCopyQueryPoolResults(occlusion_query_pool_,index,1,
          occlusion_query_readback_buffer_,sizeof(uint64_t)*index,sizeof(uint64_t),VK_QUERY_RESULT_64_BIT|VK_QUERY_RESULT_WAIT_BIT);
      occlusion_query_segments_.push_back(index);active_occlusion_query_.host_index=UINT32_MAX;
    }'''
 s=once(s,a,b)
 start=s.index('bool VulkanCommandProcessor::BeginSubmission(bool is_guest_command)');end=s.index('\nbool VulkanCommandProcessor::EndSubmission(',start);body=s[start:end];body=once(body,'  if (submission_open_ && !is_opening_frame) {\n    return true;','  if (submission_open_ && !is_opening_frame) {\n    return !is_guest_command || ResumeGuestOcclusionQuery();');a=body.rfind('  return true;');assert a>=0;body=body[:a]+body[a:].replace('  return true;','  return !is_guest_command || ResumeGuestOcclusionQuery();',1);return s[:start]+body+s[end:]
edit('include/rex/graphics/vulkan/command_processor.h','FIFA08_QUERY_SEGMENTS_V1',query_header)
edit('src/graphics/vulkan/command_processor.cpp','FIFA08_QUERY_SEGMENTS_V1',query_cp)
print('Vulkan query segments preserved across submissions, with the existing final synchronization.')
# Old snapshots from a surface not used in the preceding frame must not be published.
for api,cls,rb in [('d3d12','D3D12','rb'),('vulkan','Vulkan','readback')]:
 def freshness(s,cls=cls,rb=rb):
  start=s.index('bool '+cls+'CommandProcessor::IssueCopy_ReadbackResolvePath()');end=s.index('\nvoid '+cls+'CommandProcessor::',start);body=s[start:end];a=f'  {rb}.last_used_frame = frame_current_;';body=once(body,a,f'''  // FIFA08_RESOLVE_FRESHNESS_V1: retain fences/resources, discard stale contents only.
  if(frame_current_ > {rb}.last_used_frame + 1) {{
    {rb}.written_size[0] = {rb}.written_size[1] = 0;
  }}
'''+a);return s[:start]+body+s[end:]
 edit('src/graphics/'+api+'/command_processor.cpp','FIFA08_RESOLVE_FRESHNESS_V1',freshness)
print('Resolve snapshots expire across an unused frame without resetting in-flight fences.')