#pragma once
#include <array>
#include <chrono>
#include <cstdint>
#include <rex/cvar.h>
#include <rex/logging.h>

namespace fifa08 {
enum class ResolveDecision { kQueued, kCoalesced, kCold };
// Aggregated telemetry only, under the launcher's existing sync-cost switch.
inline void RecordResolveDecision(bool vulkan, ResolveDecision decision) {
  if (!REXCVAR_QUERY(bool, gpu_log_sync_costs)) return;
  using Clock = std::chrono::steady_clock;
  struct Counters {
    uint64_t calls=0, queued=0, coalesced=0, cold=0;
    Clock::time_point started{};
  };
  static thread_local std::array<Counters, 2> counters;
  auto& c = counters[vulkan ? 1 : 0];
  if (!c.calls) c.started=Clock::now();
  ++c.calls;
  if (decision==ResolveDecision::kQueued) ++c.queued;
  else if (decision==ResolveDecision::kCoalesced) ++c.coalesced;
  else ++c.cold;
  if ((c.calls & 127u) || Clock::now()-c.started<std::chrono::seconds(5)) return;
  REXLOG_INFO("FIFA08 async resolve: api={} calls={} queued={} coalesced={} cold_deferred={}",
      vulkan ? "Vulkan" : "D3D12", c.calls, c.queued, c.coalesced, c.cold);
  c={};
}
}
