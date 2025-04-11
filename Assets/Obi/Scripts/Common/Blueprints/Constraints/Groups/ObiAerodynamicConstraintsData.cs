using UnityEngine;
using System.Collections;
using System;

namespace Obi
{

    public interface IAerodynamicConstraintsUser
    {
        bool aerodynamicsEnabled
        {
            get;
            set;
        }

        float GetDrag(ObiAerodynamicConstraintsBatch batch, int constraintIndex);
        float GetLift(ObiAerodynamicConstraintsBatch batch, int constraintIndex);
    }

    [Serializable]
    public class ObiAerodynamicConstraintsData : ObiConstraints<ObiAerodynamicConstraintsBatch>
    {
        public override ObiAerodynamicConstraintsBatch CreateBatch(ObiAerodynamicConstraintsBatch source = null)
        {
            return new ObiAerodynamicConstraintsBatch();
        }
    }
}
