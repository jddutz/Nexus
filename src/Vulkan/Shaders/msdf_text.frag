#version 450

#extension GL_EXT_debug_printf : enable

layout(location = 0) in vec2 fragTexCoord;
layout(location = 1) in vec4 fragTintColor;
layout(location = 2) in float fragMsdfDistanceRange;

layout(location = 0) out vec4 outColor;

layout(set = 1, binding = 0) uniform sampler2D texSampler;

float median(float red, float green, float blue)
{
    return max(min(red, green), min(max(red, green), blue));
}

void main()
{
    vec3 msd = texture(texSampler, fragTexCoord).rgb;
    float signedDistance = median(msd.r, msd.g, msd.b) - 0.5;
    vec2 unitRange = vec2(fragMsdfDistanceRange) / vec2(textureSize(texSampler, 0));
    vec2 screenTexSize = vec2(1.0) / fwidth(fragTexCoord);
    float screenPxRange = max(0.5 * dot(unitRange, screenTexSize), 1.0);
    float opacity = clamp(screenPxRange * signedDistance + 0.5, 0.0, 1.0);

    if (gl_FragCoord.x > 6.0 && gl_FragCoord.x < 7.0 &&
        gl_FragCoord.y > 10.0 && gl_FragCoord.y < 11.0)
    {
        debugPrintfEXT("H_SAMPLE_V2 uv=(%f,%f) rgb=(%f,%f,%f) median=%f range=%f pxRange=%f opacity=%f\n",
            fragTexCoord.x, fragTexCoord.y,
            msd.r, msd.g, msd.b,
            median(msd.r, msd.g, msd.b),
            fragMsdfDistanceRange, screenPxRange, opacity);
    }

    outColor = vec4(fragTintColor.rgb, fragTintColor.a * opacity);
}