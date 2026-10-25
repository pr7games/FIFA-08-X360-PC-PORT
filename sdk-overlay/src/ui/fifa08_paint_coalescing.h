#pragma once
#include <atomic>
#include <cstdint>

namespace fifa08::paint {
// One producer, one UI consumer. Scheduling only: never skip new guest frames,
// forced repaints, idle UI recovery, or the rendering of the game's image.
struct State {
  std::atomic<uint64_t> published{0};
  std::atomic<int64_t> active_at_ns{0};
  std::atomic<bool> forced{false};
  uint64_t pending = 0, presented = 0, skipped = 0, paints = 0;
  int64_t painted_at_ns = 0, logged_at_ns = 0;

  void Publish(bool active, int64_t now) {
    active_at_ns.store(active ? now : 0, std::memory_order_relaxed);
    published.fetch_add(1, std::memory_order_release);
  }
  bool Skip(bool force, int64_t now) {
    pending = published.load(std::memory_order_acquire);
    const bool urgent = forced.exchange(false, std::memory_order_relaxed);
    const int64_t active = active_at_ns.load(std::memory_order_relaxed);
    return !force && !urgent && pending && pending == presented && active > 0 &&
        now >= active && now - active < 250000000 &&
        now >= painted_at_ns && now - painted_at_ns < 100000000;
  }
  void Presented(int64_t now) {
    presented = pending;
    painted_at_ns = now;
    ++paints;
  }
};
} // namespace fifa08::paint
