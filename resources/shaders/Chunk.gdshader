shader_type spatial;

#include "res://addons/RawVoxel/resources/shaders/XYZConvert.gdshaderinc"
#include "res://addons/RawVoxel/resources/shaders/Voxel.gdshaderinc"

render_mode depth_prepass_alpha;
//render_mode world_vertex_coords;

uniform bool showChunkEdges = true;
uniform ivec3 chunkDimension;

uniform sampler3D density;

uniform int surfaceHeight = 128;
uniform int layer2Height = 96;
uniform int layer1Height = 64;
uniform int bedrockHeight = 0;

bool IsVoxelOutOfBounds(ivec3 voxelPosition) {
	if (voxelPosition.x < 0 || voxelPosition.x >= chunkDimension.x) return true;
	if (voxelPosition.y < 0 || voxelPosition.y >= chunkDimension.y) return true;
	if (voxelPosition.z < 0 || voxelPosition.z >= chunkDimension.z) return true;
	
	return false;
}

int GenerateVoxel(ivec3 voxelPosition, ivec3 chunkPosition) {
	// Return air if we want to show chunk edges and the voxel being generated is not in this chunk.
	if (showChunkEdges && IsVoxelOutOfBounds(voxelPosition)) return 0;
	
	// Add this chunk's position to the voxel position to get the voxel's global position.
	ivec3 globalVoxelPosition = voxelPosition + chunkPosition;
	if (globalVoxelPosition.y <= bedrockHeight) return 1;
	if (globalVoxelPosition.y <= layer1Height)  return 2;
	if (globalVoxelPosition.y <= layer2Height)  return 3;
	if (globalVoxelPosition.y <= surfaceHeight) return 4;
	
	return 0;
}

void vertex() {
	//vec3 voxelPosition;
	//
	//normal = normals[VERTEX_ID / 4 % 6];
	//
	//voxelPosition = vec3(VERTEX + normal * -0.5);
	//voxelPosition.x = floor(voxelPosition.x);
	//voxelPosition.y = floor(voxelPosition.y);
	//voxelPosition.z = floor(voxelPosition.z);
	//voxelPosition = mod(voxelPosition, vec3(chunkDimension));
	//
	//vec4 typeYX = texelFetch(typeMapYX, ivec2(vec2(voxelPosition.y, voxelPosition.x)), 0);
	//vec4 typeYZ = texelFetch(typeMapYZ, ivec2(vec2(voxelPosition.y, voxelPosition.z)), 0);
	//
	//color = typeYX + typeYZ;
	//COLOR = color;
}

void fragment() {
	ALBEDO = COLOR.rgb;
	ALPHA = COLOR.a;
}