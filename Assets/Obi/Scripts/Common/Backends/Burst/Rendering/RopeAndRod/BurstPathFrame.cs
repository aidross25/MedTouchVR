#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;

namespace Obi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BurstPathFrame
    {
        public enum Axis
        {
            X = 0,
            Y = 1,
            Z = 2
        }

        public float3 position;
        public float3 tangent;
        public float3 normal;
        public float3 binormal;
        public float4 color;
        public float thickness;

        public BurstPathFrame(float3 position, float3 tangent, float3 normal, float3 binormal, float4 color, float thickness)
        {
            this.position = position;
            this.normal = normal;
            this.tangent = tangent;
            this.binormal = binormal;
            this.color = color;
            this.thickness = thickness;
        }

        public void Reset()
        {
            position = float3.zero;
            tangent = new float3(0, 0, 1);
            normal = new float3(0, 1, 0);
            binormal = new float3(1, 0, 0);
            color = new float4(1, 1, 1, 1);
            thickness = 0;
        }

        public static BurstPathFrame operator +(BurstPathFrame c1, BurstPathFrame c2)
        {
            return new BurstPathFrame(c1.position + c2.position, c1.tangent + c2.tangent, c1.normal + c2.normal, c1.binormal + c2.binormal, c1.color + c2.color, c1.thickness + c2.thickness);
        }

        public static BurstPathFrame operator *(float f, BurstPathFrame c)
        {
            return new BurstPathFrame(c.position * f, c.tangent * f, c.normal * f, c.binormal * f, c.color * f, c.thickness * f);
        }

        public static void WeightedSum(float w1, float w2, float w3, in BurstPathFrame c1, in BurstPathFrame c2, in BurstPathFrame c3, ref BurstPathFrame sum)
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

        public void SetTwist(float twist)
        {
            quaternion twistQ = quaternion.AxisAngle(tangent, math.radians(twist));
            normal = math.mul(twistQ, normal);
            binormal = math.mul(twistQ, binormal);
        }

        public static quaternion FromToRotation(float3 aFrom, float3 aTo)
        {
            float3 axis = math.cross(aFrom, aTo);
            float angle = math.acos(math.clamp(math.dot(math.normalize(aFrom), math.normalize(aTo)), -1f, 1f));
            return quaternion.AxisAngle(math.normalize(axis), angle);
        }

        public void SetTwistAndTangent(float twist, float3 tangent)
        {
            this.tangent = tangent;
            normal = math.normalize(new float3(tangent.y, tangent.x, 0));
            binormal = math.cross(normal, tangent);

            quaternion twistQ = quaternion.AxisAngle(tangent, math.radians(twist));
            normal = math.mul(twistQ, normal);
            binormal = math.mul(twistQ, binormal);
        }

        public void Transport(in BurstPathFrame frame, float twist)
        {
            // Calculate delta rotation:
            quaternion rotQ = Quaternion.FromToRotation(tangent, frame.tangent);
            quaternion twistQ = quaternion.AxisAngle(frame.tangent, math.radians(twist));
            quaternion finalQ = math.mul(twistQ, rotQ);

            // Rotate previous frame axes to obtain the new ones:
            normal = math.mul(finalQ, normal);
            binormal = math.mul(finalQ, binormal);
            tangent = frame.tangent;
            position = frame.position;
            thickness = frame.thickness;
            color = frame.color;
        }

        public void Transport(float3 newPosition, float3 newTangent, float twist)
        {
            // Calculate delta rotation:
            quaternion rotQ = Quaternion.FromToRotation(tangent, newTangent);
            quaternion twistQ = quaternion.AxisAngle(newTangent, math.radians(twist));
            quaternion finalQ = math.mul(twistQ, rotQ);

            // Rotate previous frame axes to obtain the new ones:
            normal = math.mul(finalQ, normal);
            binormal = math.mul(finalQ, binormal);
            tangent = newTangent;
            position = newPosition;

        }

        // Transport, hinting the normal.
        public void Transport(float3 newPosition, float3 newTangent, float3 newNormal, float twist)
        {
            normal = math.mul(quaternion.AxisAngle(newTangent, math.radians(twist)), newNormal);
            tangent = newTangent;
            binormal = math.cross(normal, tangent);
            position = newPosition;
        }

        public float3x3 ToMatrix(int mainAxis)
        {
            float3x3 basis = new float3x3();

            int xo = (mainAxis) % 3;
            int yo = (mainAxis + 1) % 3;
            int zo = (mainAxis + 2) % 3;

            basis[xo] = tangent;
            basis[yo] = binormal;
            basis[zo] = normal;

            return basis;
        }

        public void DebugDraw(float size)
        {
            Debug.DrawRay(position, binormal * size, Color.red);
            Debug.DrawRay(position, normal * size, Color.green);
            Debug.DrawRay(position, tangent * size, Color.blue);
        }
    }
}
#endif

