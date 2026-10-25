#define PPC_CONFIG_H_INCLUDED
#include <ppc_context.h>
#include <cstdio>
#include <random>
#include <array>
extern "C" void __imp__vslh_1000(PPCContext&, uint8_t*);
extern "C" void __imp__vslh_1008(PPCContext&, uint8_t*);
extern "C" void __imp__vslh_1010(PPCContext&, uint8_t*);
extern "C" void __imp__vslh_1018(PPCContext&, uint8_t*);
int main() {
    using Fn=void(*)(PPCContext&,uint8_t*);
    Fn functions[]={__imp__vslh_1000,__imp__vslh_1008,__imp__vslh_1010,__imp__vslh_1018};
    alignas(32) uint8_t memory[32]{};
    std::mt19937 random(12345);
    size_t cases=0;
    for(unsigned mode=0;mode<4;mode++) for(unsigned n=0;n<65536*16+10000;n++) {
        PPCContext ctx{};
        std::array<uint8_t,16> a{},b{},expected{};
        for(unsigned lane=0;lane<8;lane++) {
            const unsigned value=n<65536*16 ? ((n/16+lane)&65535) : random()&65535;
            const unsigned count=n<65536*16 ? (0xa5f0|((n+lane)&15)) : random()&65535;
            a[2*lane]=value>>8; a[2*lane+1]=value;
            b[2*lane]=count>>8; b[2*lane+1]=count;
            const unsigned shifts=(mode==3?value:count)&15;
            // Independent oracle: repeated multiplication modulo 65536.
            unsigned result=value;
            for(unsigned s=0;s<shifts;s++) result=(result*2)%65536;
            expected[2*lane]=result>>8; expected[2*lane+1]=result;
        }
        // Model the guest's big-endian byte vector; XenonRecomp reverses it.
        for(unsigned i=0;i<16;i++) {ctx.v2.u8[15-i]=a[i];ctx.v3.u8[15-i]=b[i];}
        functions[mode](ctx,memory);
        const auto& result=mode==1?ctx.v2:mode==2?ctx.v3:ctx.v1;
        for(unsigned i=0;i<16;i++) if(result.u8[15-i]!=expected[i]) {
            std::printf("FAIL mode=%u case=%u guest-byte=%u\n",mode,n,i);return 1;
        }
        cases++;
    }
    std::printf("PASS: %zu translated vslh cases; exhaustive halfwords/shifts, endian order, truncation, masking and aliases.\n",cases);
}
