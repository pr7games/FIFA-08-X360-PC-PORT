#pragma once
#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <cstdint>

// Counts newly submitted game images, not repeated UI paints or CPU requests.
// It measures submission cadence, not physical display scanout.
namespace fifa08::gpu_meter {
inline std::atomic<uint64_t> submitted{0};
inline std::array<std::atomic<uint32_t>, 240> intervals{};
inline void Record() {
  using Clock = std::chrono::steady_clock;
  static thread_local Clock::time_point previous{};
  const auto now = Clock::now();
  const auto index = submitted.load(std::memory_order_relaxed) + 1;
  if (previous != Clock::time_point{}) {
    const auto us = std::chrono::duration_cast<std::chrono::microseconds>(now-previous).count();
    intervals[index % intervals.size()].store(uint32_t(std::min<int64_t>(us, UINT32_MAX)),
                                               std::memory_order_relaxed);
  }
  previous = now;
  submitted.store(index, std::memory_order_release);
}
}
