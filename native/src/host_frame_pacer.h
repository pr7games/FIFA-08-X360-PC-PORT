#pragma once
#include <windows.h>
#include <chrono>
#include <cstdint>
#include <mutex>
#include <thread>

// One fixed-cadence timeline for guest submissions. A whole missed interval
// rebases the timeline rather than releasing a burst of catch-up frames.
class Fifa08HostFramePacer {
 public:
  ~Fifa08HostFramePacer() { if (timer_) CloseHandle(timer_); }
  void Wait(int fps) {
    std::lock_guard lock(mutex_);
    if (fps <= 0) {
      next_deadline_ = {};
      last_fps_ = 0;
      return;
    }
    const auto interval = std::chrono::duration_cast<Clock::duration>(
        std::chrono::duration<double>(1.0 / static_cast<double>(fps)));
    const auto now = Clock::now();
    if (fps != last_fps_ || next_deadline_ == Clock::time_point{} ||
        now >= next_deadline_ + interval) {
      last_fps_ = fps;
      next_deadline_ = now + interval;
      return;
    }
    if (!timer_attempted_) {
      timer_attempted_ = true;
      timer_ = CreateWaitableTimerExW(nullptr, nullptr,
          CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
    }
    while (true) {
      const auto remaining = next_deadline_ - Clock::now();
      if (remaining <= Clock::duration::zero()) break;
      // Sleep most of the interval. Do not hand the final deadline to
      // SwitchToThread: another runnable thread may consume a full timeslice.
      constexpr auto guard = std::chrono::microseconds(500);
      if (timer_ && remaining > guard) {
        const auto sleep = remaining - guard;
        LARGE_INTEGER due;
        due.QuadPart = -std::chrono::duration_cast<
            std::chrono::duration<int64_t, std::ratio<1, 10000000>>>(sleep).count();
        if (SetWaitableTimer(timer_, &due, 0, nullptr, nullptr, FALSE) &&
            WaitForSingleObject(timer_, INFINITE) == WAIT_OBJECT_0) continue;
        // Failed timers use the same coarse fallback as unsupported systems.
        CloseHandle(timer_);
        timer_ = nullptr;
      }
      if (!timer_ && remaining > std::chrono::milliseconds(2)) {
        std::this_thread::sleep_for(remaining - std::chrono::milliseconds(2));
      } else {
        YieldProcessor();
      }
    }
    next_deadline_ += interval;
    // Also recover if Windows stalled the thread during the wait itself.
    const auto completed = Clock::now();
    if (completed >= next_deadline_) next_deadline_ = completed + interval;
  }

 private:
  using Clock = std::chrono::steady_clock;
  std::mutex mutex_;
  HANDLE timer_ = nullptr;
  bool timer_attempted_ = false;
  Clock::time_point next_deadline_{};
  int last_fps_ = 0;
};
