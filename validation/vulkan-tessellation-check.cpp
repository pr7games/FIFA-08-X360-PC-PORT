
#include <algorithm>
#include <bit>
#include <cassert>
#include <cmath>
#include <cstdint>
#include <cstdio>
using uint = uint32_t;
float uintBitsToFloat(uint value) { return std::bit_cast<float>(value); }
struct System {
  uint xe_vertex_index_endian = 0;
  int xe_vertex_base_index = 3;
  uint xe_vertex_index_min = 4, xe_vertex_index_max = 100000;
  float xe_tessellation_factor_range_min = 1, xe_tessellation_factor_range_max = 16;
};
namespace indexed {
using std::min; using std::max;
System xe_system_cbuffer; uint gl_VertexIndex;

float xe_out_value;

uint xe_swap_8_in_16(uint value) {
  return ((value << 8u) & 0xFF00FF00u) | ((value >> 8u) & 0x00FF00FFu);
}

uint xe_swap_16_in_32(uint value) {
  return ((value & 0x0000FFFFu) << 16u) | (value >> 16u);
}

void Run() {
  uint value = uint(gl_VertexIndex);
  uint endian = xe_system_cbuffer.xe_vertex_index_endian;
  if (endian == 1u || endian == 2u) {
    value = xe_swap_8_in_16(value);
  }
  if (endian == 2u || endian == 3u) {
    value = xe_swap_16_in_32(value);
  }
  value = uint(int(value) + xe_system_cbuffer.xe_vertex_base_index);
  value &= 0x00FFFFFFu;
  value = max(value, xe_system_cbuffer.xe_vertex_index_min);
  value = min(value, xe_system_cbuffer.xe_vertex_index_max);
  xe_out_value = float(value);
}

}

namespace adaptive {
using std::min; using std::max;
System xe_system_cbuffer; uint gl_VertexIndex;

float xe_out_value;

uint xe_swap_8_in_16(uint value) {
  return ((value << 8u) & 0xFF00FF00u) | ((value >> 8u) & 0x00FF00FFu);
}

uint xe_swap_16_in_32(uint value) {
  return ((value & 0x0000FFFFu) << 16u) | (value >> 16u);
}

void Run() {
  uint value = uint(gl_VertexIndex);
  uint endian = xe_system_cbuffer.xe_vertex_index_endian;
  if (endian == 1u || endian == 2u) {
    value = xe_swap_8_in_16(value);
  }
  if (endian == 2u || endian == 3u) {
    value = xe_swap_16_in_32(value);
  }
  // Adaptive DMA entries contain IEEE float edge factors, not integer indices.
  // Match the D3D12 tessellation_adaptive_vs shader (bitcast, then add one).
  float tessellation_factor = uintBitsToFloat(value) + 1.0;
  tessellation_factor = max(
      tessellation_factor, xe_system_cbuffer.xe_tessellation_factor_range_min);
  tessellation_factor = min(
      tessellation_factor, xe_system_cbuffer.xe_tessellation_factor_range_max);
  xe_out_value = tessellation_factor;
}

}

// Byte permutations are involutions. Encode test input independently of GLSL.
uint encode(uint value, uint endian) {
  const uint b[] = {value & 255, (value >> 8) & 255, (value >> 16) & 255, value >> 24};
  const uint permutation[4][4] = {{0,1,2,3}, {1,0,3,2}, {3,2,1,0}, {2,3,0,1}};
  const auto* p = permutation[endian];
  return b[p[0]] | (b[p[1]] << 8) | (b[p[2]] << 16) | (b[p[3]] << 24);
}
int main() {
  for (uint endian = 0; endian != 4; ++endian) {
    adaptive::xe_system_cbuffer.xe_vertex_index_endian = endian;
    indexed::xe_system_cbuffer.xe_vertex_index_endian = endian;
    for (float value : {-0.25f, 0.f, 0.25f, 0.5f, 2.f, 10.f, 30.f}) {
      adaptive::gl_VertexIndex = encode(std::bit_cast<uint>(value), endian);
      adaptive::Run();
      assert(adaptive::xe_out_value == std::clamp(value + 1.f, 1.f, 16.f));
    }
    for (uint value : {0u, 1u, 255u, 4000u, 0x123456u}) {
      indexed::gl_VertexIndex = encode(value, endian);
      indexed::Run();
      const auto expected = float(std::clamp((value + 3) & 0xFFFFFFu, 4u, 100000u));
      assert(indexed::xe_out_value == expected);
    }
  }
  std::puts("PASS: actual GLSL math matches D3D12 bitcast/index semantics in all four byte orders");
}
