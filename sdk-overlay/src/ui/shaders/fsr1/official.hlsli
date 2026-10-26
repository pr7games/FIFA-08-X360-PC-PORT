// PR7 presentation adapter for the unmodified AMD FSR v1.0.2 headers.
#define A_GPU 1
#ifdef VULKAN
#define A_GLSL 1
#else
#define A_HLSL 1
#endif
#include "../../../../thirdparty/fidelityfx-fsr-1.0.2/ffx_a.h"
#ifdef EASU
#define FSR_EASU_F 1
#else
#define FSR_RCAS_F 1
#endif
#include "../../../../thirdparty/fidelityfx-fsr-1.0.2/ffx_fsr1.h"
#ifdef DITHER
#include "dither.hlsli"
#endif
#ifdef EASU
AU4 EasuCon0(AF2 ratio) { return AU4(AU2_AF2(ratio), AU2_AF2(ratio * AF2_(0.5) - AF2_(0.5))); }
AU4 EasuCon1(AF2 inv) { return AU4(AU2_AF2(inv), AU2_AF2(inv * AF2(1.0,-1.0))); }
AU4 EasuCon2(AF2 inv) { return AU4(AU2_AF2(inv * AF2(-1.0,2.0)), AU2_AF2(inv * AF2(1.0,2.0))); }
AU4 EasuCon3(AF2 inv) { return AU4(AU2_AF2(AF2(0.0,4.0)*inv),AU2_(0)); }
#endif
