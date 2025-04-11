#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;

namespace Obi
{
    public class BurstJobHandle : IObiJobHandle
    {
        public JobHandle jobHandle { get; set; } = new JobHandle();

        public void Complete()
        {
            jobHandle.Complete();
        }

        public void Release()
        {
            jobHandle = new JobHandle();
        }
    }
}
#endif

