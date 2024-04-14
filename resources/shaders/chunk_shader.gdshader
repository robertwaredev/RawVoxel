shader_type spatial;

render_mode world_vertex_coords, depth_prepass_alpha;

uniform bool showChunkEdges = true;
uniform ivec3 chunkDimension;

uniform sampler2D densityNoise;
uniform float densityCurve;

uniform sampler2D surfaceNoise;
uniform float surfaceCurve;

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
	//// Return air if we want to show chunk edges and the voxel being generated is not in this chunk.
	if (showChunkEdges && IsVoxelOutOfBounds(voxelPosition)) return 0;
	
	// Add this chunk's position to the voxel position to get the voxel's global position.
	ivec3 globalVoxelPosition = voxelPosition + chunkPosition;
	
	//// Sample terrain density using the voxel's global position.
	//float densityNoise = densityNoise.GetNoise3Dv(globalVoxelPosition);
	//float densityCurve = densityCurve.Sample((densityNoise + 1) * 0.5f);
	
	//// Return air if voxel is not dense enough to be considered solid.
    //if (densityCurve < 0.5f) return 0;
	
	//// Sample terrain surface using the voxel's global position.
	//vec2 globalVoxelPositionXZ = vec2(float(globalVoxelPosition.x), float(globalVoxelPosition.z));
	//vec4 surfaceNoiseSample = texture(surfaceNoise, globalVoxelPositionXZ);
    //float surfaceCurveSample = _world.SurfaceCurve.Sample((surfaceNoise + 1) * 0.5f);
	
	if (globalVoxelPosition.y <= bedrockHeight) return 1;
	if (globalVoxelPosition.y <= layer1Height)  return 2;
	if (globalVoxelPosition.y <= layer2Height)  return 3;
	if (globalVoxelPosition.y <= surfaceHeight) return 4;
	
	return 0;
}

ivec3 IndexToVector3I(int XYZ, ivec3 dimension) {
	int X = XYZ % dimension.x;
	int Y = XYZ / dimension.x % dimension.y;
	int Z = XYZ / dimension.x / dimension.y % dimension.z;

	return ivec3(X, Y, Z);
}

int Vector3IToUInt(ivec3 XYZ, ivec3 dimension)
{
    return XYZ.z + (XYZ.y * dimension.z) + (XYZ.x * dimension.y * dimension.z);
}

void vertex() {
	int VertexIndex = Vector3IToUInt(ivec3(VERTEX), ivec3(1, 1, 1));
	
	ivec3 voxelPosition = IndexToVector3I(VertexIndex % 24, chunkDimension);
	int voxelType = GenerateVoxel(voxelPosition, ivec3(NODE_POSITION_WORLD));
	
	switch (voxelType)
	{
		case 1:  COLOR = vec4(1.0, 0.0, 0.0, 1.0); break;
		case 2:  COLOR = vec4(0.0, 1.0, 0.0, 1.0); break;
		case 3:  COLOR = vec4(0.0, 0.0, 1.0, 1.0); break;
		case 4:  COLOR = vec4(1.0, 1.0, 1.0, 1.0); break;
		default: COLOR = vec4(0.0, 0.0, 0.0, 0.0); break;
	}
}

void fragment() {
	ALBEDO = COLOR.rgb;
	ALPHA = COLOR.a;
}