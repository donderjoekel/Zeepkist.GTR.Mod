using System;
using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Curves
{
    [Serializable]
    internal class BoolCurve
    {
        [SerializeField] private AnimationCurve curve;

        public void Load(IEnumerable<KeyValuePair<float, bool>> pairs)
        {
            curve = new AnimationCurve();

            foreach (KeyValuePair<float, bool> kvp in pairs)
            {
                curve.AddKey(kvp.Key, kvp.Value ? 1 : 0);
            }
        }

        public bool Evaluate(float time)
        {
            return Mathf.Approximately(curve.Evaluate(time), 1);
        }
    }
}
