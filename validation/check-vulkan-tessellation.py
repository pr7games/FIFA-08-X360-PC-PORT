"""Compile the actual embedded GLSL math as C++ and check D3D12 semantics."""
import pathlib
import re

project = pathlib.Path(__file__).resolve().parents[1]
source = (project / 'tools/rexglue-source/rexglue-sdk-0.10.0/src/graphics/vulkan/pipeline_cache.cpp').read_text()
parts = []
for name, function in [('indexed', 'GetTessellationIndexedVertexShaderGlsl'),
                       ('adaptive', 'GetTessellationAdaptiveVertexShaderGlsl')]:
    body = source.split('std::string ' + function + '()', 1)[1].split('return source;', 1)[0]
    glsl = re.search(r'R"\((.*?)\)"', body, re.S).group(1)
    glsl = glsl.replace('layout(location = 0) out float', 'float').replace('void main()', 'void Run()')
    parts.append('namespace ' + name + ' {\nusing std::min; using std::max;\n'
                 'System xe_system_cbuffer; uint gl_VertexIndex;\n' + glsl + '\n}\n')
test = r'''
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
'''
test += '\n'.join(parts)
test += r'''
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
'''
(project / 'validation/vulkan-tessellation-check.cpp').write_text(test)
