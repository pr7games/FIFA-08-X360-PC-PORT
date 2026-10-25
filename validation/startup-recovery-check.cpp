#include "../tools/rexglue-source/rexglue-sdk-0.10.0/include/rex/ui/d3d12/startup_recovery.h"
#include <cassert>
#include <cstdio>
using rex::ui::d3d12::StartupRecovery;
int main() {
  StartupRecovery healthy, stalled, transient, idle, cap30, late;
  int recoveries = 0;
  for (int t = 0; t <= 120; ++t) {
    assert(!healthy.Observe(t, t * 60, 60));
    if (stalled.Observe(t, t * 20, 60)) { assert(t == 7); ++recoveries; }
    assert(!idle.Observe(t, 0, 60));
    assert(!cap30.Observe(t, t * 20, 30));
  }
  assert(recoveries == 1);
  uint64_t frames = 0;
  for (int t = 0; t <= 20; ++t) {
    frames += (t == 6 || t == 7) ? 20 : 60;
    assert(!transient.Observe(t, frames, 60));
  }
  assert(!late.Observe(0, 0, 60));
  assert(!late.Observe(121, 2420, 60));
  for (int t = 122; t < 140; ++t) assert(!late.Observe(t, t * 20, 60));
  std::puts("PASS: one sustained startup recovery; none for normal cadence, transient dips, idle, 30 FPS cap or late stalls");
}
