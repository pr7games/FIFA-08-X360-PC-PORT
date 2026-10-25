#define PPC_CONFIG_H_INCLUDED
#include <ppc_context.h>
#include <cstdio>
#include <random>
#include <array>
extern "C" void __imp__vandc_1000(PPCContext&, uint8_t*);
extern "C" void __imp__vandc_1008(PPCContext&, uint8_t*);
extern "C" void __imp__vandc_1010(PPCContext&, uint8_t*);
extern "C" void __imp__vandc_1018(PPCContext&, uint8_t*);
int main() {
    using Fn = void(*)(PPCContext&, uint8_t*);
    Fn functions[] = {__imp__vandc_1000,__imp__vandc_1008,__imp__vandc_1010,__imp__vandc_1018};
    alignas(32) uint8_t memory[32]{};
    std::mt19937 random(12345);
    size_t cases=0;
    for(unsigned mode=0;mode<4;mode++) for(unsigned n=0;n<65536+10000;n++) {
        PPCContext ctx{};
        std::array<uint8_t,16> expected{};
        for(unsigned lane=0;lane<16;lane++) {
            const unsigned a=n<65536 ? n>>8 : random()&255;
            const unsigned b=n<65536 ? n&255 : random()&255;
            ctx.v2.u8[lane]=a; ctx.v3.u8[lane]=b;
            // Independent bit-by-bit ISA oracle; does not call SIMD intrinsics.
            for(unsigned bit=0;bit<8;bit++)
                if ((a&(1u<<bit)) && !((mode==3?a:b)&(1u<<bit))) expected[lane]|=1u<<bit;
        }
        functions[mode](ctx,memory);
        const auto& result=mode==1?ctx.v2:mode==2?ctx.v3:ctx.v1;
        for(unsigned lane=0;lane<16;lane++) if(result.u8[lane]!=expected[lane]) {
            std::printf("FAIL mode=%u case=%u lane=%u\n",mode,n,lane); return 1;
        }
        cases++;
    }
    std::printf("PASS: %zu translated vandc cases; exhaustive bytes, varied lanes, destination/source aliases.\n",cases);
}
