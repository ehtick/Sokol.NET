using System;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    /// <summary>
    /// A node-TRS animation clip for a single glTF node, sampled onto an ECS entity's
    /// <c>Transform</c>. This is rigid/node animation (e.g. the ChronographWatch hands) — distinct
    /// from skinned animation (<see cref="CGltfAnimator"/> / <c>SkinnedCharacterRegistry</c>), which
    /// drives joint matrices. Keyframes are LINEAR or STEP (CUBICSPLINE tangents are collapsed to the
    /// keyframe value). Channels with no keys fall back to the node's bind value (<c>Base*</c>).
    /// </summary>
    public sealed class NodeAnimationClip
    {
        public float Duration;

        public float[]      TransTimes = Array.Empty<float>();
        public Vector3[]    TransVals  = Array.Empty<Vector3>();
        public bool         TransStep;
        public float[]      RotTimes   = Array.Empty<float>();
        public Quaternion[] RotVals    = Array.Empty<Quaternion>();
        public bool         RotStep;
        public float[]      ScaleTimes = Array.Empty<float>();
        public Vector3[]    ScaleVals  = Array.Empty<Vector3>();
        public bool         ScaleStep;

        // Node bind TRS — used for any channel this clip does not animate.
        public Vector3    BaseTranslation = Vector3.Zero;
        public Quaternion BaseRotation    = Quaternion.Identity;
        public Vector3    BaseScale       = Vector3.One;

        public bool HasAnyChannel => TransVals.Length > 0 || RotVals.Length > 0 || ScaleVals.Length > 0;

        /// <summary>Samples T/R/S at <paramref name="time"/> (seconds, already wrapped/clamped by the caller).</summary>
        public void Sample(float time, out Vector3 t, out Quaternion r, out Vector3 s)
        {
            t = TransVals.Length > 0 ? SampleVec3(TransTimes, TransVals, time, TransStep) : BaseTranslation;
            r = RotVals.Length   > 0 ? SampleQuat(RotTimes,   RotVals,   time, RotStep)   : BaseRotation;
            s = ScaleVals.Length > 0 ? SampleVec3(ScaleTimes, ScaleVals, time, ScaleStep) : BaseScale;
        }

        private static int FindSegment(float[] times, float time, out float u)
        {
            u = 0f;
            int n = times.Length;
            if (n == 1 || time <= times[0]) return 0;
            if (time >= times[n - 1])        return n - 1;
            for (int i = 0; i < n - 1; i++)
                if (time < times[i + 1])
                {
                    u = (time - times[i]) / (times[i + 1] - times[i]);
                    return i;
                }
            return n - 1;
        }

        private static Vector3 SampleVec3(float[] times, Vector3[] vals, float time, bool step)
        {
            int i = FindSegment(times, time, out float u);
            if (step || i >= vals.Length - 1) return vals[Math.Min(i, vals.Length - 1)];
            return Vector3.Lerp(vals[i], vals[i + 1], u);
        }

        private static Quaternion SampleQuat(float[] times, Quaternion[] vals, float time, bool step)
        {
            int i = FindSegment(times, time, out float u);
            if (step || i >= vals.Length - 1) return vals[Math.Min(i, vals.Length - 1)];
            return Quaternion.Slerp(vals[i], vals[i + 1], u);
        }
    }
}
