using System;
using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Curves
{
    [Serializable]
    internal class Vector3Curve
    {
        [SerializeField] private AnimationCurve xCurve;
        [SerializeField] private AnimationCurve yCurve;
        [SerializeField] private AnimationCurve zCurve;

        public Vector3Curve()
        {
            xCurve = new();
            yCurve = new();
            zCurve = new();
        }

        public void Add(float time, Vector3 value)
        {
            Add(time, value.x, value.y, value.z);
        }

        public void Add(float time, float x, float y, float z)
        {
            xCurve.AddKey(time, x);
            yCurve.AddKey(time, y);
            zCurve.AddKey(time, z);
        }

        public void Load(IEnumerable<KeyValuePair<float, Vector3>> pairs)
        {
            xCurve = new AnimationCurve();
            yCurve = new AnimationCurve();
            zCurve = new AnimationCurve();

            foreach (KeyValuePair<float, Vector3> kvp in pairs)
            {
                Add(kvp.Key, kvp.Value);
            }
        }

        public Vector3 Evaluate(float time)
        {
            return new Vector3(xCurve.Evaluate(time), yCurve.Evaluate(time), zCurve.Evaluate(time));
        }
    }
}
