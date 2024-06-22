shader_type spatial;

#include "res://addons/RawVoxel/resources/shaders/XYZConvert.gdshaderinc"

render_mode depth_prepass_alpha;

const vec3[6] normals = {
	vec3(-1,  0,  0),
	vec3( 1,  0,  0),
	vec3( 0, -1,  0),
	vec3( 0,  1,  0),
	vec3( 0,  0, -1),
	vec3( 0,  0,  1)
};

uniform int surfaceNormalID;

void vertex() {
	NORMAL = normals[surfaceNormalID];
}

void fragment() {
	
}