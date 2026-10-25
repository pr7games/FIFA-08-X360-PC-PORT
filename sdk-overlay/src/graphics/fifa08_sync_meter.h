#pragma once
#include <algorithm>
#include <array>
#include <chrono>
#include <rex/cvar.h>
#include <rex/logging.h>

namespace fifa08::sync_meter {
enum Category { kD3D12Frame, kD3D12Query, kVulkanQuery, kD3D12Resolve, kVulkanResolve, kD3D12Memexport, kVulkanMemexport, kD3D12AllocatorReset, kD3D12ListReset, kD3D12Record, kD3D12ListClose, kD3D12Submit, kD3D12Signal, kD3D12UIExecute, kD3D12Present, kCount };
class Scope {
 public:
  explicit Scope(Category category, bool relevant = true)
      : category_(category), enabled_(relevant && REXCVAR_QUERY(bool, gpu_log_sync_costs)) {
    if (enabled_) start_ = Clock::now();
  }
  ~Scope() { Finish(); }
  void Finish() {
    if (!enabled_) return;
    enabled_ = false;
    const auto now = Clock::now();
    const double ms = std::chrono::duration<double, std::milli>(now-start_).count();
    static thread_local std::array<Stats, kCount> stats;
    auto& s = stats[category_];
    if (!s.calls) s.started = now;
    ++s.calls;
    s.total += ms;
    s.maximum = std::max(s.maximum, ms);
    if (now-s.started >= std::chrono::seconds(5)) {
      constexpr const char* names[] = {"D3D12/frame_reuse", "D3D12/occlusion", "Vulkan/occlusion", "D3D12/resolve", "Vulkan/resolve", "D3D12/memexport", "Vulkan/memexport", "D3D12/allocator_reset", "D3D12/list_reset", "D3D12/record_commands", "D3D12/list_close", "D3D12/execute_lists", "D3D12/signal", "D3D12/ui_execute", "D3D12/present"};
      REXLOG_INFO("FIFA08 GPU sync cost: scope={} calls={} total={:.3f}ms max={:.3f}ms",
          names[category_], s.calls, s.total, s.maximum);
      s = {};
    }
  }
 private:
  using Clock = std::chrono::steady_clock;
  struct Stats { uint64_t calls = 0; double total = 0, maximum = 0; Clock::time_point started{}; };
  Category category_;
  bool enabled_;
  Clock::time_point start_{};
};
}
