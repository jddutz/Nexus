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

void main()
{
    vec3 msd = texture(texSampler, fragTexCoord).rgb;
    float sd = median(msd.r, msd.g, msd.b) - 0.5;
    float width = max(fwidth(sd), 1e-6);
    float opacity = clamp(sd / width + 0.5, 0.0, 1.0);
    outColor = vec4(fragTintColor.rgb, fragTintColor.a * opacity);
}