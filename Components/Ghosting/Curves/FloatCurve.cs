using System;
using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Curves
{
    [Serializable]
    internal class FloatCurve
    {
        [SerializeField] private AnimationCurve curve;

        public void Load(IEnumerable<KeyValuePair<float, float>> pairs)
        {
            curve = new AnimationCurve();

            foreach (KeyValuePair<float, float> kvp in pairs)
            {
                curve.AddKey(kvp.Key, kvp.Value);
            }
        }

        public float Evaluate(float time)
        {
            return curve.Evaluate(time);
        }
    }
}
