#include "nexus_font_native.h"

#include <msdfgen.h>
#include <msdfgen-ext.h>

#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

namespace {

struct FreetypeDeleter {
    void operator()(msdfgen::FreetypeHandle *value) const { msdfgen::deinitializeFreetype(value); }
};
struct FontDeleter {
    void operator()(msdfgen::FontHandle *value) const { msdfgen::destroyFont(value); }
};
struct ResultDeleter {
    void operator()(NapFontResult *value) const { nap_font_result_free(value); }
};

char *copy_error(const std::string &message) {
    auto *output = static_cast<char *>(std::malloc(message.size()+1));
    if (output)
        std::memcpy(output, message.c_str(), message.size()+1);
    return output;
}

int next_power_of_two(int value) {
    int result = 1;
    while (result < value)
        result *= 2;
    return result;
}

uint8_t to_byte(float value) {
    return static_cast<uint8_t>(std::clamp(value, 0.f, 1.f)*255.f+.5f);
}

} // namespace

extern "C" int nap_font_generate(const char *path,
                                  const int32_t *codepoints,
                                  size_t codepoint_count,
                                  int32_t em_size,
                                  double distance_range,
                                  int32_t padding,
                                  NapFontResult **result,
                                  char **error) {
    if (result) *result = nullptr;
    if (error) *error = nullptr;
    try {
        if (!path || !codepoints || !codepoint_count || !result)
            throw std::runtime_error("Invalid font rasterization request.");

        std::unique_ptr<msdfgen::FreetypeHandle, FreetypeDeleter> freetype(msdfgen::initializeFreetype());
        if (!freetype)
            throw std::runtime_error("FreeType initialization failed.");
        std::unique_ptr<msdfgen::FontHandle, FontDeleter> font(msdfgen::loadFont(freetype.get(), path));
        if (!font)
            throw std::runtime_error("The source is not a readable TrueType/OpenType font.");

        msdfgen::FontMetrics metrics{};
        if (!msdfgen::getFontMetrics(metrics, font.get()))
            throw std::runtime_error("Could not read font metrics.");

        const int margin = padding+static_cast<int>(std::ceil(distance_range));
        const int cell_size = 2*em_size+2*margin;
        const int columns = static_cast<int>(std::ceil(std::sqrt(static_cast<double>(codepoint_count))));
        const int rows = static_cast<int>((codepoint_count+columns-1)/columns);
        const int width = next_power_of_two(columns*cell_size);
        const int height = next_power_of_two(rows*cell_size);

        std::unique_ptr<NapFontResult, ResultDeleter> output(new NapFontResult{});
        output->width = width;
        output->height = height;
        output->pixels = static_cast<uint8_t *>(std::calloc(static_cast<size_t>(width)*height*3, 1));
        output->glyph_count = static_cast<int32_t>(codepoint_count);
        output->glyphs = static_cast<NapFontGlyph *>(std::calloc(codepoint_count, sizeof(NapFontGlyph)));
        output->em_size = em_size;
        output->ascender = metrics.ascenderY;
        output->descender = metrics.descenderY;
        output->line_height = metrics.lineHeight;
        output->distance_range = distance_range;
        output->generation_em_size = em_size;
        if (!output->pixels || !output->glyphs)
            throw std::bad_alloc();

        std::vector<NapFontKerning> kerning;
        for (size_t index = 0; index < codepoint_count; ++index) {
            const int32_t codepoint = codepoints[index];
            msdfgen::Shape shape;
            double advance = 0;
            if (!msdfgen::loadGlyph(shape, font.get(), static_cast<msdfgen::unicode_t>(codepoint), &advance))
                throw std::runtime_error("Requested glyph U+"+std::to_string(codepoint)+" is unavailable.");

            shape.normalize();
            double left = 0, bottom = 0, right = 0, top = 0;
            shape.bound(left, bottom, right, top);
            const int column = static_cast<int>(index)%columns;
            const int row = static_cast<int>(index)/columns;
            const int origin_x = column*cell_size;
            const int origin_y = row*cell_size;
            const double translate_x = origin_x+.5*cell_size-.5*(left+right)*em_size;
            const double translate_y = origin_y+.5*cell_size-.5*(bottom+top)*em_size;

            if (!shape.contours.empty()) {
                msdfgen::edgeColoringSimple(shape, 3.0, static_cast<unsigned long long>(codepoint));
                msdfgen::Bitmap<float, 3> bitmap(cell_size, cell_size);
                // The legacy msdfgen overload expresses range in shape (em) units.
                msdfgen::generateMSDF(bitmap, shape, distance_range/em_size, em_size,
                    msdfgen::Vector2(translate_x-origin_x, translate_y-origin_y));
                for (int y = 0; y < cell_size; ++y)
                    for (int x = 0; x < cell_size; ++x)
                        for (int channel = 0; channel < 3; ++channel)
                            output->pixels[(static_cast<size_t>(origin_y+y)*width+origin_x+x)*3+channel] =
                                to_byte(bitmap(x, y)[channel]);
            }

            const double atlas_expansion = .5*distance_range;
            const double plane_expansion = atlas_expansion/em_size;
            output->glyphs[index] = {
                codepoint,
                advance,
                {left-plane_expansion, bottom-plane_expansion,
                 right+plane_expansion, top+plane_expansion},
                {translate_x+left*em_size-atlas_expansion,
                 translate_y+bottom*em_size-atlas_expansion,
                 translate_x+right*em_size+atlas_expansion,
                 translate_y+top*em_size+atlas_expansion}
            };
        }

        for (size_t left = 0; left < codepoint_count; ++left) {
            for (size_t right = 0; right < codepoint_count; ++right) {
                double adjustment = 0;
                if (msdfgen::getKerning(adjustment, font.get(), codepoints[left], codepoints[right]) && adjustment != 0)
                    kerning.push_back({codepoints[left], codepoints[right], adjustment});
            }
        }
        output->kerning_count = static_cast<int32_t>(kerning.size());
        if (!kerning.empty()) {
            output->kerning = static_cast<NapFontKerning *>(std::malloc(kerning.size()*sizeof(NapFontKerning)));
            if (!output->kerning)
                throw std::bad_alloc();
            std::memcpy(output->kerning, kerning.data(), kerning.size()*sizeof(NapFontKerning));
        }

        *result = output.release();
        return 0;
    } catch (const std::exception &exception) {
        if (error) *error = copy_error(exception.what());
        return 1;
    } catch (...) {
        if (error) *error = copy_error("Unknown native font rasterization failure.");
        return 2;
    }
}

extern "C" void nap_font_result_free(NapFontResult *result) {
    if (!result) return;
    std::free(result->pixels);
    std::free(result->glyphs);
    std::free(result->kerning);
    delete result;
}

extern "C" void nap_font_error_free(char *error) {
    std::free(error);
}
