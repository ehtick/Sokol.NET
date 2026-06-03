// CGltfNode.cs — ported as-is from examples/CGltfViewer/Source/CGltfNode.cs (namespace only).
// Scene node that supports parent-dependent transforms with dirty flagging.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    /// <summary>
    /// Scene node that supports parent-dependent transforms with dirty flagging.
    /// </summary>
    public class CGltfNode
    {
        // Local transform components
        private Vector3 _position = Vector3.Zero;
        private Quaternion _rotation = Quaternion.Identity;
        private Vector3 _scale = Vector3.One;

        private Matrix4x4 _worldTransform = Matrix4x4.Identity;
        private bool _dirty = true;

        private CGltfNode? _parent = null;
        private List<CGltfNode> _children = new List<CGltfNode>();

        public int MeshIndex = -1;
        public string? NodeName = null;
        public bool HasAnimation = false;
        public bool IsSkinned = false;

        public int NodeIndex = -1;
        public IReadOnlyList<float>? NodeMorphWeights = null;
        public IReadOnlyList<float>? MeshMorphWeights = null;

        public Vector3 Position
        {
            get => _position;
            set { _position = value; MarkDirty(); }
        }

        public Quaternion Rotation
        {
            get => _rotation;
            set { _rotation = value; MarkDirty(); }
        }

        public Vector3 Scale
        {
            get => _scale;
            set { _scale = value; MarkDirty(); }
        }

        public CGltfNode? Parent
        {
            get => _parent;
            set
            {
                if (_parent != value)
                {
                    _parent?._children.Remove(this);
                    _parent = value;
                    _parent?._children.Add(this);
                    MarkDirty();
                }
            }
        }

        public IReadOnlyList<CGltfNode> Children => _children;

        public Matrix4x4 WorldTransform
        {
            get
            {
                if (_dirty) UpdateWorldTransform();
                return _worldTransform;
            }
        }

        public Matrix4x4 Transform
        {
            get => WorldTransform;
            set => SetWorldTransform(value);
        }

        public Matrix4x4 GetLocalTransform()
        {
            return Matrix4x4.CreateScale(_scale) *
                   Matrix4x4.CreateFromQuaternion(_rotation) *
                   Matrix4x4.CreateTranslation(_position);
        }

        public void SetLocalTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            _position = position;
            _rotation = rotation;
            _scale = scale;
            MarkDirty();
        }

        public void SetLocalTransform(Matrix4x4 localMatrix)
        {
            Matrix4x4.Decompose(localMatrix, out _scale, out _rotation, out _position);
            MarkDirty();
        }

        public void SetWorldTransform(Matrix4x4 worldTransform)
        {
            if (_parent != null)
            {
                Matrix4x4.Invert(_parent.WorldTransform, out var parentInverse);
                SetLocalTransform(worldTransform * parentInverse);
            }
            else
            {
                SetLocalTransform(worldTransform);
            }
        }

        public void AddChild(CGltfNode child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (child._parent != this) child.Parent = this;
        }

        public void RemoveChild(CGltfNode child)
        {
            if (child?._parent == this) child.Parent = null;
        }

        public void RemoveAllChildren()
        {
            while (_children.Count > 0)
                RemoveChild(_children[0]);
        }

        private void MarkDirty()
        {
            CGltfNode? cur = this;
            while (cur != null)
            {
                if (cur._dirty) return;
                cur._dirty = true;
                if (cur._children.Count > 0)
                {
                    var next = cur._children[0];
                    for (int i = 1; i < cur._children.Count; i++)
                        cur._children[i].MarkDirty();
                    cur = next;
                }
                else return;
            }
        }

        private void UpdateWorldTransform()
        {
            var local = GetLocalTransform();
            _worldTransform = _parent != null ? local * _parent.WorldTransform : local;
            _dirty = false;
        }

        public Vector3 GetWorldPosition() => WorldTransform.Translation;

        public Quaternion GetWorldRotation()
        {
            Matrix4x4.Decompose(WorldTransform, out _, out var r, out _);
            return r;
        }

        public Vector3 GetWorldScale()
        {
            Matrix4x4.Decompose(WorldTransform, out var s, out _, out _);
            return s;
        }

        public Vector3 LocalToWorld(Vector3 p) => Vector3.Transform(p, WorldTransform);

        public Vector3 WorldToLocal(Vector3 p)
        {
            Matrix4x4.Invert(WorldTransform, out var inv);
            return Vector3.Transform(p, inv);
        }
    }
}
