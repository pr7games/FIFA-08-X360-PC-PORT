"""Resolve the measured D3D12 Present / shared-queue head-of-line stall."""
from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[1]
sdk=root/'tools/rexglue-source/rexglue-sdk-0.10.0'
backup=root/'analysis/cpu-bottleneck/pass12/before'
backup.mkdir(parents=True,exist_ok=True)
def save(path,text):
    if path.read_bytes()!=text.encode():
        dest=backup/path.relative_to(sdk)
        if not dest.exists():
            dest.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(path,dest)
        path.write_bytes(text.encode())
policy=sdk/'src/ui/d3d12/fifa08_present_retry.h'
source=(root/'native/src/present_retry_policy.h').read_bytes()
if not policy.exists() or policy.read_bytes()!=source: policy.write_bytes(source)

path=sdk/'include/rex/ui/presenter.h'
text=path.read_text()
if 'kNotPresentedRetry' not in text:
    anchor='    kGpuLostResponsible,\n'
    assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'    // Busy host presentation; preserve connection and retry at a UI tick.\n    kNotPresentedRetry,\n',1)
save(path,text)

path=sdk/'include/rex/ui/d3d12/d3d12_presenter.h'
text=path.read_text()
if 'nonblocking_present_supported' not in text:
    anchor='    bool swap_chain_allows_tearing = false;'
    assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'\n    bool nonblocking_present_supported = true;',1)
save(path,text)

path=sdk/'src/ui/d3d12/d3d12_presenter.cpp'
text=path.read_text()
if 'FIFA08_NONBLOCKING_PRESENT_V1' not in text:
    at=text.index('#include ')
    text=text[:at]+'#include "fifa08_present_retry.h"\n'+text[at:]
    start=text.index('  HRESULT present_result = paint_context_.swap_chain->Present(')
    end=text.index('\n  present_measure.Finish();',start)
    text=text[:start]+'''  // FIFA08_NONBLOCKING_PRESENT_V1
  const bool was_nonblocking_supported = paint_context_.nonblocking_present_supported;
  HRESULT present_result = fifa08::PresentWithoutQueueStall(
      presentation_vsync, paint_context_.swap_chain_allows_tearing,
      paint_context_.nonblocking_present_supported,
      [&](UINT interval, UINT flags) { return paint_context_.swap_chain->Present(interval, flags); });
  if (was_nonblocking_supported && !paint_context_.nonblocking_present_supported)
    REXLOG_WARN("D3D12Presenter: nonblocking Present unsupported; retaining original presentation path");
'''.rstrip()+text[end:]
    anchor='  switch (present_result) {\n'
    assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'    case DXGI_ERROR_WAS_STILL_DRAWING:\n      return PaintResult::kNotPresentedRetry;\n',1)
save(path,text)

path=sdk/'src/ui/presenter.cpp'
text=path.read_text()
if 'FIFA08_PRESENT_RETRY_V1' not in text:
    anchor='      if (surface_paint_connection_state_ == SurfacePaintConnectionState::kConnectedOutdated) {'
    start=text.index('void Presenter::PaintFromUIThread(')
    at=text.index(anchor,text.index('paint_result = PaintAndPresent(draw_ui);',start))
    text=text[:at]+'''      // FIFA08_PRESENT_RETRY_V1: no reset, successful-frame accounting, or
      // immediate busy loop for DXGI backpressure. Keep the last image valid.
      if (paint_result == PaintResult::kNotPresentedRetry) {
        request_repaint_at_tick = true;
        if (coalescing) coalescing->forced.store(true, std::memory_order_relaxed);
      }
'''+text[at:]
    anchor='          paint_result = PaintAndPresent(false);\n'
    assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'''          if (paint_result == PaintResult::kNotPresentedRetry) {
            if (REXCVAR_GET(host_coalesce_guest_paints))
              Fifa08PaintState(this)->forced.store(true, std::memory_order_relaxed);
            RequestPaintOrConnectionRecoveryViaWindow(false);
          }
''',1)
save(path,text)
print('D3D12 nonblocking Present: deferred busy retry, VSync preserved, explicit unsupported fallback, GPU-loss handling retained.')
