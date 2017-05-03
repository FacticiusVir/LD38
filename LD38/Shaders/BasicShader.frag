#version 450
#extension GL_ARB_separate_shader_objects : enable

layout(binding = 0) uniform UniformBufferObject {
    mat4 world;
    mat4 view;
    mat4 projection;
	mat4 invTransWorld;
} ubo;

layout(location = 0) in vec3 inNormal;
layout(location = 1) in vec4 inColour;
layout(location = 2) in vec4 inSpecular;

layout(location = 0) out vec4 outColour;

void main() {
    vec3 light = normalize(vec3(1, -1, -2));
	vec3 diffuse = inColour.xyz * dot(inNormal, light);

    vec3 r = normalize(2 * dot(light, inNormal) * inNormal - light);
    vec3 v = normalize((ubo.view * normalize(vec4(0, 0, -1, 1))).xyz);

    float dotProduct = dot(r, v);
    vec3 specular = inSpecular.xyz * max(pow(dotProduct, 50), 0);

    outColour = clamp(vec4(diffuse + specular, inColour.w), 0.0, 1.0);
}