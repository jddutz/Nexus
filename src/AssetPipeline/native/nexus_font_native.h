#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#  if defined(NAP_FONT_NATIVE_EXPORTS)
#    define NAP_FONT_API __declspec(dllexport)
#  else
#    define NAP_FONT_API __declspec(dllimport)
#  endif
#else
#  define NAP_FONT_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef struct NapFontBounds {
    double left, bottom, right, top;
} NapFontBounds;

typedef struct NapFontGlyph {
    int32_t codepoint;
    double advance;
    NapFontBounds plane_bounds;
    NapFontBounds atlas_bounds;
} NapFontGlyph;

typedef struct NapFontKerning {
    int32_t left;
    int32_t right;
    double adjustment;
} NapFontKerning;

typedef struct NapFontResult {
    int32_t width, height;
    uint8_t *pixels;
    double em_size, ascender, descender, line_height;
    NapFontGlyph *glyphs;
    int32_t glyph_count;
    NapFontKerning *kerning;
    int32_t kerning_count;
    double distance_range;
    int32_t generation_em_size;
} NapFontResult;

NAP_FONT_API int nap_font_generate(const char *path,
                                   const int32_t *codepoints,
                                   size_t codepoint_count,
                                   int32_t em_size,
                                   double distance_range,
                                   int32_t padding,
                                   NapFontResult **result,
                                   char **error);
NAP_FONT_API void nap_font_result_free(NapFontResult *result);
NAP_FONT_API void nap_font_error_free(char *error);

#ifdef __cplusplus
}
#endif
