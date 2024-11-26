#ifndef PATHFRAME_INCLUDE
#define PATHFRAME_INCLUDE

#include "MathUtils.cginc"

struct pathFrame
{
    float3 position;
    float3 tangent;
    float3 normal;
    float3 binormal;

    float4 color;
    float thickness;


    void Initialize(float3 position, float3 tangent, float3 normal, float3 binormal, float4 color, float thickness){
        this.position = position;
        this.normal = normal;
        this.tangent = tangent;
        this.binormal = binormal;
        this.color = color;
        this.thickness = thickness;
    }

    void Reset()
    {
        position = float3(0,0,0);
        tangent = float3(0,0,1);
        normal = float3(0,1,0);
        binormal = float3(1,0,0);
        color = float4(1,1,1,1);
        thickness = 0;
    }

    void SetTwist(float twist)
    {
        quaternion twistQ = axis_angle(tangent, radians(twist));
        normal = rotate_vector(twistQ, normal);
        binormal = rotate_vector(twistQ, binormal);
    }

    void Transport(pathFrame frame, float twist)
    {
        // Calculate delta rotation:
        quaternion rotQ = from_to_rotation(tangent, frame.tangent);
        quaternion twistQ = axis_angle(frame.tangent, radians(twist));
        quaternion finalQ = qmul(twistQ , rotQ);

        // Rotate previous frame axes to obtain the new ones:
        normal = rotate_vector(finalQ, normal);
        binormal = rotate_vector(finalQ, binormal);
        tangent = frame.tangent;
        position = frame.position;
        thickness = frame.thickness;
        color = frame.color;
    }

    void Transport(float3 newPosition, float3 newTangent, float twist)
    {
        // Calculate delta rotation:
        quaternion rotQ = from_to_rotation(tangent, newTangent);
        quaternion twistQ = axis_angle(newTangent, radians(twist));
        quaternion finalQ = qmul(twistQ, rotQ);

        // Rotate previous frame axes to obtain the new ones:
        normal = rotate_vector(finalQ, normal);
        binormal = rotate_vector(finalQ, binormal);
        tangent = newTangent;
        position = newPosition;

    }

    // Transport, hinting the normal.
    void Transport(float3 newPosition, float3 newTangent, float3 newNormal, float twist)
    {
        normal = rotate_vector(axis_angle(newTangent,radians(twist)), newNormal);
        tangent = newTangent;
        binormal = cross(normal, tangent);
        position = newPosition;
    }

    float3x3 ToMatrix(int mainAxis)
    {
        float3x3 basis;

        if (mainAxis == 0)
        {
            basis._m00_m10_m20 = tangent;
            basis._m01_m11_m21 = binormal;
            basis._m02_m12_m22 = normal;
        }
        else if (mainAxis == 1)
        {
            basis._m01_m11_m21 = tangent;
            basis._m02_m12_m22 = binormal;
            basis._m00_m10_m20 = normal;
        }
        else
        {
            basis._m02_m12_m22 = tangent;
            basis._m00_m10_m20 = binormal;
            basis._m01_m11_m21 = normal;
        }

       /*int xo = (mainAxis) % 3;
        int yo = (mainAxis + 1) % 3;
        int zo = (mainAxis + 2) % 3;

        basis[xo] = tangent;
        basis[yo] = binormal;
        basis[zo] = normal;*/

        return basis;
    }
};

void WeightedSum(float w1, float w2, float w3, in pathFrame c1, in pathFrame c2, in pathFrame c3, out pathFrame sum)
{
    sum.position.x = c1.position.x * w1 + c2.position.x * w2 + c3.position.x * w3;
    sum.position.y = c1.position.y * w1 + c2.position.y * w2 + c3.position.y * w3;
    sum.position.z = c1.position.z * w1 + c2.position.z * w2 + c3.position.z * w3;

    sum.tangent.x = c1.tangent.x * w1 + c2.tangent.x * w2 + c3.tangent.x * w3;
    sum.tangent.y = c1.tangent.y * w1 + c2.tangent.y * w2 + c3.tangent.y * w3;
    sum.tangent.z = c1.tangent.z * w1 + c2.tangent.z * w2 + c3.tangent.z * w3;

    sum.normal.x = c1.normal.x * w1 + c2.normal.x * w2 + c3.normal.x * w3;
    sum.normal.y = c1.normal.y * w1 + c2.normal.y * w2 + c3.normal.y * w3;
    sum.normal.z = c1.normal.z * w1 + c2.normal.z * w2 + c3.normal.z * w3;

    sum.binormal.x = c1.binormal.x * w1 + c2.binormal.x * w2 + c3.binormal.x * w3;
    sum.binormal.y = c1.binormal.y * w1 + c2.binormal.y * w2 + c3.binormal.y * w3;
    sum.binormal.z = c1.binormal.z * w1 + c2.binormal.z * w2 + c3.binormal.z * w3;

    sum.color.x = c1.color.x * w1 + c2.color.x * w2 + c3.color.x * w3;
    sum.color.y = c1.color.y * w1 + c2.color.y * w2 + c3.color.y * w3;
    sum.color.z = c1.color.z * w1 + c2.color.z * w2 + c3.color.z * w3;
    sum.color.w = c1.color.w * w1 + c2.color.w * w2 + c3.color.w * w3;

    sum.thickness = c1.thickness * w1 + c2.thickness * w2 + c3.thickness * w3;
}

pathFrame addFrames(pathFrame c1, pathFrame c2)
{
    pathFrame r;
    r.Initialize(c1.position + c2.position, c1.tangent + c2.tangent, c1.normal + c2.normal, c1.binormal + c2.binormal, c1.color + c2.color, c1.thickness + c2.thickness);
    return r;
}

pathFrame multiplyFrame(float f, pathFrame c)
{
    pathFrame r;
    r.Initialize(c.position * f, c.tangent * f, c.normal * f, c.binormal * f, c.color * f, c.thickness * f);
    return r;
}

#endif