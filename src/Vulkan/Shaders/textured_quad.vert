#version 450

#ifdef NEXUS_SHADER_DEBUG_PRINTF
#extension GL_EXT_debug_printf : enable
#endif

layout(location = 0) in vec2 inPos;
layout(location = 1) in vec2 inTexCoord;

layout(location = 2) in vec4 inWorld0;
layout(location = 3) in vec4 inWorld1;
layout(location = 4) in vec4 inWorld2;
layout(location = 5) in vec4 inWorld3;
layout(location = 6) in vec4 inUvRect;
layout(location = 7) in vec4 inTintColor;

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

    vec4 localPosition = vec4(inPos, 0.0, 1.0);
    vec4 worldPosition = world * localPosition;
    gl_Position = camera.viewProjection * worldPosition;

#ifdef NEXUS_SHADER_DEBUG_PRINTF
    if (gl_InstanceIndex == 0)
    {
        debugPrintfEXT(
            "vertex instance=%d index=%d local=%v4f world=%v4f vp0=%v4f vp1=%v4f vp2=%v4f vp3=%v4f clip=%v4f",
            gl_InstanceIndex,
            gl_VertexIndex,
            localPosition,
            worldPosition,
            camera.viewProjection[0],
            camera.viewProjection[1],
            camera.viewProjection[2],
            camera.viewProjection[3],
            gl_Position
        );
    }
#endif

    fragTexCoord = inUvRect.xy + inTexCoord * inUvRect.zw;
    fragTintColor = inTintColor;
}