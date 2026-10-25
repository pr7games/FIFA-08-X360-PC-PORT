#pragma once
#include <cstdint>

namespace rex::ui::d3d12 {

// Bounded recovery for startup presentation stalls. Count guest images, not
// repeated UI paints, and never repeatedly reset a genuinely overloaded GPU.
class StartupRecovery {
 public:
  bool Observe(double seconds, uint64_t frames, int target_fps) {
    if (used_ || target_fps < 60) return false;
    if (!initialized_) {
      initialized_ = true;
      start_ = sample_time_ = seconds;
      sample_frames_ = frames;
      return false;
    }
    const double elapsed = seconds - sample_time_;
    if (elapsed < 1.0) return false;
    last_fps_ = double(frames - sample_frames_) / elapsed;
    sample_time_ = seconds;
    sample_frames_ = frames;
    if (seconds - start_ > 120.0) { used_ = true; return false; }
    if (seconds - start_ < 5.0 || elapsed > 2.0 ||
        last_fps_ < 5.0 || last_fps_ >= target_fps * 0.65) {
      slow_seconds_ = 0;
      return false;
    }
    slow_seconds_ += elapsed;
    if (slow_seconds_ < 3.0) return false;
    used_ = true;
    return true;
  }
  double last_fps() const { return last_fps_; }

 private:
  bool initialized_ = false, used_ = false;
  double start_ = 0, sample_time_ = 0, slow_seconds_ = 0, last_fps_ = 0;
  uint64_t sample_frames_ = 0;
};
}
