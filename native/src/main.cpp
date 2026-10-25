// fifa08 - ReXGlue Recompiled Project

#include "generated/default/fifa08_init.h"

#include "fifa08_app.h"

static_assert(rex::byte_swap(uint8_t{0x12}) == 0x12);
static_assert(rex::byte_swap(uint16_t{0x1234}) == 0x3412);
static_assert(rex::byte_swap(uint32_t{0x12345678}) == 0x78563412);
static_assert(rex::byte_swap(uint64_t{0x0123456789ABCDEF}) == 0xEFCDAB8967452301);

REX_DEFINE_APP(fifa08, Fifa08App::Create)
