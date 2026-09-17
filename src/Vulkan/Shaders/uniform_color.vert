#version 450

layout(location = 0) in vec3 inPosition;
layout(location = 1) in mat4 inTransformation;
layout(location = 5) in vec4 inColor;

layout(location = 0) out vec4 fragColor;

layout(set = 0, binding = 0) uniform ViewProjectionUBO
{
    mat4 viewProjection;
} camera;

void main()
{
    gl_Position = camera.viewProjection * inTransformation * vec4(inPosition, 1.0);
    fragColor = inColor;
}