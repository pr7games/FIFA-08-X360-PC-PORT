#pragma once
#include <windows.h>
#include <cstdint>
#if defined(__x86_64__) || defined(_M_X64)
#include <immintrin.h>
#endif

// The measured GPU-progress poll can occupy a full core while the command
// worker / driver needs to run. Back off only while progress stays unchanged;
// retain every guest progress, timeout, and fence condition.
inline void fifa08_gpu_poll_yield(uintptr_t address, uint32_t progress) noexcept {
  struct PollState {
    uintptr_t address = 0;
    uint32_t progress = 0;
    unsigned polls = 0;
  };
  static thread_local PollState state;
  if (state.address != address || state.progress != progress) {
    state.address = address;
    state.progress = progress;
    state.polls = 0;
  }
  // Keep short waits responsive. Only back off after repeated observations
  // of the same progress word; the guest re-reads it and its timeout on return.
  if (state.polls < 256) ++state.polls;
#if defined(__x86_64__) || defined(_M_X64)
  // Bounded hardware hints instead of a timed OS sleep: measured timer wakeups
  // had long outliers and could introduce stutter. Cap at 4 PAUSEs per poll
  // to limit additional progress-detection latency in short cinematic waits.
  const unsigned level = state.polls >> 6;
  const unsigned pauses = 1u << (level < 2 ? level : 2);
  for (unsigned i = 0; i < pauses; ++i) _mm_pause();
#endif
  // Keep the scheduler yield periodic even after the backoff saturates.
  static thread_local unsigned yield_polls = 0;
  if ((++yield_polls & 63u) == 0) SwitchToThread();
}

#if defined(__x86_64__) || defined(_M_X64)
#include <immintrin.h>
inline void fifa08_cpu_relax() noexcept { _mm_pause(); }
#else
#include <atomic>
inline void fifa08_cpu_relax() noexcept {
  std::atomic_signal_fence(std::memory_order_seq_cst);
}
#endif
