cmake_minimum_required(VERSION 3.24)
project(nexus_font_native LANGUAGES CXX)

find_package(msdfgen CONFIG REQUIRED)

add_library(nexus_font_native SHARED nexus_font_native.cpp)
target_compile_features(nexus_font_native PRIVATE cxx_std_17)
target_link_libraries(nexus_font_native PRIVATE msdfgen::msdfgen msdfgen::msdfgen-ext)
target_compile_definitions(nexus_font_native PRIVATE NAP_FONT_NATIVE_EXPORTS)

if(MSVC)
  target_compile_options(nexus_font_native PRIVATE /W4 /EHsc)
else()
  target_compile_options(nexus_font_native PRIVATE -Wall -Wextra -Wpedantic)
endif()

install(TARGETS nexus_font_native
  RUNTIME DESTINATION .
  LIBRARY DESTINATION .
  ARCHIVE DESTINATION .)
