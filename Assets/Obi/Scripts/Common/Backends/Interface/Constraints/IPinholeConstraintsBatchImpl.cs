
namespace Obi
{
    public interface IPinholeConstraintsBatchImpl : IConstraintsBatchImpl
    {
        void SetPinholeConstraints(ObiNativeIntList particleIndices, ObiNativeIntList colliderIndices, ObiNativeVector4List offsets, ObiNativeFloatList edgeMus, ObiNativeIntList edgeRanges, ObiNativeFloatList edgeRangeMus, ObiNativeFloatList parameters, ObiNativeFloatList relativeVelocities, ObiNativeFloatList lambdas, int count);
    }
}
