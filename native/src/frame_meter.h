#pragma once
#include <atomic>
#include <cstdint>
#include <array>

// Counts guest requests to present a frame, not repeated host UI paints.
inline std::atomic<uint64_t> fifa08_frame_submissions{0};
inline std::atomic<uint32_t> fifa08_frontbuffer_width{0};
inline std::atomic<uint32_t> fifa08_frontbuffer_height{0};
inline std::atomic<uint32_t> fifa08_render_scale{1};
inline std::atomic<uint32_t> fifa08_target_fps{60};
inline std::atomic<uint32_t> fifa08_requested_interval{1};
inline std::atomic<uint32_t> fifa08_scene_flags{0};
inline std::atomic<uint64_t> fifa08_pacing_generation{0};
// Rolling submission intervals (not GPU timestamps or monitor refreshes).
inline std::array<std::atomic<uint32_t>, 240> fifa08_frame_intervals_us{};
inline std::atomic<uint32_t> fifa08_last_work_us{0};
inline std::atomic<uint32_t> fifa08_last_limit_wait_us{0};
void fifa08_start_timing();
void fifa08_apply_engine_experiments(uint8_t* base);
void fifa08_stop_timing();
