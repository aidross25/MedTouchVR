#ifndef FLUIDKERNELS_INCLUDE
#define FLUIDKERNELS_INCLUDE

#include "SolverParameters.cginc"

float Poly6(float r, float h)
{
    float h2 = h * h;
    float h4 = h2 * h2;
    float h8 = h4 * h4;

    float rl = min(r, h);
    float hr = h2 - rl * rl;

    if (mode)
       return 4.0f / PI / h8 * hr * hr * hr;
    else
       return 315.0f / (64.0 * PI) / (h8 * h) * hr * hr * hr;
}

float Spiky(float r, float h)
{
    float h2 = h * h;
    float h4 = h2 * h2;

    float rl = min(r, h);
    float hr = h - rl;

    if (mode)
        return -30.0f / PI / (h4 * h) * hr * hr;
    else
        return  -45.0f / PI / (h4 * h2) * hr * hr;
}

float Cohesion(float r, float h)
{
    return cos(min(r, h) * 3 * PI / (2 * h));
}

#endif