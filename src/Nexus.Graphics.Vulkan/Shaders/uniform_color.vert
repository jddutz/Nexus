#version 450

layout(location = 0) in vec3 inPosition;

layout(push_constant) uniform PushConstants
{
    vec4 color;
} pushConstants;

layout(location = 0) out vec4 fragColor;

void main()
{
    gl_Position = vec4(inPosition, 1.0);
    fragColor = pushConstants.color;
}