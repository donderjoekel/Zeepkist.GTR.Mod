/*
 * Copyright (c) 2020 Oliver Sradnick
 */

using System;
using UnityEngine;

namespace Utilities
{
    public static class Compression
    {
        private const float MaxAbsValue = 500f;
        private const float MaxPrecisionValue = 50f;

        private static readonly Vector3 minPosition = new(-10, -10, -10);
        private static readonly Vector3 maxPosition = new(10, 10, 10);
        private static readonly Vector3 minRotation = new(-360, -360, -360);
        private static readonly Vector3 maxRotation = new(360, 360, 360);

        private static int PackVector2(Vector2 vector)
        {
            return Pack2Floats(vector.x, vector.y);
        }

        private static Vector2 UnpackVector(int vector)
        {
            (float x, float y) = Unpack2Floats(vector);
            return new Vector2(x, y);
        }

        public static float PackPosition(Vector3 current)
        {
            return PackVector3(current, minPosition, maxPosition);
        }

        public static float PackRotation(Vector3 current)
        {
            return PackVector3(current, minRotation, maxRotation);
        }

        /// <summary>
        /// Normalize current vector based on min and max range.
        /// Results in a percentage between 0 - 1 for x, y and z axis.
        /// Packing x , y and z into 1 float with bitshifting.
        /// </summary>
        /// <returns></returns>
        private static float PackVector3(Vector3 current, Vector3 min, Vector3 max)
        {
            Vector3 normalized = Normalize(current, min, max);

            int retVal = Pack3Floats(normalized);
            return retVal;
        }

        public static Vector3 UnpackPosition(float packedPosition)
        {
            return UnpackVector3(packedPosition, minPosition, maxPosition);
        }

        public static Vector3 UnpackRotation(float packedRotation)
        {
            return UnpackVector3(packedRotation, minRotation, maxRotation);
        }

        /// <summary>
        /// Recreates a Vector3 from a float.
        /// Min and Max must be the same as in PackVector3.
        /// Since the x,y,z are still normalized (between 0 - 1) the min and max is necessary to recreate the correct position.
        /// </summary>
        /// <param name="f">Packed Vector3</param>
        /// <param name="min">Min range</param>
        /// <param name="max">Max range</param>
        /// <returns></returns>
        private static Vector3 UnpackVector3(float f, Vector3 min, Vector3 max)
        {
            (float x, float y, float z) = Unpack3Floats(f);

            x = min.x + (max.x - min.x) * x;
            y = min.y + (max.y - min.y) * y;
            z = min.z + (max.z - min.z) * z;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Normalize the vector3 based on min and max.
        /// </summary>
        /// <returns>Normalized Vector3, range between 0f and 1f</returns>
        private static Vector3 Normalize(Vector3 v3, Vector3 min, Vector3 max)
        {
            float x = (v3.x - min.x) / (max.x - min.x);
            float y = (v3.y - min.y) / (max.y - min.y);
            float z = (v3.z - min.z) / (max.z - min.z);
            return new Vector3(x, y, z);
        }

        private static int Pack2Floats(float num1, float num2)
        {
            int lhs = Mathf.RoundToInt((num1 + MaxAbsValue) * MaxPrecisionValue);
            int rhs = Mathf.RoundToInt((num2 + MaxAbsValue) * MaxPrecisionValue);
            return lhs << 16 | rhs;
        }

        private static Tuple<float, float> Unpack2Floats(int packedFloats)
        {
            int lhs = packedFloats >> 16 & ushort.MaxValue;
            int rhs = packedFloats & ushort.MaxValue;
            float x = lhs / MaxPrecisionValue - MaxAbsValue;
            float y = rhs / MaxPrecisionValue - MaxAbsValue;
            return new Tuple<float, float>(x, y);
        }

        /// <summary>
        /// Supports only a range from 0 to 1
        /// </summary>
        private static int Pack3Floats(Vector3 v3)
        {
            int retVal = ((int)(v3.x * 65535.0f + 0.5f));
            retVal |= ((int)(v3.y * 255.0f + 0.5f)) << 16;
            retVal |= ((int)(v3.z * 253.0f + 1.5f)) << 24;
            return retVal;
        }

        private static Tuple<float, float, float> Unpack3Floats(float f)
        {
            int i = (int)(f);
            float x = ((i) & 0xFFFF) / 65535.0f;
            float y = ((i >> 16) & 0xFF) / 255.0f;
            float z = (((i >> 24) & 0xFF) - 1.0f) / 253.0f;
            return new Tuple<float, float, float>(x, y, z);
        }
    }
}
