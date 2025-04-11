#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
namespace Obi
{
    public interface IConstraint
    {
        int GetParticleCount();
        int GetParticle(int index);
    }
}
#endif