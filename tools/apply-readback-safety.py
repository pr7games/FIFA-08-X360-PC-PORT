"""Reproducible readback lifetime, completion and allocation fixes for pinned SDK."""
from pathlib import Path
import re

project = Path(__file__).resolve().parents[1]
sdk = project / 'tools/rexglue-source/rexglue-sdk-0.10.0'
policy = (project / 'native/src/readback_slot_policy.h').read_bytes()
policy_path = sdk / 'include/rex/graphics/fifa08_readback_slots.h'
if not policy_path.exists() or policy_path.read_bytes() != policy:
    policy_path.write_bytes(policy)

def function(text, cls, name, change):
    match = re.search(r'^bool '+cls+'::'+name+r'\(', text, re.M)
    if not match:
        match = re.search(r'^void '+cls+'::'+name+r'\(', text, re.M)
    assert match, name
    end = re.search(r'^\w[\w:*<> ]* '+cls+r'::', text[match.end():], re.M)
    assert end, name
    stop = match.end()+end.start()
    body = text[match.start():stop]
    if 'FIFA08_READBACK_SAFETY_V1' in body:
        return text
    return text[:match.start()]+change(body)+text[stop:]

for api, cls in [('d3d12','D3D12CommandProcessor'),('vulkan','VulkanCommandProcessor')]:
    hp = sdk / f'include/rex/graphics/{api}/command_processor.h'
    text = hp.read_text()
    if '#include <rex/graphics/fifa08_readback_slots.h>' not in text:
        at = text.index('#include ')
        text = text[:at]+'#include <rex/graphics/fifa08_readback_slots.h>\n'+text[at:]
    text, count = re.subn(r'(static inline uint32_t AlignReadbackBufferSize\(uint32_t size\) \{)[\s\S]*?\n  \}',
        r'\1\n    return fifa08::CachedReadbackAllocationSize(size);\n  }', text, count=1)
    assert count == 1
    if 'ranges_written[2]' not in text:
        text = text.replace('    uint32_t written_size[2] = {0, 0};',
            '    uint32_t written_size[2] = {0, 0};\n    std::vector<draw_util::MemExportRange> ranges_written[2];', 1)
        assert 'ranges_written[2]' in text
    if hp.read_bytes() != text.encode(): hp.write_bytes(text.encode())
    cp = sdk / f'src/graphics/{api}/command_processor.cpp'
    text = cp.read_text()
    d3d = api == 'd3d12'
    rb = 'rb' if d3d else 'readback'
    wait = 'CheckSubmissionFence' if d3d else 'CheckSubmissionFenceAndDeviceLoss'
    good = 'submission_completed_ != ~uint64_t(0)' if d3d else '!device_lost_'

    def resolve(body):
        body = body.replace('{\n', '{\n  // FIFA08_READBACK_SAFETY_V1\n'
            '  fifa08::sync_meter::Scope resolve_measure(fifa08::sync_meter::'+
            ('kD3D12Resolve' if d3d else 'kVulkanResolve')+');\n', 1)
        if d3d:
            anchor = '  if (size > rb.sizes[write_index]) {\n'
            assert anchor in body
            body = body.replace(anchor, anchor+'''    if (rb.submission_written[write_index] > submission_completed_) {
      CheckSubmissionFence(rb.submission_written[write_index]);
      if (submission_completed_ < rb.submission_written[write_index] ||
          submission_completed_ == ~uint64_t(0)) return true;
    }
''', 1)
            body = body.replace('    rb.sizes[write_index] = size;',
                '    rb.sizes[write_index] = size;\n    rb.submission_written[write_index] = 0;\n    rb.written_size[write_index] = 0;', 1)
            body = body.replace('  if (!rb.buffers[write_index]) {',
                '  if (!rb.buffers[write_index] || !rb.mapped_data[write_index]) {', 1)
            anchor = '  if (is_scaled) {'
        else:
            body = body.replace('    readback.sizes[index] = size;',
                '    readback.sizes[index] = size;\n    readback.submission_written[index] = 0;\n    readback.written_size[index] = 0;', 1)
            anchor = '  if (is_scaled) {'
        assert anchor in body
        # Any resize wait may close a submission. Reopen before emitting copies.
        body = body.replace(anchor, '  if (!BeginSubmission(true)) return false;\n\n'+anchor, 1)
        start = body.index('  bool use_delayed_sync =')
        stop = body.index('\n  '+rb+'.current_index =', start)
        mode_name = 'd3d12_readback_resolve' if d3d else 'vulkan_readback_resolve'
        # D3D12 obtains the mode immediately before this block; Vulkan earlier.
        new = f'''  {rb}.submission_written[write_index] = {'submission_current_' if d3d else 'GetCurrentSubmission()'};
  {rb}.written_size[write_index] = written_length;
  const bool delayed = readback_mode == ReadbackResolveMode::kFast ||
                       readback_mode == ReadbackResolveMode::kSome;
  const uint32_t previous = 1 - write_index;
  const auto plan = fifa08::PlanResolveReadback(write_index, delayed,
      readback_mode == ReadbackResolveMode::kSome, written_length,
      {rb}.buffers[previous] && {rb}.mapped_data[previous],
      {rb}.sizes[previous], {rb}.written_size[previous], {rb}.submission_written[previous]);
  const uint32_t read_index = plan.index;
  if (plan.copy) {{
    const bool consumed = fifa08::ConsumeCompletedReadback(
        {rb}.submission_written[read_index],
        [&](uint64_t producer) {{
          if (submission_completed_ < producer) {wait}(producer);
          return {good} && submission_completed_ >= producer;
        }}, [&]() {{
'''
        if not d3d:
            new += '''          VkMappedMemoryRange range = {};
          range.sType = VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE;
          range.memory = readback.memories[read_index];
          range.size = VK_WHOLE_SIZE;
          if (dfn.vkInvalidateMappedMemoryRanges(device, 1, &range) != VK_SUCCESS) return false;
'''
        new += f'''          uint8_t* destination = memory_->TranslatePhysical(written_address);
          if (destination) {{
            if (REXCVAR_QUERY(bool, readback_resolve_skip_unchanged))
              fifa08::CopyChangedReadback(destination, {rb}.mapped_data[read_index], written_length);
            else
              std::memcpy(destination, {rb}.mapped_data[read_index], written_length);
          }}
          return true;
        }});
    if (!consumed) return true;
  }}
'''
        return body[:start]+new+body[stop:]

    def memexport(body):
        body = body.replace('{\n', '{\n  // FIFA08_READBACK_SAFETY_V1\n'
            '  fifa08::sync_meter::Scope memexport_measure(fifa08::sync_meter::'+
            ('kD3D12Memexport' if d3d else 'kVulkanMemexport')+');\n', 1)
        body = body.replace('  const uint32_t read_index = 1 - write_index;',
                            '  uint32_t read_index = 1 - write_index;', 1)
        anchor = ('  shared_memory_->UseAsCopySource();' if d3d else
                  '  shared_memory_->Use(VulkanSharedMemory::Usage::kRead);')
        assert anchor in body
        body = body.replace(anchor, '  if (!BeginSubmission(true)) return false;\n'+anchor, 1)
        start = body.index('  if (!previous_slot_ready) {')
        stop = body.index('\n  }', start)+4
        body = body[:start]+f'''  if (!previous_slot_ready) {{
    // The current slot copy is already queued. Do not enqueue a second full
    // readback of identical exports just to handle a cold or busy old slot.
    read_index = write_index;
    const uint64_t producer = readback.submission_written[read_index];
    {wait}(producer);
    if (!({good}) || submission_completed_ < producer) return false;
  }}'''+body[stop:]
        # The fallback reads the write slot, but the next call still flips slots.
        body = body.replace('  readback.current_index = read_index;',
                            '  readback.current_index = 1 - write_index;')
        return body

    def evict(body):
        body = body.replace('{\n', '{\n  // FIFA08_READBACK_SAFETY_V1\n', 1)
        anchor = '    for (uint32_t i = 0; i < 2; ++i) {'
        assert anchor in body
        body = body.replace(anchor, '''    if (!fifa08::CanReleaseReadback(readback.submission_written[0],
                                     readback.submission_written[1], submission_completed_)) {
      ++it;
      continue;
    }
'''+anchor, 1)
        return body

    text = function(text, cls, 'IssueCopy_ReadbackResolvePath', resolve)
    text = function(text, cls, 'IssueDraw_MemexportReadbackFastPath', memexport)
    text = function(text, cls, 'EvictOldReadbackBuffers', evict)
    # The old cache key stores only the first address and total size. Two
    # different range lists can share that key; never reuse incompatible data.
    start = text.index('bool '+cls+'::IssueDraw_MemexportReadbackFastPath(')
    stop = text.index('bool '+cls+'::IssueCopy()', start)
    body = text[start:stop]
    if 'ranges_written[write_index] = memexport_ranges_' not in body:
        body = body.replace('  readback.written_size[write_index] = total_size;',
            '  readback.written_size[write_index] = total_size;\n  readback.ranges_written[write_index] = memexport_ranges_;', 1)
        body = body.replace('readback.submission_written[read_index] <= submission_completed_;',
            'readback.submission_written[read_index] <= submission_completed_ &&\n      fifa08::SameReadbackRanges(readback.ranges_written[read_index], memexport_ranges_);', 1)
        assert 'fifa08::SameReadbackRanges' in body
        text = text[:start]+body+text[stop:]
    if cp.read_bytes() != text.encode(): cp.write_bytes(text.encode())

p = sdk / 'src/graphics/fifa08_sync_meter.h'
text = p.read_text()
if 'kD3D12Resolve' not in text:
    text = text.replace('kVulkanQuery, kCount', 'kVulkanQuery, kD3D12Resolve, kVulkanResolve, kD3D12Memexport, kVulkanMemexport, kCount')
    text = text.replace('"Vulkan/occlusion"};', '"Vulkan/occlusion", "D3D12/resolve", "Vulkan/resolve", "D3D12/memexport", "Vulkan/memexport"};')
    p.write_bytes(text.encode())
print('Readback safety: producer fences, initialized slots, in-flight eviction guard, single-copy memexport fallback, bounded cache allocation.')
