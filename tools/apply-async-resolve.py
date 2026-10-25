"""Upgrade fenced delayed resolves without making every resolve a queue drain.

Full mode stays synchronous. Fast/some retain a completed slot and allow at
most one replacement in flight; busy replacements are coalesced. Cold caches
defer publication until a slot is complete, matching the Gemini fast path.
Guest memory retains its preceding contents until then; full mode waits.
"""
from pathlib import Path
import shutil

project = Path(__file__).resolve().parents[1]
sdk = project / 'tools/rexglue-source/rexglue-sdk-0.10.0'
backup = project / 'analysis/cpu-bottleneck/pass9/before'
meter = sdk / 'src/graphics/fifa08_async_resolve_meter.h'
source = (project / 'native/src/async_resolve_meter.h').read_bytes()
if not meter.exists() or meter.read_bytes() != source: meter.write_bytes(source)

def telemetry(body, rb, vulkan):
    if 'RecordResolveDecision' in body: return body
    api = 'true' if vulkan else 'false'
    anchor = f'      if ({rb}.submission_written[write_index] > submission_completed_) return true;'
    assert anchor in body
    body = body.replace(anchor, f'''      if ({rb}.submission_written[write_index] > submission_completed_) {{
        fifa08::RecordResolveDecision({api}, fifa08::ResolveDecision::kCoalesced);
        return true;
      }}
      fifa08::RecordResolveDecision({api}, fifa08::ResolveDecision::kQueued);''', 1)
    anchor = '  if (!snapshot_consumed) {\n'
    assert anchor in body
    return body.replace(anchor, anchor+f'    if (delayed) fifa08::RecordResolveDecision({api}, fifa08::ResolveDecision::kCold);\n', 1)

for api, cls in [('d3d12', 'D3D12CommandProcessor'), ('vulkan', 'VulkanCommandProcessor')]:
    path = sdk / f'src/graphics/{api}/command_processor.cpp'
    text = path.read_text()
    if '#include "../fifa08_async_resolve_meter.h"' not in text:
        at = text.index('#include ')
        text = text[:at]+'#include "../fifa08_async_resolve_meter.h"\n'+text[at:]
    start = text.index('bool '+cls+'::IssueCopy_ReadbackResolvePath()')
    end = text.index('\nvoid '+cls+'::', start)
    body = text[start:end]
    if 'FIFA08_ASYNC_RESOLVE_V2' in body:
        body = telemetry(body, 'rb' if api=='d3d12' else 'readback', api=='vulkan')
        text = text[:start]+body+text[end:]
        if path.read_bytes() != text.encode(): path.write_bytes(text.encode())
        continue
    assert 'FIFA08_READBACK_SAFETY_V1' in body
    saved = backup / f'{api}-command_processor.cpp'
    saved.parent.mkdir(parents=True, exist_ok=True)
    if not saved.exists(): shutil.copyfile(path, saved)
    d3d = api == 'd3d12'
    rb = 'rb' if d3d else 'readback'
    wait = 'CheckSubmissionFence' if d3d else 'CheckSubmissionFenceAndDeviceLoss'
    good = 'submission_completed_ != ~uint64_t(0)' if d3d else '!device_lost_'
    mode = '  ReadbackResolveMode readback_mode = GetReadbackResolveMode(REXCVAR_GET('+api+'_readback_resolve));\n'
    if d3d:
        assert mode in body
        body = body.replace(mode, '', 1)
    anchor = '  uint32_t write_index = '+rb+'.current_index;'
    assert anchor in body
    insert = '''  // FIFA08_ASYNC_RESOLVE_V2
'''+(mode if d3d else '')+f'''  const bool delayed = readback_mode == ReadbackResolveMode::kFast ||
                       readback_mode == ReadbackResolveMode::kSome;
  auto consume_slot = [&](uint32_t read_index) -> bool {{
    // Caller must establish completion before any host access.
    if (!{rb}.submission_written[read_index] ||
        {rb}.submission_written[read_index] > submission_completed_ ||
        !({good})) return false;
'''
    if not d3d:
        insert += '''    VkMappedMemoryRange range = {};
    range.sType = VK_STRUCTURE_TYPE_MAPPED_MEMORY_RANGE;
    range.memory = readback.memories[read_index];
    range.size = VK_WHOLE_SIZE;
    if (dfn.vkInvalidateMappedMemoryRanges(device, 1, &range) != VK_SUCCESS) return false;
'''
    insert += f'''    uint8_t* destination = memory_->TranslatePhysical(written_address);
    if (destination) {{
      if (REXCVAR_QUERY(bool, readback_resolve_skip_unchanged))
        fifa08::CopyChangedReadback(destination, {rb}.mapped_data[read_index], written_length);
      else
        std::memcpy(destination, {rb}.mapped_data[read_index], written_length);
    }}
    return true;
  }};
  uint32_t write_index = {rb}.current_index;
  bool snapshot_consumed = false;
  if (delayed) {{
    // Poll only. Waiting on a producer in the current open submission would
    // flush and serialize every resolve in this command batch.
    {wait}(0);
    if (!({good})) return false;
    const uint32_t ready = fifa08::SelectCompletedReadback(
        {rb}.submission_written[0], {rb}.submission_written[1],
        {rb}.buffers[0] && {rb}.mapped_data[0] && {rb}.written_size[0] >= written_length,
        {rb}.buffers[1] && {rb}.mapped_data[1] && {rb}.written_size[1] >= written_length,
        submission_completed_);
    if (ready < 2) {{
      if (readback_mode != ReadbackResolveMode::kSome && !consume_slot(ready)) return false;
      snapshot_consumed = true;
      // Retain the complete snapshot while the other slot is being replaced.
      write_index = 1 - ready;
      if ({rb}.submission_written[write_index] > submission_completed_) return true;
    }}
  }}
'''
    body = body.replace(anchor, insert, 1)
    start_tail = body.index('  const bool delayed =', body.index(rb+'.written_size[write_index] = written_length;'))
    stop_tail = body.index('\n  '+rb+'.current_index =', start_tail)
    vulkan_flag = 'true' if api == 'vulkan' else 'false'
    body = body[:start_tail]+f'''  if (!snapshot_consumed) {{
    if (delayed) {{
      fifa08::RecordResolveDecision({vulkan_flag}, fifa08::ResolveDecision::kCold);
    }} else {{
      const uint64_t producer = {rb}.submission_written[write_index];
      if (submission_completed_ < producer) {wait}(producer);
      if (!consume_slot(write_index)) return false;
    }}
  }}
'''+body[stop_tail:]
    body = body.replace(rb+'.current_index = 1 - '+rb+'.current_index;',
                        rb+'.current_index = 1 - write_index;')
    body = telemetry(body, rb, not d3d)
    text = text[:start]+body+text[end:]
    path.write_bytes(text.encode())
print('Async resolve V2: completed snapshots only, single in-flight replacement, cold publication deferred/full fences preserved (both APIs).')
