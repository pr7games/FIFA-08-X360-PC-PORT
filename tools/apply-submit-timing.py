"""Opt-in CPU timing around D3D12 driver calls; no command ordering changes."""
from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[1]
sdk=root/'tools/rexglue-source/rexglue-sdk-0.10.0'
backup=root/'analysis/cpu-bottleneck/pass11/before'
backup.mkdir(parents=True,exist_ok=True)
meter=sdk/'src/graphics/fifa08_sync_meter.h'
text=meter.read_text()
if 'void Finish()' not in text:
    text=text.replace('  ~Scope() {','  ~Scope() { Finish(); }\n  void Finish() {',1)
    text=text.replace('    if (!enabled_) return;','    if (!enabled_) return;\n    enabled_ = false;',1)
categories=[('kD3D12AllocatorReset','D3D12/allocator_reset'),
 ('kD3D12ListReset','D3D12/list_reset'),('kD3D12Record','D3D12/record_commands'),
 ('kD3D12ListClose','D3D12/list_close'),('kD3D12Submit','D3D12/execute_lists'),
 ('kD3D12Signal','D3D12/signal'),('kD3D12UIExecute','D3D12/ui_execute'),
 ('kD3D12Present','D3D12/present')]
if categories[0][0] not in text:
    text=text.replace(', kCount };',', '+', '.join(x[0] for x in categories)+', kCount };',1)
    anchor='"Vulkan/memexport"};'
    assert anchor in text
    text=text.replace(anchor,'"Vulkan/memexport", '+', '.join('"'+x[1]+'"' for x in categories)+'};',1)
if meter.read_bytes()!=text.encode(): meter.write_bytes(text.encode())

path=sdk/'src/graphics/d3d12/command_processor.cpp'
text=path.read_text()
start=text.index('bool D3D12CommandProcessor::EndSubmission(')
end=text.index('\nbool D3D12CommandProcessor::',start+1)
body=text[start:end]
if 'FIFA08_DRIVER_TIMING_V1' not in body:
    shutil.copyfile(path,backup/'d3d12-command_processor.cpp')
    body=body.replace('{\n','{\n  // FIFA08_DRIVER_TIMING_V1\n',1)
    for call,category in [
      ('command_allocator->Reset();','kD3D12AllocatorReset'),
      ('command_list_->Reset(command_allocator, nullptr);','kD3D12ListReset'),
      ('deferred_command_list_.Execute(command_list_, command_list_1_);','kD3D12Record'),
      ('command_list_->Close();','kD3D12ListClose'),
      ('direct_queue->ExecuteCommandLists(1, execute_command_lists);','kD3D12Submit'),
      ('direct_queue->Signal(submission_fence_, submission_current_++);','kD3D12Signal')]:
        anchor='    '+call
        assert body.count(anchor)==1,call
        body=body.replace(anchor,'    {\n      fifa08::sync_meter::Scope phase(fifa08::sync_meter::'+category+');\n      '+call+'\n    }',1)
    text=text[:start]+body+text[end:]
    path.write_bytes(text.encode())

path=sdk/'src/ui/d3d12/d3d12_presenter.cpp'
text=path.read_text()
if 'FIFA08_PRESENT_TIMING_V1' not in text:
    shutil.copyfile(path,backup/'d3d12_presenter.cpp')
    at=text.index('#include ')
    text=text[:at]+'#include "../../graphics/fifa08_sync_meter.h"\n'+text[at:]
    anchor='  provider_.GetDirectQueue()->ExecuteCommandLists(1, &execute_command_list);'
    assert text.count(anchor)==1
    text=text.replace(anchor,'  {\n    fifa08::sync_meter::Scope phase(fifa08::sync_meter::kD3D12UIExecute);\n  '+anchor+'\n  }',1)
    anchor='  const bool presentation_vsync = REXCVAR_QUERY(bool, vsync);'
    assert text.count(anchor)==1
    text=text.replace(anchor,'  // FIFA08_PRESENT_TIMING_V1\n  fifa08::sync_meter::Scope present_measure(fifa08::sync_meter::kD3D12Present);\n'+anchor,1)
    start=text.index(anchor)
    end=text.index('\n  // Even if',start)
    text=text[:end]+'\n  present_measure.Finish();'+text[end:]
    path.write_bytes(text.encode())
print('D3D12 driver timing installed: reset/record/close/execute/signal and UI execute/present; existing opt-in flag.')
