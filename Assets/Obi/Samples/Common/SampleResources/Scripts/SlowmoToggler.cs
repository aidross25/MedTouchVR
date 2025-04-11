using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi.Samples
{
    public class SlowmoToggler : MonoBehaviour
    {

        public void Slowmo(bool slowmo)
        {
            Time.timeScale = slowmo ? 0.25f : 1;
        }
    }
}
