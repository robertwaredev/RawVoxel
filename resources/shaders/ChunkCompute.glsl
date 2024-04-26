#[compute]
#version 450

layout(local_size_x = 4, local_size_y = 4, local_size_z = 4) in;


layout(set = 0, binding = 0, std430) restrict buffer ChunkData {
    int voxelIDs[];
} chunkData;

// --- TEMPERATURE --- 

layout(set = 1, binding = 0) uniform sampler2D TemperatureSampler;

layout(set = 1, binding = 1, std430) restrict buffer TemperatureDistributionPoints {
    float values[];
} temperatureDistributionPoints;

layout(set = 1, binding = 2, std430) restrict buffer TemperatureDistributionTangents {
    float values[];
} temperatureDistributionTangents;

layout(set = 1, binding = 3, std430) restrict buffer TemperatureRangePoints {
    float values[];
} temperatureRangePoints;

layout(set = 1, binding = 4, std430) restrict buffer TemperatureRangeTangents {
    float values[];
} temperatureRangeTangents;

layout(set = 1, binding = 5, std430) restrict buffer BiomeMinTemperatures {
    int values[];
} biomeMinTemperatures;

layout(set = 1, binding = 6, std430) restrict buffer BiomeMaxTemperatures {
    int values[];
} biomeMaxTemperatures;

// --- HUMIDITY --- 

layout(set = 2, binding = 0) uniform sampler2D HumiditySampler;

layout(set = 2, binding = 1, std430) restrict buffer HumidityDistributionPoints {
    float values[];
} humidityDistributionPoints;

layout(set = 2, binding = 2, std430) restrict buffer HumidityDistributionTangents {
    float values[];
} humidityDistributionTangents;

layout(set = 2, binding = 3, std430) restrict buffer HumidityRangePoints {
    float values[];
} humidityRangePoints;

layout(set = 2, binding = 4, std430) restrict buffer HumidityRangeTangents {
    float values[];
} humidityRangeTangents;

layout(set = 2, binding = 5, std430) restrict buffer BiomeMinHumidities {
    int values[];
} biomeMinHumidities;

layout(set = 2, binding = 6, std430) restrict buffer BiomeMaxHumidities {
    int values[];
} biomeMaxHumidities;


// =====================================================================================


int ToIndex(ivec3 XYZ, ivec3 dimension) {
	return XYZ.z + (XYZ.y * dimension.z) + (XYZ.x * dimension.y * dimension.z);
}

ivec3 ToIVec3(int XYZ, ivec3 dimension) {
    int x = XYZ / dimension.z / dimension.y % dimension.x;
    int y = XYZ / dimension.z % dimension.y;
    int z = XYZ % dimension.z;

    return ivec3(x, y, z);
}

vec2 BezierMixCubic(vec2 p0, vec2 p1, vec2 p2, float t) {
    vec2 a = mix(p0, p1, t);
    vec2 b = mix(p1, p2, t);

    return mix(a, b, t);
}

vec2 BezierMixQuadratic(vec2 p0, vec2 p1, vec2 p2, vec2 p3, float t) {
    vec2 a = BezierMixCubic(p0, p1, p2, t);
    vec2 b = BezierMixCubic(p1, p2, p3, t);

    return mix(a, b, t);
}

float BezierMixCubicF(float f0, float f1, float f2, float t) {
    float a = mix(f0, f1, t);
    float b = mix(f1, f2, t);

    return mix(a, b, t);
}

float BezierMixQuadraticF(float f0, float f1, float f2, float f3, float t) {
    float a = BezierMixCubicF(f0, f1, f2, t);
    float b = BezierMixCubicF(f1, f2, f3, t);

    return mix(a, b, t);
}

float SampleCurve(float ax, float ay, float aTan, float bx, float by, float bTan, float offset) {
    
    float controlPointDistance =  bx - ax;
    
    if (controlPointDistance < 0.001 && controlPointDistance > -0.001) {
        return 0;
    }

    offset /= controlPointDistance;
    controlPointDistance /= 3.0;

    float acy = ay + controlPointDistance * aTan;
    float bcy = by - controlPointDistance * bTan;

    return BezierMixQuadraticF(ay, acy, bcy, by, offset);
}

// Sample temperature buffers using SampleCurve().
float SampleTemperatureCurve(float temperature) {
    
    // Calculate bezier curve for temperature distribution.
    for (int i = 0; i < temperatureDistributionPoints.values.length(); i += 2) {
        
        float thisPointX = temperatureDistributionPoints.values[i];
        float nextPointX = temperatureDistributionPoints.values[i + 2];

        if (temperature >= thisPointX && temperature <= nextPointX) {
            
            float thisPointY = temperatureDistributionPoints.values[i + 1];
            float nextPointY = temperatureDistributionPoints.values[i + 3];

            float thisTangentR = temperatureDistributionTangents.values[i + 1];
            float nextTangentL = temperatureDistributionTangents.values[i + 2];

            temperature = SampleCurve(thisPointX, thisPointY, thisTangentR, nextPointX, nextPointY, nextTangentL, temperature);
            break;
        }
    }

    // Calculate bezier curve for temperature range.
    for (int i = 0; i < temperatureRangePoints.values.length(); i += 2) {
        
        float thisPointX = temperatureRangePoints.values[i];
        float nextPointX = temperatureRangePoints.values[i + 2];
        
        if (temperature >= thisPointX && temperature <= nextPointX) {

            float thisPointY = temperatureRangePoints.values[i + 1];
            float nextPointY = temperatureRangePoints.values[i + 3];

            float thisTangentR = temperatureRangeTangents.values[i + 1];
            float nextTangentL = temperatureRangeTangents.values[i + 2];

            temperature = SampleCurve(thisPointX, thisPointY, thisTangentR, nextPointX, nextPointY, nextTangentL, temperature);
            break;
        }
    }

    return temperature;
}

void main() {
    uint voxelIndexX = gl_GlobalInvocationID.x;
    uint voxelIndexY = gl_GlobalInvocationID.y << 4;
    uint voxelIndexZ = gl_GlobalInvocationID.z << 8;

    uint voxelIndex = voxelIndexX + voxelIndexY + voxelIndexZ;
    vec3 voxelPosition = ToIVec3(int(voxelIndex), ivec3(16, 16, 16));
    
    vec4 temperatureSample = texture(TemperatureSampler, vec2(voxelPosition.x, voxelPosition.z), 0);
    float temperature = SampleTemperatureCurve(temperatureSample.r);
    
    // Determine biome
    for (int i = 0; i < biomeMinTemperatures.values.length(); i ++)
    {
        if (temperature <= biomeMaxTemperatures.values[i] && temperature >= biomeMinTemperatures.values[i])
        {
            chunkData.voxelIDs[voxelIndex] = int(i);
            break;
        }
    }
}