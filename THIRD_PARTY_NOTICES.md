# Third-party notices

The root MIT license applies only to original PR7-owned contributions. Preserve all existing copyright and license headers when modifying or redistributing code.

- **ReXGlue SDK v0.10.0 / Xenia:** modified source in `sdk-overlay`, compatibility headers, SDK-generated build boilerplate and adapted shaders remain under the upstream BSD-3-Clause terms. Full notice: `LICENSES/ReXGlue-Xenia-BSD-3-Clause.txt`. Upstream: https://github.com/rexglue/rexglue-sdk and https://github.com/xenia-project/xenia. Copyright notices in individual source files remain applicable.
- **FIFA09 Recompiled / NOBK:** adapted visibility-query, EDRAM and other portions retain their original attribution. Full MIT notice and scope: `LICENSES/FIFA09-MIT.txt` (also retained in `FIFA09-NOTICE.txt`). Copyright (c) 2026 ChunchunQ and NOBK.Online contributors.
- **Editor format/repacking adaptations:** retain `modding/THIRD_PARTY_NOTICES.txt`, including ReXGlue/Xenia notices, in source and binary distributions.
- **Downloaded dependencies:** SDL, Dear ImGui, FFmpeg, SPIR-V/Vulkan components, DirectX-Headers, tl::expected, fmt, spdlog and the other SDK dependencies are obtained separately by the bootstrap. They are not relicensed by PR7. Their LICENSE/COPYING files remain in the downloaded trees. Any binary release must carry the applicable notices and fulfill the actual build's licensing obligations, including applicable FFmpeg LGPL/GPL requirements; this repository does not grant an exemption.
- **AMD FidelityFX FSR 1/CAS:** the project enables the upstream SDK implementation and compiled shaders; preserve AMD notices and applicable MIT terms from the SDK/dependency source when distributing them. FSR 1 is not a PR7-owned algorithm.

- **Xenia Canary texture fixes:** Vulkan adaptations from https://github.com/xenia-canary/xenia-canary at e6cd2e4bc998609ca25e24a24c8e32b66c5908ad retain BSD-3-Clause terms. Exact backports and validation: `validation/VULKAN-XENIA-TEXTURES.md`.

## Excluded commercial resources

FIFA 08 game data, executable images and translated game-derived sources are not supplied. EA SPORTS/FIFA artwork, player photographs, Xbox achievement audio and PlayStation button artwork used in the local development installation are not covered by the root MIT license and are intentionally excluded. The development-assets script generates neutral placeholders. Supply only resources you are authorized to use and distribute. Trademark mentions identify compatibility and do not imply affiliation or endorsement.
