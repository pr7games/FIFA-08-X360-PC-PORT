#pragma once
#include <windows.h>
#include <rex/chrono/clock.h>
#include <cstdint>
#include "cpu_wait_policy.h"

// Only used by the measured timebase polling loop in sub_82276D50. The
// original guest comparison remains authoritative, including after waking.
// Never change the guest clock, target deadline, or simulation delta.
inline void fifa08_wait_guest_deadline(uint64_t remaining_ticks) {
  const auto ratio = rex::chrono::Clock::guest_tick_ratio();
  const auto host_hz = rex::chrono::Clock::QueryHostTickFrequency();
  if (!ratio.first || !host_hz) { fifa08_cpu_relax(); return; }
  const double remaining_us = static_cast<double>(remaining_ticks) *
      static_cast<double>(ratio.second) / static_cast<double>(ratio.first) *
      1000000.0 / static_cast<double>(host_hz);

  // If remaining time is over 2.5ms, sleep with Windows high-resolution timer
  constexpr double guard_us = 1500.0;
  if (remaining_us > 2500.0 && remaining_us <= 100000.0) {
    struct Timer {
      HANDLE handle = CreateWaitableTimerExW(nullptr, nullptr, 0x2 /*CREATE_WAITABLE_TIMER_HIGH_RESOLUTION*/, TIMER_ALL_ACCESS);
      ~Timer() { if (handle) CloseHandle(handle); }
    };
    thread_local Timer timer;
    if (timer.handle) {
      const double sleep_us = remaining_us - guard_us;
      LARGE_INTEGER due;
      due.QuadPart = -static_cast<LONGLONG>(sleep_us * 10.0);
      if (SetWaitableTimer(timer.handle, &due, 0, nullptr, nullptr, FALSE)) {
        WaitForSingleObject(timer.handle, INFINITE);
        return;
      }
    }
  }

  fifa08_cpu_relax();
}
