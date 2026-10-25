"""Apply the reviewed Vulkan presentation subset of ReStreet's BSD SDK patch."""
import hashlib
import json
import pathlib
import re

project = pathlib.Path(__file__).resolve().parents[1]
root = project / 'tools/rexglue-source/rexglue-sdk-0.10.0'
patch = project / 'patches/restreet-vulkan-present.patch'
marker = project / 'analysis/restreet-vulkan-applied.json'

def isolate_presentation_queue(files):
    device_file = 'src/ui/vulkan/vulkan_device.cpp'
    presenter_file = 'src/ui/vulkan/vulkan_presenter.cpp'
    header_file = 'include/rex/ui/vulkan/presenter.h'
    targets = {file: (root / file).read_text() for file in (device_file, presenter_file, header_file)}
    replacements = {
        device_file: [(
            '  device->queue_families_[device->queue_family_graphics_compute_].queues.resize(std::max(\n      size_t(1), device->queue_families_[device->queue_family_graphics_compute_].queues.size()));',
            '  // FIFO or a driver/compositor can block vkQueuePresentKHR even in\n'
            '  // immediate mode. Reserve a second queue so this never holds the\n'
            '  // guest rendering queue mutex on devices supporting two queues.\n'
            '  const size_t graphics_queue_count = with_swapchain\n'
            '      ? std::min(size_t(2), size_t(queue_families[device->queue_family_graphics_compute_].queueCount))\n'
            '      : size_t(1);\n'
            '  device->queue_families_[device->queue_family_graphics_compute_].queues.resize(std::max(\n'
            '      graphics_queue_count, device->queue_families_[device->queue_family_graphics_compute_].queues.size()));')],
        header_file: [],
        presenter_file: [
            ('  present_queue_family_out = queue_family_index_present;',
             '  present_queue_family_out = queue_family_index_present;\n'
             '  // Draw submissions stay on queue 0, preserving guest-image ordering.\n'
             '  // The per-image render-finished semaphore synchronizes presentation\n'
             '  // on queue 1 within the same family; no ownership transfer is needed.\n'
             '  const uint32_t present_queue_index = queue_family_index_present == vulkan_device->queue_family_graphics_compute() &&\n'
             '      vulkan_device->queue_families()[queue_family_index_present].queues.size() > 1 ? 1 : 0;\n'
             '  REXLOG_INFO("FIFA08 Vulkan queues: render=0, present={}, family={}, independent={}",\n'
             '      present_queue_index, queue_family_index_present, present_queue_index != 0);'),
            ('vulkan_device->AcquireQueue(present_queue_family, 0);',
             'vulkan_device->AcquireQueue(present_queue_family, present_queue_family == vulkan_device->queue_family_graphics_compute() && vulkan_device->queue_families()[present_queue_family].queues.size() > 1 ? 1 : 0);'),
            ('vulkan_device_->AcquireQueue(paint_context_.present_queue_family, 0);',
             'vulkan_device_->AcquireQueue(paint_context_.present_queue_family, paint_context_.present_queue_family == vulkan_device_->queue_family_graphics_compute() && vulkan_device_->queue_families()[paint_context_.present_queue_family].queues.size() > 1 ? 1 : 0);')]
    }
    for file, changes in replacements.items():
        for before, after in changes:
            if targets[file].count(before) != 1:
                raise RuntimeError(f'Queue isolation context mismatch: {file}: {before[:60]}')
            targets[file] = targets[file].replace(before, after, 1)
    for file, text in targets.items():
        (root / file).write_text(text, newline='\n')
        files[file] = hashlib.sha256((root / file).read_bytes()).hexdigest()
digest = hashlib.sha256(patch.read_bytes()).hexdigest()
if marker.exists():
    record = json.loads(marker.read_text())
    if record['patch_sha256'] != digest:
        raise RuntimeError('Reviewed patch changed; inspect before reapplying')
    for file, expected in record['files'].items():
        if hashlib.sha256((root / file).read_bytes()).hexdigest() != expected:
            raise RuntimeError(f'Patched source changed: {file}')
    if record.get('fifa08_queue_isolation') != 1:
        isolate_presentation_queue(record['files'])
        record['fifa08_queue_isolation'] = 1
        marker.write_text(json.dumps(record, indent=2))
    print('ReStreet Vulkan presentation patch already applied.')
    raise SystemExit(0)
pending = {}
for section in re.split(r'(?=^diff --git )', patch.read_text().split('\x00')[0], flags=re.M):
    if not section.startswith('diff --git '):
        continue
    file = section.splitlines()[0].split(' b/', 1)[1]
    target = root / file
    text = target.read_text() if target.exists() else ''
    for match in re.finditer(r'^@@[^\n]*\n(.*?)(?=^@@|\Z)', section, flags=re.M | re.S):
        lines = match.group(1).splitlines(keepends=True)
        before = ''.join(line[1:] for line in lines if line.startswith((' ', '-')))
        after = ''.join(line[1:] for line in lines if line.startswith((' ', '+')))
        if not before:
            if text:
                raise RuntimeError(f'Unexpected existing new file: {file}')
            text = after
        else:
            if text.count(before) != 1:
                raise RuntimeError(f'Hunk mismatch in {file}: {before[:100]!r}')
            text = text.replace(before, after, 1)
    pending[file] = text
# All contexts must match before any source is modified.
for file, text in pending.items():
    target = root / file
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, newline='\n')
record = {'patch_sha256': digest, 'files': {file: hashlib.sha256((root / file).read_bytes()).hexdigest() for file in pending}}
isolate_presentation_queue(record['files'])
record['fifa08_queue_isolation'] = 1
marker.write_text(json.dumps(record, indent=2))
print(f'Applied {len(pending)} reviewed Vulkan presentation files.')
