using UnityEngine;
using System.Collections;

namespace Obi
{

    public abstract class ObiSoftbodyBlueprintBase : ObiMeshBasedActorBlueprint
    {
        [SerializeField] [HideInInspector] protected ObiSkinMap m_Skinmap;
        public ObiSkinMap defaultSkinmap => m_Skinmap;

        protected override IEnumerator Initialize() { yield return null; }
    }
}