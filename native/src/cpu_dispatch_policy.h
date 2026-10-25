#pragma once

// Called after the generated SDK PCH in the game build. The SDK
// records each indirect call via an imported function and a global atomic add.
// This is diagnostic work, not a guest-visible counter or synchronization.
// Keep lookup, bounds checks, fallback resolution and traps exactly intact.
#ifndef REX_PROFILE_INDIRECT_DISPATCH
#error Include this policy after the generated recompiler PCH
#endif
#undef REX_PROFILE_INDIRECT_DISPATCH
#define REX_PROFILE_INDIRECT_DISPATCH() ((void)0)
