#version 450

layout(location = 0) in vec2 inPos;
layout(location = 1) in vec2 inTexCoord;

layout(location = 2) in vec4 inWorld0;
layout(location = 3) in vec4 inWorld1;
layout(location = 4) in vec4 inWorld2;
layout(location = 5) in vec4 inWorld3;
layout(location = 7) in vec4 inUvRect;
layout(location = 6) in vec4 inTintColor;

layout(location = 0) out vec2 fragTexCoord;
layout(location = 1) out vec4 fragTintColor;

layout(set = 0, binding = 0) uniform ViewProjectionUBO
{
    mat4 viewProjection;
} camera;

void main()
{
    mat4 world = mat4(
        inWorld0,
        inWorld1,
        inWorld2,
        inWorld3
    );

    gl_Position =
        camera.viewProjection *
        world *
        vec4(inPos, 0.0, 1.0);

    fragTexCoord = mix(inUvRect.xy, inUvRect.zw, inTexCoord);
    fragTintColor = inTintColor;
}