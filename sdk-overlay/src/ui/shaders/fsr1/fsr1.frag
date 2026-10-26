#version 450
#extension GL_GOOGLE_include_directive : require
#extension GL_EXT_samplerless_texture_functions : require
#include "official.hlsli"
layout(set=0,binding=0) uniform texture2D source_image;
layout(location=0) out vec4 output_color;
#ifdef EASU
layout(set=0,binding=1) uniform sampler source_sampler;
layout(push_constant) uniform Effect { layout(offset=16) vec2 input_output_ratio; layout(offset=24) vec2 input_size_inv; } effect;
AF4 FsrEasuRF(AF2 p) { return textureGather(sampler2D(source_image,source_sampler),p,0); }
AF4 FsrEasuGF(AF2 p) { return textureGather(sampler2D(source_image,source_sampler),p,1); }
AF4 FsrEasuBF(AF2 p) { return textureGather(sampler2D(source_image,source_sampler),p,2); }
#else
layout(push_constant) uniform Effect { layout(offset=16) ivec2 output_offset; layout(offset=24) float sharpness; } effect;
AF4 FsrRcasLoadF(ASU2 p) { return texelFetch(source_image,clamp(p,ivec2(0),textureSize(source_image,0)-1),0); }
void FsrRcasInputF(inout AF1 r,inout AF1 g,inout AF1 b) {}
#endif
void main() {
  vec3 color;
#ifdef EASU
  FsrEasuF(color,uvec2(gl_FragCoord.xy),EasuCon0(effect.input_output_ratio),EasuCon1(effect.input_size_inv),EasuCon2(effect.input_size_inv),EasuCon3(effect.input_size_inv));
#else
  uvec2 p=uvec2(ivec2(gl_FragCoord.xy)-effect.output_offset);
  FsrRcasF(color.r,color.g,color.b,p,uvec4(floatBitsToUint(effect.sharpness),0,0,0));
#ifdef DITHER
  color=clamp(color+dither_lut[(p.x&15u)|((p.y&15u)<<4u)],vec3(0),vec3(1));
#endif
#endif
  output_color=vec4(color,1.0);
}
