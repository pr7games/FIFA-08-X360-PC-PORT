#pragma once
#include <cstdint>
#include <string_view>
namespace fifa08 {
enum class EffectLayerTest { kOriginal, kNoGlares, kNoVbeams, kNoBoth };
constexpr EffectLayerTest ParseEffectLayerTest(std::string_view name) {
  return name=="no_glares" ? EffectLayerTest::kNoGlares :
         name=="no_vbeams" ? EffectLayerTest::kNoVbeams :
         name=="no_both" ? EffectLayerTest::kNoBoth : EffectLayerTest::kOriginal;
}
constexpr uint64_t ApplyEffectLayerTest(uint64_t enabled, uint64_t glares,
    uint64_t vbeams, EffectLayerTest mode) {
  const uint64_t excluded = mode==EffectLayerTest::kNoGlares ? glares :
      mode==EffectLayerTest::kNoVbeams ? vbeams :
      mode==EffectLayerTest::kNoBoth ? glares|vbeams : 0;
  return enabled & ~excluded;
}
}
