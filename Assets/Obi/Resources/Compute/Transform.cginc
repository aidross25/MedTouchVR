#ifndef TRANSFORM_INCLUDE
#define TRANSFORM_INCLUDE

#include "Quaternion.cginc"
#include "Integration.cginc"

struct transform
{
    float4 translation;
    float4 scale;
    quaternion rotation;
    
    transform Transform(float4 translation_, quaternion rotation_, float4 scale_)
    {
        // make sure there are good values in the 4th component:
        translation_[3] = 0;
        scale_[3] = 1;

        transform t;
        t.translation = translation_;
        t.rotation = rotation_;
        t.scale = scale_;
        return t;
    }
    
    transform Inverse()
    {
        return Transform(float4(rotate_vector(q_conj(rotation),(translation / -scale).xyz),0),
                         q_conj(rotation),
                         1 / scale);
    }

    transform Interpolate(transform other, float translationalMu, float rotationalMu, float scaleMu)
    { 
        return Transform(lerp(translation, other.translation, translationalMu),
                         q_slerp(rotation, other.rotation, rotationalMu),
                         lerp(scale, other.scale, scaleMu));
    }

    transform Integrate(float4 linearVelocity, float4 angularVelocity, float dt)
    {
        return Transform(IntegrateLinear(translation, linearVelocity, dt),
                         IntegrateAngular(rotation, angularVelocity, dt),
                         scale);
    }

    float4 TransformPoint(float4 pnt)
    {
        return float4(translation.xyz + rotate_vector(rotation, (pnt * scale).xyz),0);
    }

    float4 InverseTransformPoint(float4 pnt)
    {
        return float4(rotate_vector(q_conj(rotation),(pnt - translation).xyz) / scale.xyz , 0);
    }

    float4 TransformPointUnscaled(float4 pnt)
    {
        return float4(translation.xyz + rotate_vector(rotation,pnt.xyz), 0);
    }
    
    float4 InverseTransformPointUnscaled(float4 pnt)
    {
        return float4(rotate_vector(q_conj(rotation), (pnt - translation).xyz), 0);
    }

    float4 TransformDirection(float4 dir)
    {
        return float4(rotate_vector(rotation, dir.xyz), 0);
    }

    float4 InverseTransformDirection(float4 dir)
    {
        return float4(rotate_vector(q_conj(rotation), dir.xyz), 0);
    }

    float4 TransformVector(float4 vect)
    {
        return float4(rotate_vector(rotation, (vect * scale).xyz), 0);
    }

    float4 InverseTransformVector(float4 vect)
    {
        return float4(rotate_vector(q_conj(rotation),vect.xyz) / scale.xyz, 0);
    }

    transform Multiply(transform b)
    {
        return Transform(this.TransformPoint(b.translation),
                         qmul(this.rotation,b.rotation),
                         this.scale * b.scale);
    }
};




#endif