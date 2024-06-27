shader_type spatial;

render_mode depth_prepass_alpha;

uniform sampler2DArray voxelIDMap;
uniform vec4[256] voxelColors : source_color;

const vec3[6] normals = {
	vec3(-1,  0,  0),
	vec3( 1,  0,  0),
	vec3( 0, -1,  0),
	vec3( 0,  1,  0),
	vec3( 0,  0, -1),
	vec3( 0,  0,  1)
};

uniform int surfaceID;

varying vec4 color;
varying flat int vertexID;

void vertex() {
	NORMAL = normals[surfaceID];
	//vertexID = VERTEX_ID % 4;
}

void fragment() {
	//vec3 position = VERTEX - (NORMAL * 0.5);
	//
	//switch (vertexID)
	//{
		//case 0:
		//position += abs(NORMAL.yzx) * 0.5;
		//position -= abs(NORMAL.zxy) * 0.5;
		//break;
		//
		//case 1:
		//position += abs(NORMAL.yzx) * 0.5;
		//position += abs(NORMAL.zxy) * 0.5;
		//break;
		//
		//case 2:
		//position -= abs(NORMAL.yzx) * 0.5;
		//position += abs(NORMAL.zxy) * 0.5;
		//break;
		//
		//case 3:
		//position -= abs(NORMAL.yzx) * 0.5;
		//position -= abs(NORMAL.zxy) * 0.5;
		//break;
	//}
	//
	//position = floor(position);
	//
	//int voxelID = int(round(texelFetch(voxelIDMap, ivec3(position.zxy), 0).r * 255.0));
	//
	////color = voxelColors[voxelID];
	//color = texelFetch(voxelIDMap, ivec3(VERTEX.zxy), 0);
	//
	//ALBEDO = color.rgb;
	//ALPHA = color.a;
}