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
    float distance = median(msd.r, msd.g, msd.b);
    float inside = step(0.5, median(msd.r, msd.g, msd.b));
    outColor = vec4(fragTintColor.rgb, fragTintColor.a * inside);
}