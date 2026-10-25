"""Keep D3D12's bootstrap coherent when a guest address is recycled.

Does not enable synchronous readback on the warmed steady-state path.
"""
from pathlib import Path
import shutil

root = Path(__file__).resolve().parents[1]
sdk = root / 'tools/rexglue-source/rexglue-sdk-0.10.0'
out = root / 'analysis/final-arena-review-20261022'
out.mkdir(parents=True, exist_ok=True)
header = sdk / 'include/rex/graphics/d3d12/command_processor.h'
source = sdk / 'src/graphics/d3d12/command_processor.cpp'

def replace(s, a, b):
    assert s.count(a) == 1, a
    return s.replace(a, b, 1)

for path in (header, source):
    s = path.read_text()
    if 'FIFA08_READBACK_EPOCH_V1' in s:
        continue
    saved = out / (path.name + '.before')
    if not saved.exists():
        shutil.copyfile(path, saved)
    if path == header:
        s = replace(s, '    uint64_t last_published_generation = 0;', '''    uint64_t last_published_generation = 0;
    // FIFA08_READBACK_EPOCH_V1: publication belongs to a surface lifetime.
    std::array<uint32_t, 4> active_resolve_layout{};
    bool snapshot_initialized = false;''')
    else:
        s = replace(s, '''  // FIFA08_RESOLVE_FRESHNESS_V1: retain fences/resources, discard stale contents only.
  if(frame_current_ > rb.last_used_frame + 1) {
    rb.written_size[0] = rb.written_size[1] = 0;
  }
  rb.last_used_frame = frame_current_;''', '')
        s = replace(s, '      rb.last_published_generation = rb.capture_generation[read_index];', '''      rb.last_published_generation = rb.capture_generation[read_index];
      rb.snapshot_initialized = true;''')
        anchor = '      (texture_cache_->draw_resolution_scale_x()<<16)|texture_cache_->draw_resolution_scale_y()};'
        s = replace(s, anchor, anchor + '''
  // FIFA08_READBACK_EPOCH_V1: a completed capture from a previous use of the
  // address is not an initialized snapshot of this surface. Keep its fence
  // for safe reuse, but never select its contents or skip the new bootstrap.
  if (rb.active_resolve_layout != layout ||
      frame_current_ > rb.last_used_frame + 1) {
    rb.written_size[0] = rb.written_size[1] = 0;
    rb.snapshot_initialized = false;
  }
  rb.active_resolve_layout = layout;
  rb.last_used_frame = frame_current_;
  if (readback_mode == ReadbackResolveMode::kFast && !rb.snapshot_initialized) {
    // Only a cold/recycled surface drains its old producers. Never overwrite
    // a still-in-flight slot merely because its allocation already fits.
    const uint64_t pending = std::max(rb.submission_written[0], rb.submission_written[1]);
    if (pending > submission_completed_) CheckSubmissionFence(pending);
    if (submission_completed_ == ~uint64_t(0) || submission_completed_ < pending)
      return false;
  }''')
        s = replace(s, '          rb.last_published_generation == 0) {', '          !rb.snapshot_initialized) {')
        s = replace(s, '        submission_completed_);',
                    '        submission_completed_, rb.capture_generation[0], rb.capture_generation[1]);')
    path.write_text(s, newline='\n')

# A resolve readback buffer is used by its recorded graphics submission only.
# Do not idle the presentation queue when replacing a completed CPU readback.
path = sdk / 'src/graphics/vulkan/command_processor.cpp'
s = path.read_text()
if 'FIFA08_READBACK_EXACT_FENCE_V1' not in s:
    saved = out / 'vulkan-command_processor.cpp.before'
    if not saved.exists():
        shutil.copyfile(path, saved)
    start=s.index('bool VulkanCommandProcessor::IssueCopy_ReadbackResolvePath()')
    end=s.index('  bool is_scaled =',start)
    body=s[start:end]
    body=replace(body, '      if (!AwaitAllQueueOperationsCompletion()) {', '''      // FIFA08_READBACK_EXACT_FENCE_V1: only the old slot's producer owns it.
      const uint64_t producer = readback.submission_written[index];
      if (producer > submission_completed_) CheckSubmissionFenceAndDeviceLoss(producer);
      if (device_lost_ || submission_completed_ < producer) {''')
    path.write_text(s[:start]+body+s[end:],newline='\n')
print('Readback epochs and exact Vulkan replacement fence applied.')
