#[compute]
#version 450

layout(local_size_x = 4, local_size_y = 4, local_size_z = 4) in;

layout(set = 0, binding = 0, std430) restrict buffer WorldData {
    int chunkIndices[];
}
worldData;

int ToIndex(ivec3 XYZ, ivec3 dimension) {
	return XYZ.z + (XYZ.y * dimension.z) + (XYZ.x * dimension.y * dimension.z);
}

ivec3 ToIVec3(int XYZ, ivec3 dimension) {
    int x = XYZ / dimension.z / dimension.y % dimension.x;
    int y = XYZ / dimension.z % dimension.y;
    int z = XYZ % dimension.z;

    return ivec3(x, y, z);
}

void main() {
    // Grab chunk index.
    //worldData.chunkIndices[gl_GlobalInvocationID] /= 2;
}