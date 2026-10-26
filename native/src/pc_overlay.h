#pragma once
#include <rex/ui/imgui_dialog.h>
#include <rex/ui/window.h>
#include "frame_meter.h"
#include <imgui.h>
#include <atomic>
#include <chrono>
#include <functional>
#include <memory>
#include <algorithm>
#include <vector>
#include <windows.h>
#include <cstdlib>

// Shared Segoe UI face, loaded once with the achievement UI atlas.
inline ImFont* fifa08_pc_menu_font = nullptr;

// Guest presentation requests are distinct from repeated host UI paints.
class PcOverlay final : public rex::ui::ImGuiDialog {
 public:
  PcOverlay(rex::ui::ImGuiDrawer* drawer, bool show_fps, rex::ui::Window* window,
            std::function<void()> exit)
      : ImGuiDialog(drawer), show_fps_(show_fps), window_(window), exit_(std::move(exit)) {
    SyncAttachment();
  }
  // Called on the UI thread. An invisible PC menu must not force UI-thread
  // presentation, which adds a scheduling hop and prevents direct guest paints.
  void ToggleMenu() { menu_.store(!menu_.load()); SyncAttachment(); }

 private:
  using Clock = std::chrono::steady_clock;
  void SyncAttachment() {
    const bool needed = show_fps_ || menu_.load();
    if (needed == attached_) return;
    if (needed) imgui_drawer()->AddDialog(this);
    else imgui_drawer()->RemoveDialog(this);
    attached_ = needed;
  }
  void OnDraw(ImGuiIO&) override {
    const auto now = Clock::now();
    if ((show_fps_ || menu_.load()) && now - last_request_ >= std::chrono::milliseconds(500)) {
      const uint64_t count = fifa08_frame_submissions.load(std::memory_order_relaxed);
      using GpuCount = uint64_t (*)();
      static GpuCount gpu_count = nullptr;
      if (!gpu_count) {
        if (auto module = GetModuleHandleW(L"rexgpu-xenosrd.dll"))
          gpu_count = reinterpret_cast<GpuCount>(GetProcAddress(module, "Fifa08GpuSubmittedImages"));
      }
      const uint64_t gpu_images = gpu_count ? gpu_count() : 0;
      using GpuInterval = uint32_t (*)(uint32_t);
      static auto gpu_interval = reinterpret_cast<GpuInterval>(GetProcAddress(
          GetModuleHandleW(L"rexgpu-xenosrd.dll"), "Fifa08GpuFrameIntervalUs"));
      const uint32_t scene = fifa08_scene_flags.load();
      if (scene != previous_scene_) {
        previous_scene_ = scene;
        gpu_window_start_ = gpu_images;
        gpu_p95_ms_ = gpu_max_ms_ = 0;
      }
      if (gpu_interval && gpu_images > gpu_window_start_) {
        std::vector<uint32_t> intervals;
        const uint64_t length = std::min<uint64_t>(240, gpu_images-gpu_window_start_);
        for (uint64_t i=0; i<length; ++i) {
          const uint32_t us = gpu_interval(uint32_t((gpu_images-i)%240));
          if (us) intervals.push_back(us);
        }
        if (!intervals.empty()) {
          std::sort(intervals.begin(), intervals.end());
          gpu_p95_ms_ = intervals[(intervals.size()-1)*95/100]/1000.0;
          gpu_max_ms_ = intervals.back()/1000.0;
        }
      }
      gpu_available_ = gpu_count != nullptr;
      if (initialized_) {
        fps_ = double(count - previous_count_) / std::chrono::duration<double>(now - last_request_).count();
        gpu_fps_ = double(gpu_images - previous_gpu_count_) / std::chrono::duration<double>(now - last_request_).count();
        ready_ = true;
        std::vector<uint32_t> samples;
        samples.reserve(fifa08_frame_intervals_us.size());
        for (const auto& value : fifa08_frame_intervals_us) {
          const auto us = value.load(std::memory_order_relaxed);
          if (us) samples.push_back(us);
        }
        if (!samples.empty()) {
          std::sort(samples.begin(), samples.end());
          p95_ms_ = samples[(samples.size() - 1) * 95 / 100] / 1000.0;
          max_ms_ = samples.back() / 1000.0;
        }
      }
      initialized_ = true;
      previous_count_ = count;
      previous_gpu_count_ = gpu_images;
      last_request_ = now;
    }
    if (show_fps_) {
      ImGui::SetNextWindowPos(ImVec2(14, 14), ImGuiCond_Always);
      ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 10.0f);
      ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
      ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(16.0f, 12.0f));
      ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(0.06f, 0.07f, 0.09f, 0.85f));
      ImGui::PushStyleColor(ImGuiCol_Border, ImVec4(0.0f, 0.0f, 0.0f, 0.0f));
      ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));

      constexpr auto flags = ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_AlwaysAutoResize |
          ImGuiWindowFlags_NoInputs | ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoFocusOnAppearing;
      if (ImGui::Begin("FPS##fifa08_pc", nullptr, flags)) {
        if (ready_) {
          ImGui::Text("FPS: %.1f", gpu_available_ ? gpu_fps_ : fps_);
        } else {
          ImGui::TextUnformatted("FPS: --");
        }
        const char* api = std::getenv("FIFA08_GRAPHICS_API");
        ImGui::Text("API: %s", api ? api : "D3D12");
        const auto scale = std::max(1u, fifa08_render_scale.load());
        const auto width = fifa08_frontbuffer_width.load();
        const auto height = fifa08_frontbuffer_height.load();
        ImGui::Text("BUFFER: %u x %u", width * scale, height * scale);
        static const char* selected_resolution = std::getenv("FIFA08_SELECTED_RESOLUTION");
        ImGui::Text("RESOLUCI\xC3\x93N: %s", selected_resolution ? selected_resolution : "--");
        int win_w = 0;
        int win_h = 0;
        if (window_) {
          win_w = int(window_->GetActualPhysicalWidth());
          win_h = int(window_->GetActualPhysicalHeight());
        }
        if (!win_w || !win_h) {
          win_w = REXCVAR_QUERY(int32_t, window_width);
          win_h = REXCVAR_QUERY(int32_t, window_height);
        }
        ImGui::Text("VENTANA: %d x %d", win_w, win_h);
        ImGui::TextUnformatted("F10 para abrir Menu");
      }
      ImGui::End();
      ImGui::PopStyleColor(3);
      ImGui::PopStyleVar(3);
    }
    if (menu_.load()) {
      const auto* viewport = ImGui::GetMainViewport();
      ImGui::SetNextWindowPos(viewport->GetCenter(), ImGuiCond_Always, ImVec2(0.5f, 0.5f));
      ImGui::SetNextWindowSize(ImVec2(std::min(380.0f,viewport->WorkSize.x-24.0f), 0.0f), ImGuiCond_Always);

      if (fifa08_pc_menu_font) ImGui::PushFont(fifa08_pc_menu_font);

      // Rounded PC menu, matching the launcher typography.
      ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 18.0f);
      ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 1.0f);
      ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(20.0f, 18.0f));
      ImGui::PushStyleVar(ImGuiStyleVar_FrameRounding, 12.0f);
      ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(16.0f, 12.0f));
      ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(10.0f, 10.0f));

      ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(0.10f, 0.11f, 0.14f, 0.95f));
      ImGui::PushStyleColor(ImGuiCol_Border, ImVec4(1.0f, 1.0f, 1.0f, 0.12f));
      ImGui::PushStyleColor(ImGuiCol_TitleBg, ImVec4(0.13f, 0.14f, 0.18f, 0.98f));
      ImGui::PushStyleColor(ImGuiCol_TitleBgActive, ImVec4(0.15f, 0.16f, 0.20f, 1.0f));
      ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.98f, 0.98f, 0.99f, 1.0f));

      // Checkbox style
      ImGui::PushStyleColor(ImGuiCol_FrameBg, ImVec4(0.18f, 0.20f, 0.25f, 0.85f));
      ImGui::PushStyleColor(ImGuiCol_FrameBgHovered, ImVec4(0.24f, 0.27f, 0.33f, 0.95f));
      ImGui::PushStyleColor(ImGuiCol_FrameBgActive, ImVec4(0.30f, 0.34f, 0.42f, 1.0f));
      ImGui::PushStyleColor(ImGuiCol_CheckMark, ImVec4(1.0f, 1.0f, 1.0f, 1.0f));

      ImGui::PushStyleColor(ImGuiCol_ButtonHovered,ImVec4(.65f,.12f,.17f,1));
      ImGui::PushStyleColor(ImGuiCol_ButtonActive,ImVec4(.48f,.09f,.13f,1));
      bool menu_open=true;
      ImGui::PushStyleColor(ImGuiCol_Separator,ImVec4(.35f,.35f,.38f,1));
      if (ImGui::Begin("FIFA 08 - Menu PC", &menu_open,
                       ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoCollapse)) {
        ImGui::SetWindowFontScale(1.0f);

        ImGui::Checkbox("Mostrar FPS en pantalla", &show_fps_);
        ImGui::Separator();
        ImGui::TextColored(ImVec4(0.70f, 0.73f, 0.78f, 1.0f), "Este menu no pausa el partido.");
        ImGui::Spacing();

        // Button 2: "Salir a Windows" (Refined Apple/FIFA Red Accent)
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.86f, 0.16f, 0.19f, 0.92f));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.96f, 0.22f, 0.26f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.72f, 0.11f, 0.14f, 1.0f));
        if (ImGui::Button("Salir a Windows", ImVec2(-1.0f, 42.0f))) {
          menu_.store(false);
          exit_();
        }
        ImGui::PopStyleColor(3);

        ImGui::SetWindowFontScale(1.0f);
      }
      ImGui::End();

      if (fifa08_pc_menu_font) ImGui::PopFont();
      if(!menu_open)menu_.store(false);
      ImGui::PopStyleColor(12);
      ImGui::PopStyleVar(6);
    }
    // RemoveDialog supports removal during the SDK's dialog draw loop.
    SyncAttachment();
  }
  rex::ui::Window* window_ = nullptr;
  bool attached_ = true; // ImGuiDialog constructor registers itself.
  bool show_fps_;
  std::atomic<bool> menu_{false};
  std::function<void()> exit_;
  bool initialized_ = false;
  bool ready_ = false;
  uint64_t previous_count_ = 0;
  uint64_t previous_gpu_count_ = 0;
  bool gpu_available_ = false;
  double gpu_fps_ = 0.0;
  double gpu_p95_ms_ = 0.0, gpu_max_ms_ = 0.0;
  uint32_t previous_scene_ = UINT32_MAX;
  uint64_t gpu_window_start_ = 0;
  double fps_ = 0.0;
  double p95_ms_ = 0.0;
  double max_ms_ = 0.0;
  Clock::time_point last_request_{};
};
