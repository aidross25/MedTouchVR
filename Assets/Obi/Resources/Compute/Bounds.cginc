#ifndef BOUNDS_INCLUDE
#define BOUNDS_INCLUDE

#include "Transform.cginc"
#include "Matrix.cginc"

struct aabb
{
    float4 min_;
    float4 max_;

    void FromTriangle(float4 v1, float4 v2, float4 v3, float4 margin)
    {
        min_ = min(min(v1, v2), v3) - margin;
        max_ = max(max(v1, v2), v3) + margin;
    }

    void FromEdge(float4 v1, float4 v2, float4 radius)
    {
        min_ = min(v2 - radius, v1 - radius);
        max_ = max(v2 + radius, v1 + radius);
    }

    void FromParticle(float4 v1, float radius)
    {
        min_ = v1 - radius;
        max_ = v1 + radius;
    }

    bool IntersectsAabb(in aabb b, bool in2D = false)
    {
        if (in2D)
        {
        return (min_[0] <= b.max_[0] && max_[0] >= b.min_[0]) &&
               (min_[1] <= b.max_[1] && max_[1] >= b.min_[1]);
        }
        else
        {
        return (min_[0] <= b.max_[0] && max_[0] >= b.min_[0]) &&
               (min_[1] <= b.max_[1] && max_[1] >= b.min_[1]) &&
               (min_[2] <= b.max_[2] && max_[2] >= b.min_[2]);
        }
    }

    float AverageAxisLength()
    {
        float4 d = max_ - min_;
        return (d.x + d.y + d.z) * 0.33f;
    }

    float MaxAxisLength()
    {
        float4 d = max_ - min_;
        return max(max(d.x,d.y),d.z);
    }

    void EncapsulateParticle(in float4 position, float radius)
    {
        min_ = min(min(min_, position - radius), position - radius);
        max_ = max(max(max_, position + radius), position + radius);
    }

    void EncapsulateParticle(in float4 previousPosition, in float4 position, float radius)
    {
        min_ = min(min(min_, position - radius), previousPosition - radius);
        max_ = max(max(max_, position + radius), previousPosition + radius);
    }

    void EncapsulateBounds(in aabb bounds)
    {
        min_ = min(min_,bounds.min_);
        max_ = max(max_,bounds.max_);
    }

    void Expand(float4 amount)
    {
        min_ -= amount;
        max_ += amount;
    }

    void Sweep(float4 velocity)
    {
        min_ = min(min_, min_ + velocity);
        max_ = max(max_, max_ + velocity);
    }

    float4 Center()
    {
        return (min_ + (max_ - min_) * 0.5f);
    }

    void Transform(in float4x4 transform)
    {
        float3 xa = transform._m00_m10_m20 * min_.x;
        float3 xb = transform._m00_m10_m20 * max_.x;

        float3 ya = transform._m01_m11_m21 * min_.y;
        float3 yb = transform._m01_m11_m21 * max_.y;

        float3 za = transform._m02_m12_m22 * min_.z;
        float3 zb = transform._m02_m12_m22 * max_.z;

        min_ = float4(min(xa, xb) + min(ya, yb) + min(za, zb) + transform._m03_m13_m23, 0);
        max_ = float4(max(xa, xb) + max(ya, yb) + max(za, zb) + transform._m03_m13_m23, 0);
    }

    void Transform(in transform transform)
    {
        Transform(TRS(transform.translation.xyz, transform.rotation, transform.scale.xyz));
    }

    aabb Transformed(in float4x4 trfm)
    {
        aabb cpy = this;
        cpy.Transform(trfm);
        return cpy;
    }

    aabb Transformed(in transform trfm)
    {
        aabb cpy = this;
        cpy.Transform(trfm);
        return cpy;
    }
};

#endif