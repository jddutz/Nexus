#version 450
layout(location = 0) in vec2 fragTexCoord;
layout(location = 1) in vec4 fragTintColor;
layout(location = 2) in vec2 fragLocalCoord;
layout(location = 3) flat in vec2 fragClippingMask;
layout(location = 0) out vec4 outColor;
layout(set = 1, binding = 0) uniform sampler2D texSampler;
void main() {
    float x = abs(fragLocalCoord.x - 0.5);
    float y = fragLocalCoord.y - 0.5;
    if (fragClippingMask.x == 1.0 && x + abs(y) > 0.5 - fragClippingMask.y) discard;
    if (fragClippingMask.x == 2.0 && y > 0.0 && x + y > 0.5 - fragClippingMask.y) discard;
    outColor = texture(texSampler, fragTexCoord) * fragTintColor;
}
