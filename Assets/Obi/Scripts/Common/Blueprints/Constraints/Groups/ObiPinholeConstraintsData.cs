using UnityEngine;
using System.Collections;
using System;

namespace Obi
{
    [Serializable]
    public class ObiPinholeConstraintsData : ObiConstraints<ObiPinholeConstraintsBatch>
    {

        public override ObiPinholeConstraintsBatch CreateBatch(ObiPinholeConstraintsBatch source = null)
        {
            return new ObiPinholeConstraintsBatch();
        }
    }
}
