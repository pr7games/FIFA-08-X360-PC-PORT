#include "official.hlsli"
Texture2D<float4> source_image : register(t0);
#ifdef EASU
SamplerState source_sampler : register(s0);
cbuffer Effect : register(b0) { float2 input_output_ratio; float2 input_size_inv; };
AF4 FsrEasuRF(AF2 p) { return source_image.GatherRed(source_sampler,p); }
AF4 FsrEasuGF(AF2 p) { return source_image.GatherGreen(source_sampler,p); }
AF4 FsrEasuBF(AF2 p) { return source_image.GatherBlue(source_sampler,p); }
#else
cbuffer Effect : register(b0) { int2 output_offset; float sharpness; };
AF4 FsrRcasLoadF(ASU2 p) { uint w,h; source_image.GetDimensions(w,h); return source_image.Load(int3(clamp(p,int2(0,0),int2(w,h)-1),0)); }
void FsrRcasInputF(inout AF1 r,inout AF1 g,inout AF1 b) {}
#endif
float4 main(float4 position : SV_Position) : SV_Target {
  float3 color;
#ifdef EASU
  FsrEasuF(color,uint2(position.xy),EasuCon0(input_output_ratio),EasuCon1(input_size_inv),EasuCon2(input_size_inv),EasuCon3(input_size_inv));
#else
  uint2 p=uint2(int2(position.xy)-output_offset);
  FsrRcasF(color.r,color.g,color.b,p,uint4(asuint(sharpness),0,0,0));
#ifdef DITHER
  color=saturate(color+dither_lut[(p.x&15)|((p.y&15)<<4)]);
#endif
#endif
  return float4(color,1.0);
}
