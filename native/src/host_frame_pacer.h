#pragma once
#include <windows.h>
#include <chrono>
#include <cstdint>
#include <mutex>
#include <thread>

// One timeline for all submission threads. Keep deadlines on a fixed cadence
// so small wake-up delays do not accumulate against the display's cadence.
// A whole missed interval resets the schedule; never emit a catch-up burst.
class Fifa08HostFramePacer {
 public:
  ~Fifa08HostFramePacer() { if (timer_) CloseHandle(timer_); }
  void Wait(int fps) {
    if (fps <= 0) return;
    std::lock_guard lock(mutex_);
    const auto interval = std::chrono::duration_cast<Clock::duration>(
        std::chrono::duration<double>(1.0 / static_cast<double>(fps)));
    const auto now = Clock::now();
    if (next_deadline_ == Clock::time_point{} || now > next_deadline_ + interval) {
      next_deadline_ = now + interval;
      return;
    }
    // Block on a high-resolution waitable timer for the coarse interval.
    // Keep a short guard for scheduling latency, then recheck the deadline.
    // This shortens the old 2 ms yield window to a 1.5 ms guard.
    if (!timer_attempted_) {
      timer_attempted_ = true;
      timer_ = CreateWaitableTimerExW(nullptr, nullptr,
          CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
    }
    while (true) {
      const auto remaining = next_deadline_ - Clock::now();
      if (remaining <= Clock::duration::zero()) break;
      if (timer_ && remaining > std::chrono::milliseconds(2)) {
        const auto sleep = remaining - std::chrono::microseconds(1500);
        LARGE_INTEGER due;
        due.QuadPart = -std::chrono::duration_cast<
            std::chrono::duration<int64_t, std::ratio<1, 10000000>>>(sleep).count();
        if (SetWaitableTimer(timer_, &due, 0, nullptr, nullptr, FALSE) &&
            WaitForSingleObject(timer_, INFINITE) == WAIT_OBJECT_0) continue;
      }
      // Preserve a working coarse fallback on older Windows versions.
      if (remaining > std::chrono::milliseconds(2)) {
        std::this_thread::sleep_for(remaining - std::chrono::milliseconds(2));
      } else if (remaining > std::chrono::microseconds(50)) {
        std::this_thread::yield();
      } else {
        YieldProcessor();
      }
    }
    next_deadline_ += interval;
  }

 private:
  using Clock = std::chrono::steady_clock;
  std::mutex mutex_;
  HANDLE timer_ = nullptr;
  bool timer_attempted_ = false;
  Clock::time_point next_deadline_{};
  std::chrono::nanoseconds last_period_{};
};
