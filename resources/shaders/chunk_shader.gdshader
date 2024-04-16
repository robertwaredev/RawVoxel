shader_type spatial;

render_mode depth_prepass_alpha;
render_mode ensure_correct_normals;

uniform bool showChunkEdges = true;
uniform ivec3 chunkDimension;

uniform int surfaceHeight = 128;
uniform int layer2Height = 96;
uniform int layer1Height = 64;
uniform int bedrockHeight = 0;

bool IsVoxelOutOfBounds(vec3 voxelPosition) {
	if (voxelPosition.x < 0.0 || voxelPosition.x >= float(chunkDimension.x)) return true;
	if (voxelPosition.y < 0.0 || voxelPosition.y >= float(chunkDimension.y)) return true;
	if (voxelPosition.z < 0.0 || voxelPosition.z >= float(chunkDimension.z)) return true;
	
	return false;
}

int GenerateVoxel(vec3 voxelPosition, vec3 chunkPosition) {
	// Return air if we want to show chunk edges and the voxel being generated is not in this chunk.
	if (showChunkEdges && IsVoxelOutOfBounds(voxelPosition)) return 0;
	
	// Add this chunk's position to the voxel position to get the voxel's global position.
	vec3 globalVoxelPosition = voxelPosition + chunkPosition;
	
	if (int(globalVoxelPosition.y) <= bedrockHeight) return 1;
	if (int(globalVoxelPosition.y) <= layer1Height)  return 2;
	if (int(globalVoxelPosition.y) <= layer2Height)  return 3;
	if (int(globalVoxelPosition.y) <= surfaceHeight) return 4;
	
	return 0;
}

const vec3 normals[6] = {
	vec3( 0.0,  1.0,  0.0), 	// UP
	vec3( 0.0, -1.0,  0.0), 	// DOWN
	vec3(-1.0,  0.0,  0.0), 	// WEST
	vec3( 1.0,  0.0,  0.0), 	// EAST
	vec3( 0.0,  0.0, -1.0), 	// NORTH
	vec3( 0.0,  0.0,  1.0)  	// SOUTH
};

const vec2 uvs[4] = {
	vec2( 0.0,  0.0),	// TOP LEFT
	vec2( 0.0, -1.0),	// BTM LEFT
	vec2( 1.0, -1.0),	// BTM RIGHT
	vec2( 1.0,  0.0) 	// TOP RIGHT
};

const vec3 vertices[8] = {
	vec3(0.0, 1.0, 1.0),	// FrontTopLeft
	vec3(0.0, 0.0, 1.0),	// FrontBtmLeft
	vec3(1.0, 1.0, 1.0),	// FrontTopRight
	vec3(1.0, 0.0, 1.0),	// FrontBtmRight
	vec3(0.0, 1.0, 0.0),	// BackTopLeft
	vec3(0.0, 0.0, 0.0),	// BackBtmLeft
	vec3(1.0, 1.0, 0.0),	// BackTopRight
	vec3(1.0, 0.0, 0.0) 	// BackBtmRight
};

const int topFace[4]  	= { 4, 6, 2, 0 };
const int btmFace[4]  	= { 1, 3, 7, 5 };
const int westFace[4] 	= { 4, 0, 1, 5 };
const int eastFace[4] 	= { 2, 6, 7, 3 };
const int northFace[4]	= { 6, 4, 5, 7 };
const int southFace[4]	= { 0, 2, 3, 1 };

varying flat vec4 color;

void vertex() {
	int voxelFace = VERTEX_ID % 6 / 6;
	
	NORMAL = normals[voxelFace];
	UV = uvs[VERTEX_ID % 4];
	
	color = vec4(0.1, 0.0, 0.1, 1.0);
}

void fragment() {
	ALBEDO = color.rgb;
	ALPHA = color.a;
}