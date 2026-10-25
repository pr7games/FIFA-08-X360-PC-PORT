/**
 ******************************************************************************
 * Xenia : Xbox 360 Emulator Research Project                                 *
 ******************************************************************************
 * Copyright 2025 Ben Vanik. All rights reserved.                             *
 * Released under the BSD license - see LICENSE in the root for more details. *
 ******************************************************************************
 */

// Compute shader to downscale scaled resolve buffer data back to 1x resolution.
// Operates on 32x32 tiled data format used by Xbox 360.
// Each of 256 threads owns complete output words; no shared-memory packing.
//
// By default, picks the top-left pixel of each scale_x * scale_y block.
// When xe_downscale_half_pixel_offset is set, samples from (scale/2, scale/2)
// within each block to compensate for the half-pixel offset becoming a
// full-pixel offset at higher resolutions.

cbuffer XeResolveDownscaleConstants : register(b0) {
  uint xe_downscale_scale_x;         // 1 to kMaxDrawResolutionScaleAlongAxis
  uint xe_downscale_scale_y;         // 1 to kMaxDrawResolutionScaleAlongAxis
  uint xe_downscale_pixel_size_log2; // 0=8bit, 1=16bit, 2=32bit, 3=64bit
  uint xe_downscale_tile_count;      // Number of 32x32 tiles to process
  // When non-zero, apply half-pixel offset correction by sampling from
  // (scale/2, scale/2) within each scaled block instead of (0, 0).
  // This compensates for the D3D9-style half-pixel offset used by Xbox 360
  // games, which at Nx resolution scaling shifts rendered content by
  // (N/2, N/2) host pixels.
  uint xe_downscale_half_pixel_offset;
};

ByteAddressBuffer xe_resolve_source : register(t0);
RWByteAddressBuffer xe_resolve_dest : register(u0);

// One writer per complete output word avoids atomics and group barriers.
[numthreads(256, 1, 1)]
void main(uint3 group : SV_GroupID, uint lane : SV_GroupIndex) {
  uint tile = group.x;
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
  for (uint word = lane; word < tile_bytes / 4u; word += 256u) {
    uint value = 0u;
    if (size_log >= 2u) {
      uint pixel = word >> (size_log - 2u);
      uint part = word & ((pixel_bytes / 4u) - 1u);
      uint address = source_base + pixel * pixel_bytes * scale + part * 4u;
      value = xe_resolve_source.Load(address);
    } else {
      uint components = 4u / pixel_bytes;
      for (uint c = 0u; c < components; ++c) {
        uint address = source_base + (word * components + c) * pixel_bytes * scale;
        uint packed = xe_resolve_source.Load(address & ~3u);
        uint mask = size_log == 0u ? 255u : 65535u;
        uint component = (packed >> ((address & 3u) * 8u)) & mask;
        value |= component << (c * pixel_bytes * 8u);
      }
    }
    xe_resolve_dest.Store(tile * tile_bytes + word * 4u, value);
  }
}
