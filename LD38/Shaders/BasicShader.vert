#version 450
#extension GL_ARB_separate_shader_objects : enable

layout(binding = 0) uniform UniformBufferObject {
    mat4 world;
    mat4 view;
    mat4 projection;
	mat4 invTransWorld;
} ubo;

layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inNormal;

layout(location = 0) out vec3 outNormal;

out gl_PerVertex {
    vec4 gl_Position;
};

void main() {
    gl_Position = ubo.projection * ubo.view * ubo.world * vec4(inPosition, 1.0);
	outNormal = (ubo.invTransWorld * vec4(inNormal, 0.0)).xyz;
}