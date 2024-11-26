#ifndef SOLVERPARAMS_INCLUDE
#define SOLVERPARAMS_INCLUDE

int mode;
int interpolation;
float3 gravity;
float damping;
float worldLinearInertiaScale;
float worldAngularInertiaScale;
float maxAnisotropy;
float sleepThreshold;
float maxVelocity;
float maxAngularVelocity;
float collisionMargin;
float maxDepenetration;
float colliderCCD;
float particleCCD;
float shockPropagation;
int surfaceCollisionIterations;
float surfaceCollisionTolerance;

#endif