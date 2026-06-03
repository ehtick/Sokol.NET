// CGltfAnimation.cs — ported as-is from examples/CGltfViewer/Source/CGltfAnimation.cs (namespace only).
// Animation data structures for cgltf-based loading.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    // -----------------------------------------------------------------------
    // Bone hierarchy snapshot
    // -----------------------------------------------------------------------

    public struct CGltfNodeData
    {
        public Matrix4x4 Transformation;
        public string Name;
        public int ChildrenCount;
        public List<CGltfNodeData> Children;
    }

    // -----------------------------------------------------------------------
    // Material property animation (KHR_animation_pointer — not supported by
    // cgltf, kept as empty stubs so the rest of the code compiles unchanged)
    // -----------------------------------------------------------------------

    public enum MaterialAnimationTarget
    {
        NormalTextureRotation,
        NormalTextureOffset,
        NormalTextureScale,
        ThicknessTextureRotation,
        ThicknessTextureOffset,
        ThicknessTextureScale,
    }

    public class MaterialPropertyAnimation
    {
        public int MaterialIndex { get; set; }
        public MaterialAnimationTarget Target { get; set; }
        public string PropertyPath { get; set; } = "";

        public List<(float time, float value)> FloatKeyframes { get; set; } = new();
        public List<(float time, Vector2 value)> Vector2Keyframes { get; set; } = new();

        public bool IsFloatType =>
            Target == MaterialAnimationTarget.NormalTextureRotation ||
            Target == MaterialAnimationTarget.ThicknessTextureRotation;

        public float SampleFloatAtTime(float time)
        {
            if (FloatKeyframes.Count == 0) return 0f;
            if (FloatKeyframes.Count == 1) return FloatKeyframes[0].value;
            for (int i = 0; i < FloatKeyframes.Count - 1; i++)
            {
                if (time >= FloatKeyframes[i].time && time <= FloatKeyframes[i + 1].time)
                {
                    float t = (time - FloatKeyframes[i].time) / (FloatKeyframes[i + 1].time - FloatKeyframes[i].time);
                    return FloatKeyframes[i].value + t * (FloatKeyframes[i + 1].value - FloatKeyframes[i].value);
                }
            }
            return time < FloatKeyframes[0].time ? FloatKeyframes[0].value : FloatKeyframes[^1].value;
        }

        public Vector2 SampleVector2AtTime(float time)
        {
            if (Vector2Keyframes.Count == 0) return Vector2.Zero;
            if (Vector2Keyframes.Count == 1) return Vector2Keyframes[0].value;
            for (int i = 0; i < Vector2Keyframes.Count - 1; i++)
            {
                if (time >= Vector2Keyframes[i].time && time <= Vector2Keyframes[i + 1].time)
                {
                    float t = (time - Vector2Keyframes[i].time) / (Vector2Keyframes[i + 1].time - Vector2Keyframes[i].time);
                    return Vector2.Lerp(Vector2Keyframes[i].value, Vector2Keyframes[i + 1].value, t);
                }
            }
            return time < Vector2Keyframes[0].time ? Vector2Keyframes[0].value : Vector2Keyframes[^1].value;
        }
    }

    // -----------------------------------------------------------------------
    // Morph weight animation
    // -----------------------------------------------------------------------

    public class MorphWeightAnimation
    {
        public int NodeIndex { get; set; }
        public string NodeName { get; set; } = "";
        public List<(float time, float[] weights)> Keyframes { get; set; } = new();

        public float[] SampleWeightsAtTime(float time)
        {
            if (Keyframes.Count == 0) return Array.Empty<float>();
            if (Keyframes.Count == 1) return Keyframes[0].weights;

            for (int i = 0; i < Keyframes.Count - 1; i++)
            {
                if (time >= Keyframes[i].time && time <= Keyframes[i + 1].time)
                {
                    float t = (time - Keyframes[i].time) / (Keyframes[i + 1].time - Keyframes[i].time);
                    var w0 = Keyframes[i].weights;
                    var w1 = Keyframes[i + 1].weights;
                    var result = new float[w0.Length];
                    for (int w = 0; w < w0.Length; w++)
                        result[w] = w0[w] + t * (w1[w] - w0[w]);
                    return result;
                }
            }
            return time < Keyframes[0].time ? Keyframes[0].weights : Keyframes[^1].weights;
        }
    }

    // -----------------------------------------------------------------------
    // Animation clip
    // -----------------------------------------------------------------------

    public class CGltfAnimation
    {
        public string Name { get; set; } = "";

        private float _duration;
        private float _ticksPerSecond;
        private List<CGltfBone> _bones = new();
        private CGltfNodeData _rootNode;
        private Dictionary<string, BoneInfo> _boneInfoMap;

        public List<MaterialPropertyAnimation> MaterialAnimations { get; set; } = new();
        public List<MorphWeightAnimation> MorphAnimations { get; set; } = new();

        public CGltfAnimation(float duration, float ticksPerSecond, CGltfNodeData rootNode,
                              Dictionary<string, BoneInfo> boneInfoMap)
        {
            _duration = duration;
            _ticksPerSecond = ticksPerSecond;
            _rootNode = rootNode;
            _boneInfoMap = boneInfoMap;
        }

        public void AddBone(CGltfBone bone) => _bones.Add(bone);

        public CGltfBone? FindBone(string name) => _bones.Find(b => b.Name == name);

        public float GetTicksPerSecond() => _ticksPerSecond;
        public float GetDuration() => _duration;
        public ref CGltfNodeData GetRootNode() => ref _rootNode;
        public Dictionary<string, BoneInfo> GetBoneIDMap() => _boneInfoMap;
        public List<CGltfBone> GetBones() => _bones;

        public CGltfAnimation CreateSubsetForCharacter(Dictionary<string, BoneInfo> characterBoneInfoMap, string characterName)
        {
            var subset = new CGltfAnimation(_duration, _ticksPerSecond, _rootNode, characterBoneInfoMap);
            subset.Name = $"{Name}_{characterName}";
            foreach (var bone in _bones)
            {
                if (characterBoneInfoMap.ContainsKey(bone.Name))
                    subset.AddBone(bone.Clone(characterBoneInfoMap[bone.Name].Id));
            }
            return subset;
        }
    }
}
