#ifndef MATRIX_INCLUDE
#define MATRIX_INCLUDE

#define FLOAT4X4_IDENTITY float4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)
#define FLOAT3X3_IDENTITY float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1)
#define FLOAT4X4_ZERO float4x4(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
#define FLOAT3X3_ZERO float3x3(0, 0, 0, 0, 0, 0, 0, 0, 0)

#include "Quaternion.cginc"

float4x4 Inverse(float4x4 m) 
{
    float n11 = m[0][0], n12 = m[1][0], n13 = m[2][0], n14 = m[3][0];
    float n21 = m[0][1], n22 = m[1][1], n23 = m[2][1], n24 = m[3][1];
    float n31 = m[0][2], n32 = m[1][2], n33 = m[2][2], n34 = m[3][2];
    float n41 = m[0][3], n42 = m[1][3], n43 = m[2][3], n44 = m[3][3];

    float t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44;
    float t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44;
    float t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44;
    float t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

    float det = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
    float idet = 1.0f / det;

    float4x4 ret;

    ret[0][0] = t11 * idet;
    ret[0][1] = (n24 * n33 * n41 - n23 * n34 * n41 - n24 * n31 * n43 + n21 * n34 * n43 + n23 * n31 * n44 - n21 * n33 * n44) * idet;
    ret[0][2] = (n22 * n34 * n41 - n24 * n32 * n41 + n24 * n31 * n42 - n21 * n34 * n42 - n22 * n31 * n44 + n21 * n32 * n44) * idet;
    ret[0][3] = (n23 * n32 * n41 - n22 * n33 * n41 - n23 * n31 * n42 + n21 * n33 * n42 + n22 * n31 * n43 - n21 * n32 * n43) * idet;

    ret[1][0] = t12 * idet;
    ret[1][1] = (n13 * n34 * n41 - n14 * n33 * n41 + n14 * n31 * n43 - n11 * n34 * n43 - n13 * n31 * n44 + n11 * n33 * n44) * idet;
    ret[1][2] = (n14 * n32 * n41 - n12 * n34 * n41 - n14 * n31 * n42 + n11 * n34 * n42 + n12 * n31 * n44 - n11 * n32 * n44) * idet;
    ret[1][3] = (n12 * n33 * n41 - n13 * n32 * n41 + n13 * n31 * n42 - n11 * n33 * n42 - n12 * n31 * n43 + n11 * n32 * n43) * idet;

    ret[2][0] = t13 * idet;
    ret[2][1] = (n14 * n23 * n41 - n13 * n24 * n41 - n14 * n21 * n43 + n11 * n24 * n43 + n13 * n21 * n44 - n11 * n23 * n44) * idet;
    ret[2][2] = (n12 * n24 * n41 - n14 * n22 * n41 + n14 * n21 * n42 - n11 * n24 * n42 - n12 * n21 * n44 + n11 * n22 * n44) * idet;
    ret[2][3] = (n13 * n22 * n41 - n12 * n23 * n41 - n13 * n21 * n42 + n11 * n23 * n42 + n12 * n21 * n43 - n11 * n22 * n43) * idet;

    ret[3][0] = t14 * idet;
    ret[3][1] = (n13 * n24 * n31 - n14 * n23 * n31 + n14 * n21 * n33 - n11 * n24 * n33 - n13 * n21 * n34 + n11 * n23 * n34) * idet;
    ret[3][2] = (n14 * n22 * n31 - n12 * n24 * n31 - n14 * n21 * n32 + n11 * n24 * n32 + n12 * n21 * n34 - n11 * n22 * n34) * idet;
    ret[3][3] = (n12 * n23 * n31 - n13 * n22 * n31 + n13 * n21 * n32 - n11 * n23 * n32 - n12 * n21 * n33 + n11 * n22 * n33) * idet;

    return ret;
}

float4x4 m_scale(float4x4 m, float3 v)
{
    float x = v.x, y = v.y, z = v.z;

    m[0][0] *= x; m[1][0] *= y; m[2][0] *= z;
    m[0][1] *= x; m[1][1] *= y; m[2][1] *= z;
    m[0][2] *= x; m[1][2] *= y; m[2][2] *= z;
    m[0][3] *= x; m[1][3] *= y; m[2][3] *= z;

    return m;
}

float4x4 m_translate(float4x4 m, float3 v)
{
    float x = v.x, y = v.y, z = v.z;
    m[0][3] = x;
    m[1][3] = y;
    m[2][3] = z;
    return m;
}

float4x4 TRS(float3 position, float4 quat, float3 scale)
{
    float4x4 m = q_toMatrix(quat);
    return float4x4(m[0][0] * scale.x, m[0][1] * scale.y, m[0][2] * scale.z, position.x,
                    m[1][0] * scale.x, m[1][1] * scale.y, m[1][2] * scale.z, position.y,
                    m[2][0] * scale.x, m[2][1] * scale.y, m[2][2] * scale.z, position.z,
                    0, 0, 0, 1);
}

float4x4 AsDiagonal(in float4 v)
{
    return float4x4(v.x, 0, 0, 0,
                    0, v.y, 0, 0,
                    0, 0, v.z, 0,
                    0, 0, 0, v.w);
}

float3x3 multrnsp(in float4 column, in float4 row)
{
    return float3x3(row.xyz * column[0],row.xyz * column[1],row.xyz * column[2]);
}

float4x4 multrnsp4(in float4 column, float4 row)
{
    row[3] = 0;
    return float4x4(row * column[0],row * column[1],row * column[2], float4(0,0,0,0));
}

float FrobeniusNorm(in float4x4 m)
{
    return sqrt(dot(m._m00_m10_m20_m30,m._m00_m10_m20_m30) + 
                dot(m._m01_m11_m21_m31,m._m01_m11_m21_m31) +
                dot(m._m02_m12_m22_m32,m._m02_m12_m22_m32) + 
                dot(m._m03_m13_m23_m33,m._m03_m13_m23_m33));
}

#endif