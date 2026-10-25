// fifa08 - ReXGlue Recompiled Project
//
// Customize your app by overriding virtual hooks from rex::ReXApp.

#pragma once

#include <rex/rex_app.h>
#include <rex/ui/window.h>
#include <rex/ui/ui_event.h>
#include <rex/ui/keybinds.h>
#include <rex/system/gpu_plugin.h>
#include <rex/cvar.h>
#include <rex/memory.h>
#include "output_capture.h"
#include "pc_overlay.h"
#include "fifa08_native_render.h"
#include "achievements_pc.h"
#if defined(_WIN32)
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#endif

class Fifa08App : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  static std::unique_ptr<rex::ui::WindowedApp> Create(
      rex::ui::WindowedAppContext& ctx) {
    return std::unique_ptr<Fifa08App>(new Fifa08App(ctx, "FIFA 08 PORT PC",
        PPCImageConfig));
  }

  void OnConfigureFonts(ImFontAtlas* atlas) override {
    wchar_t windows[MAX_PATH]{};GetWindowsDirectoryW(windows,MAX_PATH);
    auto path=std::filesystem::path(windows)/"Fonts"/"segoeui.ttf";
    if(std::filesystem::exists(path))fifa08::achievements::presentation::font=atlas->AddFontFromFileTTF(path.string().c_str(),18.f);
    if(!fifa08::achievements::presentation::font){ImFontConfig cfg;cfg.SizePixels=18.f;fifa08::achievements::presentation::font=atlas->AddFontDefault(&cfg);}
    fifa08_pc_menu_font = fifa08::achievements::presentation::font;
  }

  void OnPreSetup(rex::RuntimeConfig& config) override {
#if defined(FIFA08_REQUIRE_AVX2)
    if (!IsProcessorFeaturePresent(PF_AVX2_INSTRUCTIONS_AVAILABLE)) {
      MessageBoxW(nullptr, L"Esta compilacion de FIFA 08 requiere una CPU con AVX2.",
                  L"FIFA 08 - CPU incompatible", MB_OK | MB_ICONERROR);
      std::exit(EXIT_FAILURE);
    }
#endif
    config.gpu_plugin = "xenos";
    const char* api = std::getenv("FIFA08_GRAPHICS_API");
    std::string_view backend = (api && (_stricmp(api, "Vulkan") == 0 || _stricmp(api, "VK") == 0)) ? "vulkan" : "d3d12";
    config.graphics = rex::system::LoadGpuPlugin(config.gpu_plugin, backend);
    if (!config.graphics) {
      REXLOG_ERROR("FIFA08: failed to load requested graphics API: {}", backend);
      std::exit(EXIT_FAILURE);
    }
    REXLOG_INFO("FIFA08 graphics API selected: {}", backend);

    // Honor the launcher profile, matching the clean reference executable.
    rex::cvar::ApplyEnvironment();
  }

  void OnPostLaunchModule(rex::system::XThread*) override {
    capture_.Start(runtime()->graphics_system()->presenter());
    achievement_export_.Start(runtime());
  }

  std::unique_ptr<rex::ui::ImGuiDialog> CreateAchievementsOverlay() override {
    if(!imgui_drawer()||!immediate_drawer()||!runtime())return nullptr;
    return std::make_unique<fifa08::achievements::Panel>(imgui_drawer(),immediate_drawer(),runtime(),[this]{app_context().CallInUIThreadDeferred([this]{CloseAchievementsOverlay();});});
  }

  std::unique_ptr<rex::ui::AchievementNotificationDialog> CreateAchievementNotificationDialog() override {
    if(!imgui_drawer()||!immediate_drawer()||!runtime())return nullptr;
    auto* context=&app_context();
    return std::make_unique<fifa08::achievements::Toast>(imgui_drawer(),immediate_drawer(),runtime(),[context](auto fn){context->CallInUIThreadDeferred(std::move(fn));});
  }

  void OnPreLaunchModule() override {
    fifa08_apply_engine_experiments(runtime()->memory()->virtual_membase());
  }

  void OnPostSetup() override {
    fifa08_start_timing();
    fifa08::native_render::Install();
    fifa08_render_scale.store(REXCVAR_QUERY(int32_t, draw_resolution_scale_x));
    REXLOG_INFO("FIFA08 graphics: target path D3D12={}, Vulkan={}",
        REXCVAR_QUERY(std::string, render_target_path_d3d12),
        REXCVAR_QUERY(std::string, render_target_path_vulkan));
    REXLOG_INFO("FIFA08 settings: language={}, async_shaders={}, clear_pages={}, readback_resolve={}",
        REXCVAR_QUERY(uint32_t, user_language),
        REXCVAR_QUERY(bool, async_shader_compilation),
        REXCVAR_QUERY(bool, clear_memory_page_state),
        REXCVAR_QUERY(std::string, readback_resolve));
    REXLOG_INFO("FIFA08 presentation: vsync={}, post_effect={}, host_rendering_thread={}",
        REXCVAR_QUERY(bool, vsync), REXCVAR_QUERY(std::string, swap_post_effect),
        REXCVAR_QUERY(bool, host_present_from_non_ui_thread));
    REXLOG_INFO("FIFA08 display: fullscreen={}, window={}x{}, render_scale={}",
        window()->IsFullscreen(),
        REXCVAR_QUERY(int32_t, window_width),
        REXCVAR_QUERY(int32_t, window_height),
        REXCVAR_QUERY(int32_t, resolution_scale));

#if defined(_WIN32)
    HWND hwnd = static_cast<HWND>(window()->GetNativeWindowHandle());
    if (hwnd) {
      HICON icon_big = (HICON)LoadImageW(GetModuleHandleW(nullptr), MAKEINTRESOURCEW(1),
          IMAGE_ICON, 0, 0, LR_DEFAULTSIZE | LR_SHARED);
      if (!icon_big) {
        icon_big = (HICON)LoadImageW(nullptr, L"launcher-assets\\icon.ico",
            IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
      }
      if (icon_big) {
        SendMessageW(hwnd, WM_SETICON, ICON_BIG, reinterpret_cast<LPARAM>(icon_big));
        SendMessageW(hwnd, WM_SETICON, ICON_SMALL, reinterpret_cast<LPARAM>(icon_big));
      }
      SetWindowTextW(hwnd, L"FIFA 08 PORT PC");
      window()->SetTitle("FIFA 08 PORT PC");
    }
#endif
  }

  void OnKeyDown(rex::ui::KeyEvent& e) override {
    if (e.virtual_key() == rex::ui::VirtualKey::kF10 && e.prev_state()) {
      e.set_handled(true);
      return;
    }
    if (e.virtual_key() == rex::ui::VirtualKey::kReturn && e.is_alt_pressed()) {
      // Ignore auto-repeat so holding the shortcut cannot toggle repeatedly.
      if (!e.prev_state()) {
        const bool fullscreen = !window()->IsFullscreen();
        rex::cvar::SetFlagByName("fullscreen", fullscreen ? "true" : "false");
        // Apply directly as well, using the actual window state as the source.
        window()->SetFullscreen(fullscreen);
        fifa08_pacing_generation.fetch_add(1, std::memory_order_relaxed);
        REXLOG_INFO("FIFA08 Alt+Enter: fullscreen={}", window()->IsFullscreen());
      }
      e.set_handled(true);
      return;
    }
    rex::ui::ProcessKeyEvent(e);
  }

  void OnWindowPixelSizeChanged(uint32_t width, uint32_t height) override {
    // A resize changes the host surface only, never the guest render size.
    fifa08_pacing_generation.fetch_add(1, std::memory_order_relaxed);
    REXLOG_INFO("FIFA08 window: {}x{}; render buffer unchanged", width, height);
  }

  void OnWindowFocusChanged(bool focused) override {
    fifa08_pacing_generation.fetch_add(1, std::memory_order_relaxed);
  }

  void OnCreateDialogs(rex::ui::ImGuiDrawer* drawer) override {
    const char* show = std::getenv("FIFA08_SHOW_FPS");
    pc_overlay_ = std::make_unique<PcOverlay>(drawer, show && std::string_view(show) == "1", window(),
        [this] {
          app_context().CallInUIThreadDeferred([this] { window()->RequestClose(); });
        });
    rex::ui::RegisterBind("bind_fifa08_pc_menu", "F10", "Open FIFA 08 PC menu",
        [this] { if (pc_overlay_) pc_overlay_->ToggleMenu(); });
    // Public UI: keep the game menu and achievements, disable SDK development panels.
    rex::ui::UnregisterBind("bind_debug_overlay");
    rex::ui::UnregisterBind("bind_console");
    rex::ui::UnregisterBind("bind_settings");
  }

  void OnShutdown() override {
    achievement_export_.Stop();
    fifa08_stop_timing();
    rex::ui::UnregisterBind("bind_fifa08_pc_menu");
    pc_overlay_.reset();
    capture_.Stop();
  }

 private:
  OutputCapture capture_;
  fifa08::achievements::Exporter achievement_export_;
  std::unique_ptr<PcOverlay> pc_overlay_;
};

