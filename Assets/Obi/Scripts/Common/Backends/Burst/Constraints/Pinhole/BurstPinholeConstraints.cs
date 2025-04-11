#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using System;

namespace Obi
{
    public class BurstPinholeConstraints : BurstConstraintsImpl<BurstPinholeConstraintsBatch>
    {
        public BurstPinholeConstraints(BurstSolverImpl solver) : base(solver, Oni.ConstraintType.Pinhole)
        {
        }

        public override IConstraintsBatchImpl CreateConstraintsBatch()
        {
            var dataBatch = new BurstPinholeConstraintsBatch(this);
            batches.Add(dataBatch);
            return dataBatch;
        }

        public override void RemoveBatch(IConstraintsBatchImpl batch)
        {
            batches.Remove(batch as BurstPinholeConstraintsBatch);
            batch.Destroy();
        }
    }
}
#endif