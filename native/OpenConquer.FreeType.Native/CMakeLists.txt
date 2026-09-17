cmake_minimum_required(VERSION 3.21)

project(OpenConquerFreeTypeNative LANGUAGES C)

set(BUILD_SHARED_LIBS ON CACHE BOOL "" FORCE)

set(FT_DISABLE_ZLIB ON CACHE BOOL "" FORCE)
set(FT_DISABLE_BZIP2 ON CACHE BOOL "" FORCE)
set(FT_DISABLE_PNG ON CACHE BOOL "" FORCE)
set(FT_DISABLE_HARFBUZZ ON CACHE BOOL "" FORCE)
set(FT_DISABLE_BROTLI ON CACHE BOOL "" FORCE)
set(FT_DISABLE_HVF ON CACHE BOOL "" FORCE)

if(
  NOT DEFINED OPENCONQUER_RUNTIME_IDENTIFIER
  OR OPENCONQUER_RUNTIME_IDENTIFIER STREQUAL ""
)
  message(
    FATAL_ERROR
    "OPENCONQUER_RUNTIME_IDENTIFIER must identify the target OpenConquer runtime."
  )
endif()

set(
  _supported_runtime_identifiers
  win-x64
  win-arm64
  osx-x64
  osx-arm64
  linux-x64
  linux-arm64
)

list(
  FIND
  _supported_runtime_identifiers
  "${OPENCONQUER_RUNTIME_IDENTIFIER}"
  _runtime_identifier_index
)

if(_runtime_identifier_index EQUAL -1)
  message(
    FATAL_ERROR
    "Unsupported OpenConquer runtime identifier: ${OPENCONQUER_RUNTIME_IDENTIFIER}"
  )
endif()

if(APPLE)
  if(OPENCONQUER_RUNTIME_IDENTIFIER STREQUAL "osx-arm64")
    if(NOT CMAKE_OSX_ARCHITECTURES STREQUAL "arm64")
      message(
        FATAL_ERROR
        "Runtime identifier osx-arm64 requires CMAKE_OSX_ARCHITECTURES=arm64; "
        "configured value is '${CMAKE_OSX_ARCHITECTURES}'."
      )
    endif()
  elseif(OPENCONQUER_RUNTIME_IDENTIFIER STREQUAL "osx-x64")
    if(NOT CMAKE_OSX_ARCHITECTURES STREQUAL "x86_64")
      message(
        FATAL_ERROR
        "Runtime identifier osx-x64 requires CMAKE_OSX_ARCHITECTURES=x86_64; "
        "configured value is '${CMAKE_OSX_ARCHITECTURES}'."
      )
    endif()
  else()
    message(
      FATAL_ERROR
      "Runtime identifier '${OPENCONQUER_RUNTIME_IDENTIFIER}' does not target macOS."
    )
  endif()
elseif(WIN32)
  if(NOT CMAKE_GENERATOR MATCHES "^Visual Studio ")
    message(
      FATAL_ERROR
      "OpenConquer Windows FreeType builds require a Visual Studio generator; "
      "configured generator is '${CMAKE_GENERATOR}'."
    )
  endif()

  string(TOLOWER "${CMAKE_VS_PLATFORM_NAME}" _windows_target_architecture)

  if(OPENCONQUER_RUNTIME_IDENTIFIER STREQUAL "win-x64")
    if(NOT _windows_target_architecture STREQUAL "x64")
      message(
        FATAL_ERROR
        "Runtime identifier win-x64 requires Visual Studio target platform x64; "
        "configured platform is '${CMAKE_VS_PLATFORM_NAME}'."
      )
    endif()
  elseif(OPENCONQUER_RUNTIME_IDENTIFIER STREQUAL "win-arm64")
    if(NOT _windows_target_architecture STREQUAL "arm64")
      message(
        FATAL_ERROR
        "Runtime identifier win-arm64 requires Visual Studio target platform ARM64; "
        "configured platform is '${CMAKE_VS_PLATFORM_NAME}'."
      )
    endif()
  else()
    message(
      FATAL_ERROR
      "Runtime identifier '${OPENCONQUER_RUNTIME_IDENTIFIER}' does not target Windows."
    )
  endif()
elseif(CMAKE_SYSTEM_NAME STREQUAL "Linux")
  string(TOLOWER "${CMAKE_SYSTEM_PROCESSOR}" _linux_target_architecture)

  if(OPENCONQUER_RUNTIME_IDENTIFIER STREQUAL "linux-x64")
    set(
      _valid_linux_target_architectures
      x86_64
      amd64
      x64
    )
  elseif(OPENCONQUER_RUNTIME_IDENTIFIER STREQUAL "linux-arm64")
    set(
      _valid_linux_target_architectures
      aarch64
      arm64
    )
  else()
    message(
      FATAL_ERROR
      "Runtime identifier '${OPENCONQUER_RUNTIME_IDENTIFIER}' does not target Linux."
    )
  endif()

  list(
    FIND
    _valid_linux_target_architectures
    "${_linux_target_architecture}"
    _linux_target_architecture_index
  )

  if(_linux_target_architecture_index EQUAL -1)
    message(
      FATAL_ERROR
      "Runtime identifier '${OPENCONQUER_RUNTIME_IDENTIFIER}' does not match "
      "Linux target architecture '${CMAKE_SYSTEM_PROCESSOR}'."
    )
  endif()
else()
  message(
    FATAL_ERROR
    "Unsupported native target operating system: ${CMAKE_SYSTEM_NAME}"
  )
endif()

add_subdirectory(upstream/freetype)

set(
  OPENCONQUER_FREETYPE_PACKAGE_DIRECTORY
  "${PROJECT_SOURCE_DIR}/artifacts/package/runtimes/${OPENCONQUER_RUNTIME_IDENTIFIER}/native"
)

set(
  OPENCONQUER_FREETYPE_PACKAGE_FILE
  "${OPENCONQUER_FREETYPE_PACKAGE_DIRECTORY}/${CMAKE_SHARED_LIBRARY_PREFIX}freetype${CMAKE_SHARED_LIBRARY_SUFFIX}"
)

add_custom_command(
  OUTPUT "${OPENCONQUER_FREETYPE_PACKAGE_FILE}"
  COMMAND
    "${CMAKE_COMMAND}" -E make_directory
    "${OPENCONQUER_FREETYPE_PACKAGE_DIRECTORY}"
  COMMAND
    "${CMAKE_COMMAND}" -E copy_if_different
    "$<TARGET_FILE:freetype>"
    "${OPENCONQUER_FREETYPE_PACKAGE_FILE}"
  DEPENDS freetype
  COMMENT
    "Staging FreeType runtime for ${OPENCONQUER_RUNTIME_IDENTIFIER}"
  VERBATIM
)

add_custom_target(
  OpenConquerFreeTypePackage
  ALL
  DEPENDS "${OPENCONQUER_FREETYPE_PACKAGE_FILE}"
)
