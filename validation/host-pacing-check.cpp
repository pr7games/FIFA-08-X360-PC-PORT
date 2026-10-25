#include "../native/src/host_frame_pacer.h"
#include <algorithm>
#include <cstdio>
#include <vector>

int main() {
  using Clock = std::chrono::steady_clock;
  for (int fps : {30, 60, 90, 120}) {
    Fifa08HostFramePacer pacer;
    std::vector<double> intervals;
    pacer.Wait(fps);
    auto previous = Clock::now();
    for (int i = 0; i < 32; ++i) {
      // Include a late frame and alternate calling threads. Neither should
      // reset the timeline or permit an early compensating frame.
      if (i == 12) std::this_thread::sleep_for(std::chrono::milliseconds(45));
      if (i % 2) {
        std::thread worker([&] { pacer.Wait(fps); });
        worker.join();
      } else {
        pacer.Wait(fps);
      }
      auto current = Clock::now();
      const double ms = std::chrono::duration<double, std::milli>(current - previous).count();
      intervals.push_back(ms);
      // Allow measurement / thread-join overhead variation, not burst frames.
      if (ms < 1000.0 / fps - 0.5) {
        std::fprintf(stderr, "Early frame at %d FPS: %.3f ms\n", fps, ms);
        return 1;
      }
      previous = current;
    }
    std::printf("%d FPS: minimum %.3f ms, maximum %.3f ms; no catch-up burst\n",
                fps, *std::min_element(intervals.begin(), intervals.end()),
                *std::max_element(intervals.begin(), intervals.end()));
  }
}
