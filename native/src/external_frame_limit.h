#pragma once
#include <windows.h>
#include <chrono>
#include <filesystem>
#include <fstream>
#include <string>

// Read-only coordination with an already injected RTSS. Never load its hook,
// change shared flags, or change the user's global/application profiles.
inline int fifa08_rtss_limit() {
  HMODULE module = GetModuleHandleW(L"RTSSHooks64.dll");
  if (!module) return 0;
  // RTSS SDK RTSSHooksInterface.h: LIMITER_DISABLED = 4. The installed x64
  // GetFlags export returns the shared DWORD without arguments (verified ABI).
  using GetFlags = DWORD (*)();
  auto flags = reinterpret_cast<GetFlags>(GetProcAddress(module, "GetFlags"));
  if (!flags || (flags() & 4)) return 0;
  using Clock = std::chrono::steady_clock;
  static thread_local auto checked = Clock::time_point{};
  static thread_local int cached_limit = 0;
  const auto now = Clock::now();
  if (now - checked < std::chrono::seconds(1)) return cached_limit;
  checked = now;
  cached_limit = 0;
  wchar_t dll_path[32768]{};
  const DWORD size = GetModuleFileNameW(module, dll_path, 32768);
  if (!size || size >= 32768) return 0;
  auto profiles = std::filesystem::path(dll_path).parent_path() / L"Profiles";
  const auto read = [](const std::filesystem::path& path, int fallback) {
    std::ifstream input(path);
    if (!input) return fallback;
    bool framerate = false;
    int limit = fallback, denominator = 1;
    std::string line;
    while (std::getline(input, line)) {
      if (!line.empty() && line.back() == '\r') line.pop_back();
      if (!line.empty() && line.front() == '[') {
        framerate = line == "[Framerate]";
        continue;
      }
      if (!framerate) continue;
      try {
        if (line.starts_with("Limit=")) limit = std::stoi(line.substr(6));
        else if (line.starts_with("LimitDenominator=")) denominator = std::stoi(line.substr(17));
      } catch (...) { return 0; }
    }
    return denominator == 1 && limit > 0 && limit <= 1000 ? limit : 0;
  };
  const int global = read(profiles / L"Global", 0);
  wchar_t exe_path[32768]{};
  const DWORD exe_size = GetModuleFileNameW(nullptr, exe_path, 32768);
  if (!exe_size || exe_size >= 32768) return 0;
  auto profile = std::filesystem::path(exe_path).filename().wstring() + L".cfg";
  cached_limit = read(profiles / profile, global);
  return cached_limit;
}

constexpr bool fifa08_needs_host_limiter(int target, int external) {
  return target != external;
}
static_assert(!fifa08_needs_host_limiter(60, 60));
static_assert(fifa08_needs_host_limiter(30, 60));
static_assert(fifa08_needs_host_limiter(60, 0));
static_assert(!fifa08_needs_host_limiter(30, 30));
