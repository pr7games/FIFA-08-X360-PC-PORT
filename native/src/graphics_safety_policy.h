#pragma once
#include <cstdint>
namespace fifa08 {
constexpr bool ShouldExecuteVisibilityDraw(bool query,bool kill,bool copy,bool fix){return !(query&&kill)||(copy&&fix);}
constexpr uint32_t ClampEdramOwnership(uint32_t base,uint32_t length,uint32_t limit,bool enabled){return !enabled?length:base>=limit?0:length>limit-base?limit-base:length;}
}
