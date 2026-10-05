#version 450

layout(location = 0) in vec2 fragTexCoord;
layout(location = 1) in vec4 fragTintColor;
layout(location = 2) in float fragMsdfDistanceRange;

layout(location = 0) out vec4 outColor;

layout(set = 1, binding = 0) uniform sampler2D texSampler;

float median(float red, float green, float blue)
{
    return max(min(red, green), min(max(red, green), blue));
}

float srgbToLinear(float channel)
{
    return channel <= 0.04045
        ? channel / 12.92
        : pow((channel + 0.055) / 1.055, 2.4);
}

void main()
{
    vec3 msd = texture(texSampler, fragTexCoord).rgb;
    float sd = median(msd.r, msd.g, msd.b) - 0.5;
    // The generator encodes 0.5 - distance / (2 * DistanceRange).
    vec2 unitRange = vec2(2.0 * fragMsdfDistanceRange) / vec2(textureSize(texSampler, 0));
    vec2 screenTexSize = 1.0 / max(fwidth(fragTexCoord), vec2(1e-6));
    float screenPxRange = max(0.5 * dot(unitRange, screenTexSize), 1.0);
    float opacity = clamp(screenPxRange * sd + 0.5, 0.0, 1.0);
    outColor = vec4(
        srgbToLinear(fragTintColor.r),
        srgbToLinear(fragTintColor.g),
        srgbToLinear(fragTintColor.b),
        fragTintColor.a * opacity
    );
}
