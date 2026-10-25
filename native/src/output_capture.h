#pragma once
#include <rex/ui/presenter.h>
#include <atomic>
#include <chrono>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <thread>
#include <vector>

// Opt-in diagnostic capture of the game's framebuffer through the SDK.
// It does not capture the desktop or other applications.
class OutputCapture {
 public:
  ~OutputCapture() { Stop(); }
  void Start(rex::ui::Presenter* presenter) {
    const char* filename = std::getenv("FIFA08_CAPTURE_OUTPUT");
    if (!presenter || !filename || !*filename || worker_.joinable()) return;
    const std::filesystem::path output(filename);
    stop_ = false;
    worker_ = std::thread([this, presenter, output] {
      for (unsigned seconds = 1; seconds <= 120 && !stop_; ++seconds) {
        std::this_thread::sleep_for(std::chrono::seconds(1));
        if (stop_ || seconds % 5) continue;
        rex::ui::RawImage image;
        if (presenter->CaptureGuestOutput(image)) SaveBmp(output, image);
      }
    });
  }
  void Stop() {
    stop_ = true;
    if (worker_.joinable()) worker_.join();
  }
 private:
  static void SaveBmp(const std::filesystem::path& filename, const rex::ui::RawImage& image) {
    if (!image.width || !image.height || image.width > 16384 || image.height > 16384) return;
    const size_t row = size_t(image.width) * 4;
    if (image.stride < row || image.data.size() < (image.height - 1) * image.stride + row) return;
    const uint32_t pixels = uint32_t(row * image.height);
    std::vector<uint8_t> bmp(54 + pixels);
    auto put16 = [&](size_t offset, uint16_t value) {
      for (unsigned i = 0; i < 2; ++i) bmp[offset+i] = uint8_t(value >> (i*8));
    };
    auto put32 = [&](size_t offset, uint32_t value) {
      for (unsigned i = 0; i < 4; ++i) bmp[offset+i] = uint8_t(value >> (i*8));
    };
    bmp[0]='B'; bmp[1]='M'; put32(2,uint32_t(bmp.size())); put32(10,54);
    put32(14,40); put32(18,image.width); put32(22,0u-image.height);
    put16(26,1); put16(28,32); put32(34,pixels);
    for (size_t y = 0; y < image.height; ++y)
      for (size_t x = 0; x < image.width; ++x) {
        const auto* src = image.data.data() + y*image.stride + x*4;
        auto* dst = bmp.data() + 54 + y*row + x*4;
        dst[0]=src[2]; dst[1]=src[1]; dst[2]=src[0]; dst[3]=255;
      }
    std::ofstream stream(filename, std::ios::binary|std::ios::trunc);
    stream.write(reinterpret_cast<const char*>(bmp.data()), bmp.size());
  }
  std::atomic<bool> stop_{false};
  std::thread worker_;
};
