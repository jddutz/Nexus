#version 450

layout(location = 0) in vec4 fragColor;

layout(location = 0) out vec4 outColor;

float srgbToLinear(float channel)
{
    return channel <= 0.04045
        ? channel / 12.92
        : pow((channel + 0.055) / 1.055, 2.4);
}

void main() {
    outColor = vec4(
        srgbToLinear(fragColor.r),
        srgbToLinear(fragColor.g),
        srgbToLinear(fragColor.b),
        fragColor.a
    );
}
