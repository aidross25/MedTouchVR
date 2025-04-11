#ifndef COLLISIONMATERIAL_INCLUDE
#define COLLISIONMATERIAL_INCLUDE

struct collisionMaterial
{
    float dynamicFriction;
    float staticFriction;
    float rollingFriction;
    float stickiness;
    float stickDistance;
    int frictionCombine;
    int stickinessCombine;
    int rollingContacts;
};

StructuredBuffer<int> collisionMaterialIndices;
StructuredBuffer<collisionMaterial> collisionMaterials;

collisionMaterial EmptyCollisionMaterial()
{
    collisionMaterial m;
    m.dynamicFriction = 0;
    m.staticFriction = 0;
    m.rollingFriction = 0;
    m.stickiness = 0;
    m.stickDistance = 0;
    m.frictionCombine = 0;
    m.stickinessCombine = 0;
    m.rollingContacts = 0;
    return m;
}

collisionMaterial CombineWith(collisionMaterial a, collisionMaterial b)
{
    collisionMaterial result;
    int frictionCombineMode = max(a.frictionCombine, b.frictionCombine);
    int stickCombineMode = max(a.stickinessCombine, b.stickinessCombine);

    switch (frictionCombineMode)
    {
        case 0:
        default:
            result.dynamicFriction = (a.dynamicFriction + b.dynamicFriction) * 0.5f;
            result.staticFriction = (a.staticFriction + b.staticFriction) * 0.5f;
            result.rollingFriction = (a.rollingFriction + b.rollingFriction) * 0.5f;
            break;

        case 1:
            result.dynamicFriction = min(a.dynamicFriction, b.dynamicFriction);
            result.staticFriction = min(a.staticFriction, b.staticFriction);
            result.rollingFriction = min(a.rollingFriction, b.rollingFriction);
            break;

        case 2:
            result.dynamicFriction = a.dynamicFriction * b.dynamicFriction;
            result.staticFriction = a.staticFriction * b.staticFriction;
            result.rollingFriction = a.rollingFriction * b.rollingFriction;
            break;

        case 3:
            result.dynamicFriction = max(a.dynamicFriction, b.dynamicFriction);
            result.staticFriction = max(a.staticFriction, b.staticFriction);
            result.rollingFriction = max(a.rollingFriction, b.rollingFriction);
            break;
    }

    switch (stickCombineMode)
    {
        case 0:
        default:
            result.stickiness = (a.stickiness + b.stickiness) * 0.5f;
            break;

        case 1:
            result.stickiness = min(a.stickiness, b.stickiness);
            break;

        case 2:
            result.stickiness = a.stickiness * b.stickiness;
            break;

        case 3:
            result.stickiness = max(a.stickiness, b.stickiness);
            break;
    }

    result.stickDistance = max(a.stickDistance, b.stickDistance);
    result.rollingContacts = a.rollingContacts | b.rollingContacts;
    return result;
}

collisionMaterial CombineCollisionMaterials(int materialA, int materialB)
{
    // Combine collision materials:
    collisionMaterial combined;

    if (materialA >= 0 && materialB >= 0)
        combined = CombineWith(collisionMaterials[materialA], collisionMaterials[materialB]);
    else if (materialA >= 0)
        combined = collisionMaterials[materialA];
    else if (materialB >= 0)
        combined = collisionMaterials[materialB];
    else 
        combined = EmptyCollisionMaterial();

    return combined;
}

#endif