#pragma once

#include <algorithm>
#include <chrono>
#include <rex/cvar.h>
#include <rex/logging.h>

namespace rex::ui::vulkan {

// Observe existing CPU fence waits without inserting any GPU operations.
class FenceWaitTiming {
 public:
  enum class Reason { Other, Resolve, Memexport, FrameReuse, ResourceReuse, Occlusion, OutputDescriptors, IntermediateImage, PaintPipeline, AcquireImage, Count };
  FenceWaitTiming(const char* role, const void* tracker,
                  Reason reason = Reason::Other, uint64_t fences = 1)
      : role_(role), tracker_(tracker), reason_(reason), fences_(fences) {
    // This diagnostic setting is selected before workers start, not hot-reloaded.
    static thread_local const bool enabled = REXCVAR_QUERY(bool, vulkan_log_fence_waits);
    enabled_ = enabled;
    if (enabled_) start_ = Clock::now();
  }
  ~FenceWaitTiming() {
    if (!enabled_) return;
    const auto now = Clock::now();
    const double ms = std::chrono::duration<double, std::milli>(now - start_).count();
    struct Stats {
      Clock::time_point start = Clock::now();
      uint64_t calls = 0, over_8ms = 0, fences = 0;
      double total = 0, maximum = 0;
    };
    static thread_local Stats all_stats[2][size_t(Reason::Count)];
    const size_t role_index = role_[0] == 'g' ? 0 : 1;
    Stats& stats = all_stats[role_index][size_t(reason_)];
    ++stats.calls;
    stats.fences += fences_;
    stats.over_8ms += ms > 8.0;
    stats.total += ms;
    stats.maximum = std::max(stats.maximum, ms);
    static thread_local Clock::time_point interval_start = Clock::now();
    if (now - interval_start < std::chrono::seconds(5)) return;
    constexpr const char* names[] = {"other", "resolve", "memexport", "frame_reuse",
                                    "resource_reuse", "occlusion", "output_descriptors",
                                    "intermediate_image", "paint_pipeline", "acquire_image"};
    for (size_t role = 0; role < 2; ++role) {
      for (size_t reason = 0; reason < size_t(Reason::Count); ++reason) {
        Stats& entry = all_stats[role][reason];
        if (!entry.calls) continue;
        REXLOG_INFO("VulkanFenceWait: role={} reason={} calls={} fences={} "
                    "total={:.3f}ms max={:.3f}ms over8ms={}",
                    role == 0 ? "guest" : "presenter", names[reason], entry.calls,
                    entry.fences, entry.total, entry.maximum, entry.over_8ms);
        entry = Stats{};
      }
    }
    interval_start = now;
  }
 private:
  using Clock = std::chrono::steady_clock;
  const char* role_;
  const void* tracker_;
  Reason reason_;
  uint64_t fences_;
  bool enabled_ = false;
  Clock::time_point start_{};
};

}  // namespace rex::ui::vulkan
