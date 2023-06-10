using System;
using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Curves
{
    [Serializable]
    internal class QuaternionCurve
    {
        [SerializeField] private AnimationCurve xCurve;
        [SerializeField] private AnimationCurve yCurve;
        [SerializeField] private AnimationCurve zCurve;
        [SerializeField] private AnimationCurve wCurve;

        public QuaternionCurve()
        {
            xCurve = new();
            yCurve = new();
            zCurve = new();
            wCurve = new();
        }

        public void Add(float time, Quaternion value)
        {
            Add(time, value.x, value.y, value.z, value.w);
        }

        public void Add(float time, Vector4 value)
        {
            Add(time, value.x, value.y, value.z, value.w);
        }

        public void Add(float time, float x, float y, float z, float w)
        {
            xCurve.AddKey(time, x);
            yCurve.AddKey(time, y);
            zCurve.AddKey(time, z);
            wCurve.AddKey(time, w);
        }

        public void Load(IEnumerable<KeyValuePair<float, Quaternion>> pairs)
        {
            xCurve = new AnimationCurve();
            yCurve = new AnimationCurve();
            zCurve = new AnimationCurve();
            wCurve = new AnimationCurve();

            foreach (KeyValuePair<float, Quaternion> kvp in pairs)
            {
                Add(kvp.Key, kvp.Value);
            }
        }

        public Quaternion Evaluate(float time)
        {
            return new Quaternion(xCurve.Evaluate(time),
                yCurve.Evaluate(time),
                zCurve.Evaluate(time),
                wCurve.Evaluate(time));
        }
    }
}
