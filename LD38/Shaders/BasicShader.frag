#version 450
#extension GL_ARB_separate_shader_objects : enable

layout(location = 0) in vec3 inNormal;

layout(location = 0) out vec4 outColour;

void main() {
    float factor = (dot(inNormal, normalize(vec3(1, -1, -1))) + 0.5f) / 1.5f;

	factor = clamp(factor, 0.0, 1.0);

    outColour = vec4(0, factor, 0, 1);
}