#pragma once
#include <cstdint>

namespace fifa08 {
// Prefer the newest complete snapshot. A pending slot is never CPU-readable,
// including when it belongs to the still-open command submission.
constexpr uint32_t SelectCompletedReadback(uint64_t a, uint64_t b,
    bool valid_a, bool valid_b, uint64_t completed,
    uint64_t generation_a = 0, uint64_t generation_b = 0) {
  const bool ready_a = valid_a && a && a <= completed;
  const bool ready_b = valid_b && b && b <= completed;
  return ready_a && (!ready_b || a > b ||
      (a == b && generation_a >= generation_b)) ? 0u : ready_b ? 1u : 2u;
}
struct ReadbackPlan {
  uint32_t index;
  bool cache_miss;
  bool copy;
};
constexpr ReadbackPlan PlanResolveReadback(uint32_t write, bool delayed,
    bool copy_only_on_miss, uint32_t bytes, bool previous_mapped,
    uint32_t previous_capacity, uint32_t previous_written, uint64_t previous_submission) {
  const bool previous_valid = previous_mapped && previous_submission &&
      bytes <= previous_capacity && bytes <= previous_written;
  const bool miss = delayed && !previous_valid;
  return {delayed && previous_valid ? 1u-write : write, miss,
          !copy_only_on_miss || miss};
}
// The CPU consumer cannot run before the exact producer has completed.
template<class Wait, class Consume>
bool ConsumeCompletedReadback(uint64_t producer, Wait&& wait, Consume&& consume) {
  if (!producer || !wait(producer)) return false;
  return consume();
}
constexpr bool CanReleaseReadback(uint64_t a, uint64_t b, uint64_t completed) {
  return a <= completed && b <= completed;
}
template<class A, class B>
bool SameReadbackRanges(const A& a, const B& b) {
  if (a.size() != b.size()) return false;
  for (decltype(a.size()) i = 0; i < a.size(); ++i) {
    if (a[i].base_address_dwords != b[i].base_address_dwords ||
        a[i].size_bytes != b[i].size_bytes) return false;
  }
  return true;
}
// Cache keys include the exact byte length, so oversized growth slack is not
// reused by later larger requests. Keep only a small allocation quantum.
constexpr uint32_t CachedReadbackAllocationSize(uint32_t bytes) {
  const uint64_t aligned = (uint64_t(bytes) + 262143u) & ~uint64_t(262143u);
  return aligned <= 0xffffffffull ? uint32_t(aligned) : 0;
}
}
