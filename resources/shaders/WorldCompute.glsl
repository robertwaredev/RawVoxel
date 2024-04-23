#[compute]
#version 450

layout(local_size_x = 4, local_size_y = 4, local_size_z = 4) in;

layout(set = 0, binding = 0, std430) restrict buffer worldData {
    ivec3 worldRadius;
    ivec3 chunkDimension;
    float data[];
}

void main() {
    
}