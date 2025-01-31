#ifndef QUERYDEFS_INCLUDE
#define QUERYDEFS_INCLUDE

#define SPHERE_QUERY 0
#define BOX_QUERY 1
#define RAY_QUERY 2

struct queryShape
{
    float4 center;
    float4 size;
    int type;
    float contactOffset;
    float maxDistance;
    int filter;
};

struct queryResult
{
    float4 simplexBary; // point A, expressed as simplex barycentric coords for simplices.
    float4 queryPoint; // point B, expressed as a solver-space position.
    float4 normal;
    float dist;
    float distAlongRay; 
    int simplexIndex;
    int queryIndex;
};


#endif