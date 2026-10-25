/**
 ******************************************************************************
 * Xenia : Xbox 360 Emulator Research Project                                 *
 ******************************************************************************
 * Copyright 2025 Ben Vanik. All rights reserved.                             *
 * Released under the BSD license - see LICENSE in the root for more details. *
 ******************************************************************************
 */

#version 460

// Compute shader to downscale scaled resolve buffer data back to 1x resolution.
// Operates on 32x32 tiled data format used by Xbox 360.
// Each of 256 threads owns complete output words; no shared-memory packing.
//
// By default, picks the top-left pixel of each scale_x * scale_y block.
// When xe_downscale_half_pixel_offset is set, samples from (scale/2, scale/2)
// within each block to compensate for the half-pixel offset becoming a
// full-pixel offset at higher resolutions.

layout(local_size_x = 256, local_size_y = 1, local_size_z = 1) in;

layout(push_constant) uniform ResolveDownscaleConstants {
  uint xe_downscale_scale_x;           // 1 to kMaxDrawResolutionScaleAlongAxis
  uint xe_downscale_scale_y;           // 1 to kMaxDrawResolutionScaleAlongAxis
  uint xe_downscale_pixel_size_log2;   // 0=8bit, 1=16bit, 2=32bit, 3=64bit
  uint xe_downscale_tile_count;        // Number of 32x32 tiles to process
  uint xe_downscale_source_offset_bytes;   // Byte offset into source buffer
  // When non-zero, apply half-pixel offset correction by sampling from
  // (scale/2, scale/2) within each scaled block instead of (0, 0).
  // This compensates for the D3D9-style half-pixel offset used by Xbox 360
  // games, which at Nx resolution scaling shifts rendered content by
  // (N/2, N/2) host pixels.
  uint xe_downscale_half_pixel_offset;
};

// Source buffer (scaled resolve data)
layout(std430, set = 0, binding = 0) readonly buffer SourceBuffer {
  uint data[];
} xe_resolve_source;

// Destination buffer (1x resolution)
layout(std430, set = 0, binding = 1) writeonly buffer DestBuffer {
  uint data[];
} xe_resolve_dest;

// One writer per complete output word avoids atomics and group barriers.
void main() {
  uint tile = gl_WorkGroupID.x;
  uint lane = gl_LocalInvocationIndex;
  if (tile >= xe_downscale_tile_count) return;
  uint size_log = xe_downscale_pixel_size_log2;
  uint pixel_bytes = 1u << size_log;
  uint scale = xe_downscale_scale_x * xe_downscale_scale_y;
  uint sample_pixel = 0u;
  if (xe_downscale_half_pixel_offset != 0u && scale > 1u)
    sample_pixel = (xe_downscale_scale_x >> 1u) +
                   (xe_downscale_scale_y >> 1u) * xe_downscale_scale_x;
  uint tile_bytes = 1024u * pixel_bytes;
  uint source_base = tile * tile_bytes * scale + sample_pixel * pixel_bytes;
  source_base += xe_downscale_source_offset_bytes;
  for (uint word = lane; word < tile_bytes / 4u; word += 256u) {
    uint value = 0u;
    if (size_log >= 2u) {
      uint pixel = word >> (size_log - 2u);
      uint part = word & ((pixel_bytes / 4u) - 1u);
      uint address = source_base + pixel * pixel_bytes * scale + part * 4u;
      value = xe_resolve_source.data[address >> 2u];
    } else {
      uint components = 4u / pixel_bytes;
      for (uint c = 0u; c < components; ++c) {
        uint address = source_base + (word * components + c) * pixel_bytes * scale;
        uint packed = xe_resolve_source.data[address >> 2u];
        uint mask = size_log == 0u ? 255u : 65535u;
        uint component = (packed >> ((address & 3u) * 8u)) & mask;
        value |= component << (c * pixel_bytes * 8u);
      }
    }
    xe_resolve_dest.data[(tile * tile_bytes) / 4u + word] = value;
  }
}
