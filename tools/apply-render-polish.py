"""Preserve Gemini delayed reads; coalesce cold producers and reserve FXAA views once."""
from pathlib import Path
root = Path(__file__).resolve().parents[1]
sdk = root / 'tools/rexglue-source/rexglue-sdk-0.10.0'
for api, rb, flag in [('d3d12', 'rb', 'false'), ('vulkan', 'readback', 'true')]:
    p = sdk / f'src/graphics/{api}/command_processor.cpp'
    original = p.read_bytes()
    s = original.decode().replace('\r\n', '\n')
    if 'FIFA08_COLD_COALESCE_V1' not in s:
        start = s.index('bool ' + ('D3D12' if api == 'd3d12' else 'Vulkan') + 'CommandProcessor::IssueCopy_ReadbackResolvePath()')
        a = s.index('    if (ready < 2) {', start)
        s = s[:a] + f"""    // FIFA08_COLD_COALESCE_V1: until the first producer completes,
    // keep it in flight. Repeated resolves still execute above (including
    // clears), but must not enqueue redundant downscales/readback copies or
    // continually replace the only snapshot that could become readable.
    if (ready == 2 && ({rb}.submission_written[0] > submission_completed_ ||
                       {rb}.submission_written[1] > submission_completed_)) {{
      fifa08::RecordResolveDecision({flag}, fifa08::ResolveDecision::kCoalesced);
      return true;
    }}
""" + s[a:]
    if api == 'd3d12' and 'FIFA08_FXAA_DESCRIPTOR_SAFETY_V1' not in s:
        a = s.index('        // Destination, source, and if bindful, gamma ramp.')
        b = s.index('        // Must not call anything', a)
        s = s[:a] + """        // Destination, source, and if bindful, gamma ramp.
        // FIFA08_FXAA_DESCRIPTOR_SAFETY_V1: reserve both passes together.
        // A second allocation can switch heaps after gamma tables are bound.
        ui::d3d12::util::DescriptorCpuGpuHandlePair apply_gamma_descriptors[5];
        ui::d3d12::util::DescriptorCpuGpuHandlePair apply_gamma_descriptor_gamma_ramp;
        const uint32_t gamma_descriptor_count = bindless_resources_used_ ? 2 : 3;
        auto* fxaa_descriptors = apply_gamma_descriptors + gamma_descriptor_count;
        if (!RequestOneUseSingleViewDescriptors(
                gamma_descriptor_count + (use_fxaa ? 2u : 0u), apply_gamma_descriptors)) {
          return false;
        }
""" + s[b:]
        a = s.index('          // Destination and source.', a)
        b = s.index('            assert_true(apply_gamma_dest_initial_state ==', a)
        s = s[:a] + '          {\n' + s[b:]
    if original != s.encode(): p.write_bytes(s.encode())
print('Render polish: cold producer coalescing on both APIs; atomic D3D12 FXAA descriptor reservation.')
