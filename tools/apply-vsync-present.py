"""Add bounded telemetry for the nonblocking VSync present path."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'tools/rexglue-source/rexglue-sdk-0.10.0/src/ui/d3d12/d3d12_presenter.cpp'
s=p.read_text()
if 'FIFA08_PRESENT_ASYNC_STATS_V1' not in s:
    anchor='  present_measure.Finish();\n'
    assert s.count(anchor)==1
    s=s.replace(anchor,anchor+"""  // FIFA08_PRESENT_ASYNC_STATS_V1: distinguish accepted frames from retries.
  if (REXCVAR_QUERY(bool, gpu_log_sync_costs)) {
    struct PresentStats {
      uint64_t calls = 0, accepted = 0, busy = 0, occluded = 0, errors = 0, fallbacks = 0;
      std::chrono::steady_clock::time_point start{};
    };
    static thread_local PresentStats stats;
    const auto now = std::chrono::steady_clock::now();
    if (!stats.calls) stats.start = now;
    ++stats.calls;
    if (present_result == DXGI_ERROR_WAS_STILL_DRAWING) ++stats.busy;
    else if (present_result == DXGI_STATUS_OCCLUDED) ++stats.occluded;
    else if (SUCCEEDED(present_result)) ++stats.accepted;
    else ++stats.errors;
    if (was_nonblocking_supported && !paint_context_.nonblocking_present_supported)
      ++stats.fallbacks;
    if (now - stats.start >= std::chrono::seconds(5)) {
      REXLOG_INFO("FIFA08 DXGI present: vsync={} nonblocking={} calls={} accepted={} busy={} occluded={} errors={} fallbacks={}",
          presentation_vsync, paint_context_.nonblocking_present_supported,
          stats.calls, stats.accepted, stats.busy, stats.occluded, stats.errors, stats.fallbacks);
      stats = {};
    }
  }
""",1)
    p.write_bytes(s.encode())
print('VSync present: nonblocking retry statistics installed; display interval and tearing policy preserved.')
