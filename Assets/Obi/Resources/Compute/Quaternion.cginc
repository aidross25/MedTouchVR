#ifndef QUATERNION_INCLUDE
#define QUATERNION_INCLUDE

#define QUATERNION_IDENTITY float4(0, 0, 0, 1)

typedef float4 quaternion;

// Quaternion multiplication
// http://mathworld.wolfram.com/Quaternion.html
quaternion qmul(quaternion q1, quaternion q2)
{
    return quaternion(
        q2.xyz * q1.w + q1.xyz * q2.w + cross(q1.xyz, q2.xyz),
        q1.w * q2.w - dot(q1.xyz, q2.xyz)
    );
}

// Vector rotation with a quaternion
// http://mathworld.wolfram.com/Quaternion.html
float3 rotate_vector(quaternion r, float3 v)
{
    float4 r_c = r * float4(-1, -1, -1, 1);
    return qmul(r, qmul(float4(v, 0), r_c)).xyz;
}

// A given angle of rotation about a given axis
quaternion axis_angle(float3 axis, float angle)
{
    float sn = sin(angle * 0.5);
    float cs = cos(angle * 0.5);
    return quaternion(axis * sn, cs);
}

// https://stackoverflow.com/questions/1171849/finding-quaternion-representing-the-rotation-from-one-vector-to-another
quaternion from_to_rotation(float3 v1, float3 v2)
{
    float4 q;
    float d = dot(v1, v2);
    if (d < -0.999999)
    {
        float3 right = float3(1, 0, 0);
        float3 up = float3(0, 1, 0);
        float3 tmp = cross(right, v1);
        if (length(tmp) < 0.000001)
        {
            tmp = cross(up, v1);
        }
        tmp = normalize(tmp);
        q = axis_angle(tmp, 3.14159265359f);
    } else if (d > 0.999999) {
        q = QUATERNION_IDENTITY;
    } else {
        q.xyz = cross(v1, v2);
        q.w = 1 + d;
        q = normalize(q);
    }
    return q;
}

float4 q_conj(float4 q)
{
    return float4(-q.x, -q.y, -q.z, q.w);
}

// https://jp.mathworks.com/help/aeroblks/quaternioninverse.html
quaternion q_inverse(quaternion q)
{
    quaternion conj = q_conj(q);
    return conj / (q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
}

quaternion q_diff(quaternion q1, quaternion q2)
{
    return q2 * q_inverse(q1);
}

quaternion q_look_at(float3 forward, float3 up)
{
    forward = normalize(forward);
    float3 right = normalize(cross(up, forward));
    up = cross(forward, right);

    float m00 = right.x;
    float m01 = right.y;
    float m02 = right.z;
    float m10 = up.x;
    float m11 = up.y;
    float m12 = up.z;
    float m20 = forward.x;
    float m21 = forward.y;
    float m22 = forward.z;

    float num8 = (m00 + m11) + m22;
    quaternion q = QUATERNION_IDENTITY;

    if (num8 > 0.0)
    {
        float num = sqrt(num8 + 1.0);
        q.w = num * 0.5;
        num = 0.5 / num;
        q.x = (m12 - m21) * num;
        q.y = (m20 - m02) * num;
        q.z = (m01 - m10) * num;
        return q;
    }
    else if ((m00 >= m11) && (m00 >= m22))
    {
        float num7 = sqrt(((1.0 + m00) - m11) - m22);
        float num4 = 0.5 / num7;
        q.x = 0.5 * num7;
        q.y = (m01 + m10) * num4;
        q.z = (m02 + m20) * num4;
        q.w = (m12 - m21) * num4;
        return q;
    }
    else if (m11 > m22)
    {
        float num6 = sqrt(((1.0 + m11) - m00) - m22);
        float num3 = 0.5 / num6;
        q.x = (m10 + m01) * num3;
        q.y = 0.5 * num6;
        q.z = (m21 + m12) * num3;
        q.w = (m20 - m02) * num3;
        return q;
    }
    else
    {
        float num5 = sqrt(((1.0 + m22) - m00) - m11);
        float num2 = 0.5 / num5;
        q.x = (m20 + m02) * num2;
        q.y = (m21 + m12) * num2;
        q.z = 0.5 * num5;
        q.w = (m01 - m10) * num2;
        return q;
    }
}

quaternion q_slerp(in quaternion a, in quaternion b, float t)
{
    // if either input is zero, return the other.
    if (length(a) == 0.0)
    {
        if (length(b) == 0.0)
            return QUATERNION_IDENTITY;
        else 
            return b;
    }
    else if (length(b) == 0.0)
    {
        return a;
    }
    else
    {
        float cosHalfAngle = a.w * b.w + dot(a.xyz, b.xyz);

        if (cosHalfAngle >= 1.0 || cosHalfAngle <= -1.0)
        {
            return a;
        }
        else 
        {
            if (cosHalfAngle < 0.0)
            {
                b.xyz = -b.xyz;
                b.w = -b.w;
                cosHalfAngle = -cosHalfAngle;
            }

            float blendA;
            float blendB;
            if (cosHalfAngle < 0.99)
            {
                // do proper slerp for big angles
                float halfAngle = acos(cosHalfAngle);
                float sinHalfAngle = sin(halfAngle);
                float oneOverSinHalfAngle = 1.0 / sinHalfAngle;
                blendA = sin(halfAngle * (1.0 - t)) * oneOverSinHalfAngle;
                blendB = sin(halfAngle * t) * oneOverSinHalfAngle;
            }
            else
            {
                // do lerp if angle is really small.
                blendA = 1.0 - t;
                blendB = t;
            }

            quaternion result = quaternion(blendA * a.xyz + blendB * b.xyz, blendA * a.w + blendB * b.w);

            if (length(result) > 0.0)
                return normalize(result);
            else 
                return QUATERNION_IDENTITY;
        }
    }
}

quaternion q_eulerXYZ(float3 euler) 
{
    float3 s, c;
    sincos(0.5f * euler, s, c);
    return quaternion(
        // s.x * c.y * c.z + s.y * s.z * c.x,
        // s.y * c.x * c.z - s.x * s.z * c.y,
        // s.z * c.x * c.y - s.x * s.y * c.z,
        // c.x * c.y * c.z + s.y * s.z * s.x
        float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(1.0f, -1.0f, -1.0f, 1.0f)
    );
}

float4x4 q_toMatrix(quaternion q)
{
    float xx = q.x * q.x;
    float xy = q.x * q.y;
    float xz = q.x * q.z;
    float xw = q.x * q.w;

    float yy = q.y * q.y;
    float yz = q.y * q.z;
    float yw = q.y * q.w;

    float zz = q.z * q.z;
    float zw = q.z * q.w;

    return float4x4(1 - 2 * (yy + zz), 2 * (xy - zw), 2 * (xz + yw), 0,
                        2 * (xy + zw), 1 - 2 * (xx + zz), 2 * (yz - xw), 0,
                        2 * (xz - yw), 2 * (yz + xw), 1 - 2 * (xx + yy), 0,
                        0, 0, 0, 1);
}

#endif 