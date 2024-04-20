int ToIndex(ivec3 XYZ, ivec3 dimension) {
	return XYZ.z + (XYZ.y * dimension.x) + (XYZ.x * dimension.y * dimension.z);
}

ivec3 ToIVec3(int XYZ, ivec3 dimension) {
    int x = XYZ / dimension.z / dimension.y % dimension.x;
    int y = XYZ / dimension.z % dimension.y;
    int z = XYZ % dimension.z;

    return ivec3(x, y, z);
}