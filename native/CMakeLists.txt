# fifa08 - ReXGlue Recompiled Project
#
# SDK boilerplate lives in generated/rexglue.cmake.

cmake_minimum_required(VERSION 3.25)
project(fifa08 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(generated/rexglue.cmake)

# Spatial FSR1/CAS use the SDK's bundled shaders and CPU constants.
# No temporal runtime, motion-vector synthesis or frame generation.
option(FIFA08_SPATIAL_FSR "Enable bundled spatial FSR1 presentation" ON)
if(FIFA08_SPATIAL_FSR AND TARGET rexui)
    target_compile_definitions(rexui PUBLIC REX_HAS_FIDELITYFX_SDK=1)
endif()


# One host import name for either paired backend. The launch script deploys
# the selected runtime before the process starts; never load two registries.
if(REXSDK_DIR)
    set_target_properties(rexruntime PROPERTIES RELWITHDEBINFO_POSTFIX "")
endif()

# Sources
set(FIFA08_SOURCES
    src/main.cpp
    src/runtime_compat.cpp
    src/fifa08_native_render.cpp
)

if(WIN32)
    enable_language(RC)
    set(FIFA08_ICON_FILE "${CMAKE_CURRENT_SOURCE_DIR}/../launcher-assets/icon.ico")
    configure_file(src/fifa08.rc.in "${CMAKE_CURRENT_BINARY_DIR}/fifa08.rc" @ONLY)
    list(APPEND FIFA08_SOURCES "${CMAKE_CURRENT_BINARY_DIR}/fifa08.rc")
    add_executable(fifa08 WIN32 ${FIFA08_SOURCES})
else()
    add_executable(fifa08 ${FIFA08_SOURCES})
endif()

set(FIFA08_OUTPUT_NAME "fifa08" CACHE STRING "Name of the experimental executable")
set_target_properties(fifa08 PROPERTIES OUTPUT_NAME "${FIFA08_OUTPUT_NAME}")

rexglue_setup_target(fifa08 GPU_PLUGINS xenos)
if(REXSDK_DIR)
    add_custom_command(TARGET fifa08 POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E make_directory "$<TARGET_FILE_DIR:fifa08>/Backends/D3D12"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:rexruntime>"
            "$<TARGET_FILE_DIR:fifa08>/Backends/D3D12/rexruntime.dll"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:rexgpu-xenos>"
            "$<TARGET_FILE_DIR:fifa08>/Backends/D3D12/rexgpu-xenosrd.dll"
        COMMAND ${CMAKE_COMMAND} -E make_directory "$<TARGET_FILE_DIR:fifa08>/Backends/Vulkan"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:rexruntime>"
            "$<TARGET_FILE_DIR:fifa08>/Backends/Vulkan/rexruntime.dll"
        COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:rexgpu-xenos>"
            "$<TARGET_FILE_DIR:fifa08>/Backends/Vulkan/rexgpu-xenosrd.dll")
endif()
if(TARGET imgui::imgui)
    target_include_directories(fifa08 PRIVATE
        $<TARGET_PROPERTY:imgui::imgui,INTERFACE_INCLUDE_DIRECTORIES>)
endif()
if(WIN32)
    target_link_libraries(fifa08 PRIVATE winmm)
endif()

# Apply to both the host and the generated PPC code. Optimizing only the host
# leaves nearly all gameplay code at the previous compiler settings.
option(FIFA08_OPTIMIZED_BUILD "Build optimized x86-64 executable with ThinLTO" OFF)
option(FIFA08_CPU_THINLTO "Optimize across recompiled functions at reference O2/SSE4.1 precision" OFF)
option(FIFA08_CPU_AVX2 "Use native AVX2 integer/vector instructions for recompiled game code" ON)
option(FIFA08_DISPATCH_TELEMETRY "Keep the SDK atomic counter on every guest indirect call" OFF)

if(FIFA08_CPU_AVX2)
    if(NOT CMAKE_CXX_COMPILER_ID STREQUAL "Clang" OR
       NOT CMAKE_SYSTEM_PROCESSOR MATCHES "AMD64|x86_64")
        message(FATAL_ERROR "FIFA08_CPU_AVX2 requires x86-64 Clang")
    endif()
    # Keep O2, explicit guest FMA operations and existing memory semantics.
    # AVX2 lowers variable integer SIMD shifts directly; no fast-math or LTO.
    target_compile_options(fifa08_recomp PRIVATE -mavx2 -ffp-contract=off)
    target_compile_definitions(fifa08 PRIVATE FIFA08_REQUIRE_AVX2=1)
endif()
target_compile_definitions(fifa08_recomp PRIVATE FIFA08_MATCHING_RUNTIME_DISPATCH=1)
if(NOT FIFA08_DISPATCH_TELEMETRY)
    # Append after fifa08_pch.h; change only the diagnostic macro, with no SDK
    # class layout/ABI changes and no change to address checks or dispatch.
    target_precompile_headers(fifa08_recomp PRIVATE src/cpu_dispatch_policy.h)
    target_compile_definitions(fifa08_recomp PRIVATE FIFA08_DISPATCH_TELEMETRY_DISABLED=1)
endif()
if(FIFA08_CPU_THINLTO)
    if(NOT CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
        message(FATAL_ERROR "FIFA08_CPU_THINLTO requires Clang")
    endif()
    # Limit bitcode to the PPC translations. The pinned LLVM linker crashes
    # compiling the Windows host STL sleep_until under strict FP in ThinLTO.
    # Mixed native host + ThinLTO guest objects still optimize across all guest
    # partitions without changing host/runtime ABI or exception handling.
    target_compile_options(fifa08_recomp PRIVATE -O2 -msse4.1 -ffp-model=strict -flto=thin)
    target_link_options(fifa08 PRIVATE -flto=thin)
endif()
if(FIFA08_OPTIMIZED_BUILD)
    if(NOT CMAKE_CXX_COMPILER_ID STREQUAL "Clang" OR
       NOT CMAKE_SYSTEM_PROCESSOR MATCHES "AMD64|x86_64")
        message(FATAL_ERROR "FIFA08 optimized build currently requires x86-64 Clang")
    endif()
    foreach(target fifa08 fifa08_recomp)
        # AVX2 supported by the development i5-13400. Deliberately no fast-math
        # or FMA contraction: keep translated PPC floating point semantics.
        target_compile_options(${target} PRIVATE -O3 -mavx2 -ffp-contract=off -flto=thin)
    endforeach()
    target_link_options(fifa08 PRIVATE -flto=thin)
endif()
