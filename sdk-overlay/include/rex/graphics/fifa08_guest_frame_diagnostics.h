#pragma once
#include <atomic>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cstdint>
#include <memory>
#include <rex/logging.h>
#include <rex/ui/renderdoc_api.h>
namespace fifa08::diagnostics {
inline std::atomic<uint32_t> scene{0};inline std::atomic<uint64_t> cpu_submission{0};inline std::atomic<bool> capture_requested{false};
inline bool Enabled(){static const bool value=std::getenv("FIFA08_DRAW_LOG")!=nullptr;return value;}
struct Counts {uint64_t draws=0,copies=0,copy_bytes=0,draw_us=0,copy_us=0,query_copies=0,clamps=0;struct Range {uint32_t address,length;};Range ranges[64]{};uint32_t range_count=0;bool ranges_overflow=false;};
inline thread_local Counts counts;
enum class Kind { Draw,Copy };
class Scope {bool enabled_;Kind kind_;std::chrono::steady_clock::time_point start_;public:explicit Scope(Kind k):enabled_(Enabled()),kind_(k){if(enabled_)start_=std::chrono::steady_clock::now();}~Scope(){if(!enabled_)return;auto us=std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now()-start_).count();if(kind_==Kind::Draw){counts.draws++;counts.draw_us+=us;}else{counts.copies++;counts.copy_us+=us;}}};
inline thread_local uint32_t previous_swap_base=0;
inline void Resolve(uint32_t address,uint32_t length){if(!Enabled())return;counts.copy_bytes+=length;if(length){if(counts.range_count<64)counts.ranges[counts.range_count++]={address,length};else counts.ranges_overflow=true;}}
inline void QueryCopy(){if(Enabled())counts.query_copies++;}
inline void Clamp(){if(Enabled())counts.clamps++;}
// No renderer injection: an API is obtained only from an already connected
// RenderDoc. Frames are bracketed at guest swaps, including both API paths.
inline void CaptureSwap(void* device){static std::unique_ptr<rex::ui::RenderDocAPI> api=rex::ui::RenderDocAPI::CreateIfConnected();static thread_local bool capturing=false;if(api&&capturing){api->api_1_0_0()->EndFrameCapture(device,nullptr);capturing=false;REXGPU_INFO("FIFA08: guest-frame capture ended");}if(capture_requested.exchange(false)){if(api){api->api_1_0_0()->StartFrameCapture(device,nullptr);capturing=true;REXGPU_INFO("FIFA08: capturing one complete guest frame");}else REXGPU_WARN("FIFA08 F11: launch through RenderDoc to capture a guest frame");}}
class Swap {
 const char* api_;uint32_t base_,w_,h_;void* device_;std::chrono::steady_clock::time_point start_;
 public:Swap(const char* api,uint32_t base,uint32_t w,uint32_t h,void* device):api_(api),base_(base),w_(w),h_(h),device_(device){if(Enabled())start_=std::chrono::steady_clock::now();}
 ~Swap(){if(Enabled()){struct File {std::FILE* file=nullptr;uint64_t frame=0;char buffer[32768]{};File(){if(auto p=_wgetenv(L"FIFA08_DRAW_LOG")){file=_wfopen(p,L"w");if(file){std::setvbuf(file,buffer,_IOFBF,sizeof(buffer));std::fputs("api,frame,observed_scene_flags,observed_cpu_submission,draws,copies,copy_bytes,draw_cpu_us,copy_cpu_us,swap_cpu_us,swap_base,previous_swap_base,swap_source_written,requested_width,requested_height,visibility_copies,edram_clamps,resolve_ranges_overflow\n",file);}}}~File(){if(file)std::fclose(file);}};static thread_local File log;auto us=std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now()-start_).count();bool written=false;for(uint32_t i=0;i<counts.range_count;++i){auto r=counts.ranges[i];if(uint64_t(r.address)<=base_&&uint64_t(r.address)+r.length>base_)written=true;}if(log.file){std::fprintf(log.file,"%s,%llu,%u,%llu,%llu,%llu,%llu,%llu,%llu,%lld,%u,%u,%u,%u,%u,%llu,%llu,%u\n",api_,++log.frame,scene.load(),cpu_submission.load(),counts.draws,counts.copies,counts.copy_bytes,counts.draw_us,counts.copy_us,(long long)us,base_,previous_swap_base,written?1:0,w_,h_,counts.query_copies,counts.clamps,counts.ranges_overflow?1u:0u);if(log.frame%120==0)std::fflush(log.file);}counts={};previous_swap_base=base_;}CaptureSwap(device_);}
};
}
