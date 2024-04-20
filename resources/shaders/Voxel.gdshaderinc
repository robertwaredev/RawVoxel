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