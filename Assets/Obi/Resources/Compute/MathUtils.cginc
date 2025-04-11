#ifndef MATHUTILS_INCLUDE
#define MATHUTILS_INCLUDE

#define PI 3.14159265359f
#define SQRT2 1.41421356237f
#define SQRT3 1.73205080757f
#define EPSILON 0.0000001f
#define FLT_MAX 3.402823466e+38
#define FLT_MIN 1.175494351e-38

#define FLOAT4_ZERO float4(0, 0, 0, 0)
#define FLOAT4_EPSILON float4(EPSILON, EPSILON, EPSILON, EPSILON)

#define zero 0
#define one 1

#define PHASE_SELFCOLLIDE (1 << 24)
#define PHASE_FLUID (1 << 25)
#define PHASE_ONESIDED (1 << 26)

#include "Quaternion.cginc"
#include "Matrix.cginc"

float4 normalizesafe(in float4 v, float4 def = float4(0,0,0,0))
{
    float len = length(v);
    return (len < EPSILON) ? def : v/len;
}

float3 normalizesafe(in float3 v, float3 def = float3(0,0,0))
{
    float len = length(v);
    return (len < EPSILON) ? def : v/len;
}

inline float cmax( in float3 v)
{
    return max(max(v.x,v.y),v.z);
}

inline float3 nfmod(float3 a, float3 b)
{
    return a - b * floor(a / b);
}

inline float BaryScale(float4 coords)
{
    return 1.0 / dot(coords, coords);
}

float Remap01(float value, float min_, float max_)
{
    return (min(value, max_) - min(value, min_)) / (max_ - min_);
}

float EllipsoidRadius(float4 normSolverDirection, quaternion orientation, float3 radii)
{
    float3 localDir = rotate_vector(q_conj(orientation), normSolverDirection.xyz) / radii;
    float sqrNorm = dot(localDir, localDir);
    return sqrNorm > EPSILON ? sqrt(1 / sqrNorm) : radii.x;
}

float4 Project(float4 v, float4 onto)
{
    float len = dot(onto,onto);
    if (len < EPSILON)
        return FLOAT4_ZERO;
    return dot(onto, v) * onto / len;
}

float3 Project(float3 v, float3 onto)
{
    float len = dot(onto,onto);
    if (len < EPSILON)
        return float3(0,0,0);
    return dot(onto, v) * onto / len;
}

inline void OneSidedNormal(float4 forward, inout float4 normal)
{
    float d = dot(normal.xyz, forward.xyz);
    if (d < 0) normal -= 2 * d * forward;
}

quaternion ExtractRotation(float3x3 m, quaternion rotation, int iterations)
{
    float4x4 R;
    for (int i = 0; i < iterations; ++i)
    {
        R = q_toMatrix(rotation);
        float3 omega = (cross(R._m00_m10_m20, m._m00_m10_m20) + cross(R._m01_m11_m21, m._m01_m11_m21) + cross(R._m02_m12_m22, m._m02_m12_m22)) /
                       (abs(dot(R._m00_m10_m20, m._m00_m10_m20) + dot(R._m01_m11_m21, m._m01_m11_m21) + dot(R._m02_m12_m22, m._m02_m12_m22)) + EPSILON);

        float w = length(omega);
        if (w < EPSILON)
            break;

        rotation = normalize(qmul(axis_angle((1.0f / w) * omega, w), rotation));
    }
    return rotation;
}

quaternion ExtractRotation(float4x4 m, quaternion rotation, int iterations)
{
    return ExtractRotation((float3x3) m, rotation, iterations);
}

float4 GetParticleInertiaTensor(in float4 principalRadii, in float invRotationalMass)
{
    float4 sqrRadii = principalRadii * principalRadii;
    return 0.2f / (invRotationalMass + EPSILON) * float4(sqrRadii[1] + sqrRadii[2],
                                                         sqrRadii[0] + sqrRadii[2],
                                                         sqrRadii[0] + sqrRadii[1], 0);
}

float4x4 TransformInertiaTensor(float4 tensor, quaternion rotation)
{
    float4x4 rotMatrix = q_toMatrix(rotation);
    return mul(rotMatrix, mul(AsDiagonal(tensor), transpose(rotMatrix)));
}
        
float RotationalInvMass(float4x4 inverseInertiaTensor, float4 pos, float4 direction)
{
    float4 cr = mul(inverseInertiaTensor, float4(cross(pos.xyz, direction.xyz), 0));
    return dot(cross(cr.xyz, pos.xyz), direction.xyz);
}

float4 NearestPointOnEdge(float4 a, float4 b, float4 p, out float mu, bool clampToSegment = true)
{
    float4 ap = p - a;
    float4 ab = b - a;
    ap.w = 0;
    ab.w = 0;

    mu = dot(ap, ab) / (dot(ab, ab) + EPSILON);

    if (clampToSegment)
        mu = saturate(mu);

    float4 result = a + ab * mu;
    result.w = 0;
    return result;
}

float3 NearestPointOnEdge(float3 a, float3 b, float3 p, out float mu, bool clampToSegment = true)
{
    float3 ap = p - a;
    float3 ab = b - a;

    mu = dot(ap, ab) / (dot(ab, ab) + EPSILON);

    if (clampToSegment)
        mu = saturate(mu);

    float3 result = a + ab * mu;
    return result;
}

float RaySphereIntersection(float3 rayOrigin, float3 rayDirection, float3 center, float radius)
{
    float3 oc = rayOrigin - center;

    float a = dot(rayDirection, rayDirection);
    float b = 2.0 * dot(oc, rayDirection);
    float c = dot(oc, oc) - radius * radius;
    float discriminant = b * b - 4 * a * c;
    if (discriminant < 0){
        return -1.0f;
    }
    else{
        return (-b - sqrt(discriminant)) / (2.0f * a);
    }
}

struct CachedTri
{
    float4 vertex;
    float4 edge0;
    float4 edge1;
    float4 data;

    void Cache(in float4 v1,
               in float4 v2,
               in float4 v3)
    {
        vertex = v1;
        edge0 = v2 - v1;
        edge1 = v3 - v1;
        data = float4(0,0,0,0);
        data[0] = dot(edge0, edge0);
        data[1] = dot(edge0, edge1);
        data[2] = dot(edge1, edge1);
        data[3] = data[0] * data[2] - data[1] * data[1];
    }
};

float4 NearestPointOnTri(in CachedTri tri,
                         in float4 p,
                         out float4 bary)
{
    float4 v0 = tri.vertex - p;
    float b0 = dot(tri.edge0, v0);
    float b1 = dot(tri.edge1, v0);
    float t0 = tri.data[1] * b1 - tri.data[2] * b0;
    float t1 = tri.data[1] * b0 - tri.data[0] * b1;

    if (t0 + t1 <= tri.data[3])
    {
        if (t0 < zero)
        {
            if (t1 < zero)  // region 4
            {
                if (b0 < zero)
                {
                    t1 = zero;
                    if (-b0 >= tri.data[0])  // V0
                        t0 = one;
                    else  // E01
                        t0 = -b0 / tri.data[0];
                }
                else
                {
                    t0 = zero;
                    if (b1 >= zero)  // V0
                        t1 = zero;
                    else if (-b1 >= tri.data[2])  // V2
                        t1 = one;
                    else  // E20
                        t1 = -b1 / tri.data[2];
                }
            }
            else  // region 3
            {
                t0 = zero;
                if (b1 >= zero)  // V0
                    t1 = zero;
                else if (-b1 >= tri.data[2])  // V2
                    t1 = one;
                else  // E20
                    t1 = -b1 / tri.data[2];
            }
        }
        else if (t1 < zero)  // region 5
        {
            t1 = zero;
            if (b0 >= zero)  // V0
                t0 = zero;
            else if (-b0 >= tri.data[0])  // V1
                t0 = one;
            else  // E01
                t0 = -b0 / tri.data[0];
        }
        else  // region 0, interior
        {
            float invDet = one / tri.data[3];
            t0 *= invDet;
            t1 *= invDet;
        }
    }
    else
    {
        float tmp0, tmp1, numer, denom;

        if (t0 < zero)  // region 2
        {
            tmp0 = tri.data[1] + b0;
            tmp1 = tri.data[2] + b1;
            if (tmp1 > tmp0)
            {
                numer = tmp1 - tmp0;
                denom = tri.data[0] - 2 * tri.data[1] + tri.data[2];
                if (numer >= denom)  // V1
                {
                    t0 = one;
                    t1 = zero;
                }
                else  // E12
                {
                    t0 = numer / denom;
                    t1 = one - t0;
                }
            }
            else
            {
                t0 = zero;
                if (tmp1 <= zero)  // V2
                    t1 = one;
                else if (b1 >= zero)  // V0
                    t1 = zero;
                else  // E20
                    t1 = -b1 / tri.data[2];
            }
        }
        else if (t1 < zero)  // region 6
        {
            tmp0 = tri.data[1] + b1;
            tmp1 = tri.data[0] + b0;
            if (tmp1 > tmp0)
            {
                numer = tmp1 - tmp0;
                denom = tri.data[0] - 2 * tri.data[1] + tri.data[2];
                if (numer >= denom)  // V2
                {
                    t1 = one;
                    t0 = zero;
                }
                else  // E12
                {
                    t1 = numer / denom;
                    t0 = one - t1;
                }
            }
            else
            {
                t1 = zero;
                if (tmp1 <= zero)  // V1
                    t0 = one;
                else if (b0 >= zero)  // V0
                    t0 = zero;
                else  // E01
                    t0 = -b0 / tri.data[0];
            }
        }
        else  // region 1
        {
            numer = tri.data[2] + b1 - tri.data[1] - b0;
            if (numer <= zero)  // V2
            {
                t0 = zero;
                t1 = one;
            }
            else
            {
                denom = tri.data[0] - 2 * tri.data[1] + tri.data[2];
                if (numer >= denom)  // V1
                {
                    t0 = one;
                    t1 = zero;
                }
                else  // 12
                {
                    t0 = numer / denom;
                    t1 = one - t0;
                }
            }
        }
    }

    bary = float4(1 - (t0 + t1), t0, t1,0);
    return tri.vertex + t0 * tri.edge0 + t1 * tri.edge1;
}

float3 unitOrthogonal(float3 input)
{
    // Find a vector to cross() the input with.
    if (!(input.x < input.z * EPSILON)
    ||  !(input.y < input.z * EPSILON))
    {
        float invnm = 1 / length(input.xy);
        return float3(-input.y * invnm, input.x * invnm, 0);
    }
    else
    {
        float invnm = 1 / length(input.yz);
        return float3(0, -input.z * invnm, input.y * invnm);
    }
}

// D is symmetric, S is an eigen value
float3 EigenVector(float3x3 D, float S)
{
    // Compute a cofactor matrix of D - sI.
    float3 c0 = D._m00_m10_m20; c0[0] -= S;
    float3 c1 = D._m01_m11_m21; c1[1] -= S;
    float3 c2 = D._m02_m12_m22; c2[2] -= S;

    // Upper triangular matrix
    float3 c0p = float3(c1[1] * c2[2] - c2[1] * c2[1], 0, 0);
    float3 c1p = float3(c2[1] * c2[0] - c1[0] * c2[2], c0[0] * c2[2] - c2[0] * c2[0], 0);
    float3 c2p = float3(c1[0] * c2[1] - c1[1] * c2[0], c1[0] * c2[0] - c0[0] * c2[1], c0[0] * c1[1] - c1[0] * c1[0]);

    // Get a column vector with a largest norm (non-zero).
    float C01s = c1p[0] * c1p[0];
    float C02s = c2p[0] * c2p[0];
    float C12s = c2p[1] * c2p[1];
    float3 norm = float3(c0p[0] * c0p[0] + C01s + C02s,
                         C01s + c1p[1] * c1p[1] + C12s,
                         C02s + C12s + c2p[2] * c2p[2]);

    // index of largest:
    int index = 0;
    if (norm[0] > norm[1] && norm[0] > norm[2])
        index = 0;
    else if (norm[1] > norm[0] && norm[1] > norm[2])
        index = 1;
    else
        index = 2;

    float3 V = float3(0,0,0);

    // special case
    if (norm[index] < EPSILON)
    {
        V[0] = 1; return V;
    }
    else if (index == 0)
    {
        V[0] = c0p[0]; V[1] = c1p[0]; V[2] = c2p[0];
        return normalize(V);
    }
    else if (index == 1)
    {
        V[0] = c1p[0]; V[1] = c1p[1]; V[2] = c2p[1];
        return normalize(V);
    }
    else
    {
        V = c2p;
        return normalize(V);
    }
}

static float3 EigenValues(float3x3 D)
{
    float one_third = 1 / 3.0f;
    float one_sixth = 1 / 6.0f;
    float three_sqrt = sqrt(3.0f);

    float3 c0 = D._m00_m10_m20;
    float3 c1 = D._m01_m11_m21;
    float3 c2 = D._m02_m12_m22;

    float m = one_third * (c0[0] + c1[1] + c2[2]);

    // K is D - I*diag(S)
    float K00 = c0[0] - m;
    float K11 = c1[1] - m;
    float K22 = c2[2] - m;

    float K01s = c1[0] * c1[0];
    float K02s = c2[0] * c2[0];
    float K12s = c2[1] * c2[1];

    float q = 0.5f * (K00 * (K11 * K22 - K12s) - K22 * K01s - K11 * K02s) + c1[0] * c2[1] * c0[2];
    float p = one_sixth * (K00 * K00 + K11 * K11 + K22 * K22 + 2 * (K01s + K02s + K12s));

    float p_sqrt = sqrt(p);

    float tmp = p * p * p - q * q;
    float phi = one_third * atan2(sqrt(max(0, tmp)), q);
    float phi_c = cos(phi);
    float phi_s = sin(phi);
    float sqrt_p_c_phi = p_sqrt * phi_c;
    float sqrt_p_3_s_phi = p_sqrt * three_sqrt * phi_s;

    float e0 = m + 2 * sqrt_p_c_phi;
    float e1 = m - sqrt_p_c_phi - sqrt_p_3_s_phi;
    float e2 = m - sqrt_p_c_phi + sqrt_p_3_s_phi;

    float aux;
    if (e0 > e1)
    {
        aux = e0;
        e0 = e1;
        e1 = aux;
    }
    if (e0 > e2)
    {
        aux = e0;
        e0 = e2;
        e2 = aux;
    }
    if (e1 > e2)
    {
        aux = e1;
        e1 = e2;
        e2 = aux;
    }

    return float3(e2, e1, e0);
}

void EigenSolve(float3x3 D, out float3 S, out float3x3 V)
{
    // D is symmetric
    // S is a vector whose elements are eigenvalues
    // V is a matrix whose columns are eigenvectors
    S = EigenValues(D);
    float3 V0, V1, V2;

    if (S[0] - S[1] > S[1] - S[2])
    {
        V0 = EigenVector(D, S[0]);
        if (S[1] - S[2] < EPSILON)
        {
            V2 = unitOrthogonal(V0);
        }
        else
        {
            V2 = EigenVector(D, S[2]); V2 -= V0 * dot(V0, V2); V2 = normalize(V2);
        }
        V1 = cross(V2, V0);
    }
    else
    {
        V2 = EigenVector(D, S[2]);
        if (S[0] - S[1] < EPSILON)
        {
            V1 = unitOrthogonal(V2);
        }
        else
        {
            V1 = EigenVector(D, S[1]); V1 -= V2 * dot(V2, V1); V1 = normalize(V1);
        }
        V0 = cross(V1, V2);
    }

    V._m00_m10_m20 = V0;
    V._m01_m11_m21 = V1;
    V._m02_m12_m22 = V2;
}

float4 UnpackFloatRGBA(float v)
{
    uint rgba = asuint(v);
    float r = ((rgba & 0xff000000) >> 24) / 255.0;
    float g = ((rgba & 0x00ff0000) >> 16) / 255.0;
    float b = ((rgba & 0x0000ff00) >> 8) / 255.0;
    float a = (rgba & 0x000000ff) / 255.0;
    return float4(r, g, b, a);
}

float PackFloatRGBA(float4 enc)
{
    uint rgba = ((uint)(enc.x * 255.0) << 24) +
                ((uint)(enc.y * 255.0) << 16) +
                ((uint)(enc.z * 255.0) << 8) +
                (uint)(enc.w * 255.0);
    return asfloat(rgba);
}

float2 UnpackFloatRG(float v)
{
    uint rgba = asuint(v);
    float r = ((rgba & 0xffff0000) >> 16) / 65535.0;
    float g = (rgba & 0x0000ffff) / 65535.0;
    return float2(r, g);
}

float PackFloatRG(float2 enc)
{
    uint rgba = ((uint)(enc.x * 65535.0) << 16) +
                (uint)(enc.y * 65535.0);
    return asfloat(rgba);
}

#endif