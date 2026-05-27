using System;
using System.Numerics;

namespace GameEditor.Framework.Renderer
{
    /// <summary>
    /// Pure geometry utilities for primitive mesh specs — no GPU/shader dependencies.
    /// These methods are separated from SceneRenderer so they can be compiled into the
    /// shared Framework.dll without pulling in platform-specific shader types.
    /// </summary>
    public static class PrimitiveMeshGeometry
    {
        /// <summary>
        /// Returns a point cloud in local mesh space suitable as input for a ConvexHull physics shape.
        /// Points embed the primitive's geometric parameters (radius, height, etc.) so the caller
        /// only needs to multiply by the entity's Scale to get world-sized hull input.
        /// </summary>
        public static Vector3[] GetHullPoints(in PrimitiveMeshSpec spec)
        {
            const int CirclePts = 16;
            var pts = new System.Collections.Generic.List<Vector3>(64);

            switch (spec.Kind)
            {
                case PrimitiveKind.Box:
                {
                    float hx = spec.Width * 0.5f, hy = spec.Height * 0.5f, hz = spec.Depth * 0.5f;
                    foreach (float sx in new[] { -hx, hx })
                    foreach (float sy in new[] { -hy, hy })
                    foreach (float sz in new[] { -hz, hz })
                        pts.Add(new Vector3(sx, sy, sz));
                    break;
                }
                case PrimitiveKind.Sphere:
                {
                    int sl = Math.Max(8, spec.Slices / 2), st = Math.Max(4, spec.Stacks / 2);
                    float R = spec.Radius;
                    for (int i = 0; i <= st; i++)
                    {
                        float v  = MathF.PI * i / st;
                        float sv = MathF.Sin(v), cv = MathF.Cos(v);
                        for (int j = 0; j < sl; j++)
                        {
                            float u  = MathF.Tau * j / sl;
                            pts.Add(new Vector3(R * sv * MathF.Cos(u), R * cv, R * sv * MathF.Sin(u)));
                        }
                    }
                    break;
                }
                case PrimitiveKind.Capsule:
                {
                    float r = spec.Radius, hh = MathF.Max(0f, spec.Height * 0.5f - r);
                    for (int j = 0; j < CirclePts; j++)
                    {
                        float a = MathF.Tau * j / CirclePts;
                        float ca = MathF.Cos(a), sa = MathF.Sin(a);
                        for (int i = 0; i <= 4; i++)
                        {
                            float t  = MathF.PI * 0.5f * i / 4;
                            float st = MathF.Sin(t), ct = MathF.Cos(t);
                            pts.Add(new Vector3(r * st * ca,  hh + r * ct, r * st * sa));
                            pts.Add(new Vector3(r * st * ca, -hh - r * ct, r * st * sa));
                        }
                    }
                    break;
                }
                case PrimitiveKind.Cylinder:
                {
                    float r = spec.Radius, hy = spec.Height * 0.5f;
                    for (int j = 0; j < CirclePts; j++)
                    {
                        float a = MathF.Tau * j / CirclePts;
                        pts.Add(new Vector3(r * MathF.Cos(a),  hy, r * MathF.Sin(a)));
                        pts.Add(new Vector3(r * MathF.Cos(a), -hy, r * MathF.Sin(a)));
                    }
                    break;
                }
                case PrimitiveKind.Ring:
                {
                    float R = spec.Radius, r = spec.RingRadius;
                    int nU = Math.Max(16, spec.Rings / 2), nV = Math.Max(8, spec.Sides / 2);
                    for (int i = 0; i < nU; i++)
                    {
                        float u = MathF.Tau * i / nU;
                        float cu = MathF.Cos(u), su = MathF.Sin(u);
                        for (int j = 0; j < nV; j++)
                        {
                            float v  = MathF.Tau * j / nV;
                            float d  = R + r * MathF.Cos(v);
                            pts.Add(new Vector3(d * cu, r * MathF.Sin(v), d * su));
                        }
                    }
                    break;
                }
                case PrimitiveKind.Cone:
                case PrimitiveKind.Pyramid:
                {
                    int sides = Math.Max(3, spec.Kind == PrimitiveKind.Cone ? spec.Slices : spec.Sides);
                    float r = spec.Radius, hy = spec.Height * 0.5f;
                    pts.Add(new Vector3(0f, hy, 0f)); // apex
                    for (int j = 0; j < sides; j++)
                    {
                        float a = MathF.Tau * j / sides;
                        pts.Add(new Vector3(r * MathF.Cos(a), -hy, r * MathF.Sin(a)));
                    }
                    break;
                }
                case PrimitiveKind.Plane:
                {
                    float hw = spec.Width * 0.5f, hd = spec.Depth * 0.5f;
                    pts.Add(new Vector3(-hw, 0f, -hd)); pts.Add(new Vector3( hw, 0f, -hd));
                    pts.Add(new Vector3( hw, 0f,  hd)); pts.Add(new Vector3(-hw, 0f,  hd));
                    break;
                }
                default:
                {
                    foreach (float sx in new[] { -0.5f, 0.5f })
                    foreach (float sy in new[] { -0.5f, 0.5f })
                    foreach (float sz in new[] { -0.5f, 0.5f })
                        pts.Add(new Vector3(sx, sy, sz));
                    break;
                }
            }

            return pts.ToArray();
        }

        /// <summary>
        /// Returns (vertices, indices) in local mesh space for a MeshShape collider.
        /// Indices are packed triples (CCW winding). Scale is NOT applied — multiply
        /// each vertex by the entity Scale before passing to the physics engine.
        /// </summary>
        public static (Vector3[] Vertices, uint[] Indices) GetMeshTriangles(in PrimitiveMeshSpec spec)
        {
            var verts = new System.Collections.Generic.List<Vector3>(128);
            var tris  = new System.Collections.Generic.List<uint>(384);

            void Quad(uint a, uint b, uint c, uint d)
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }

            switch (spec.Kind)
            {
                case PrimitiveKind.Box:
                {
                    float hx = spec.Width * 0.5f, hy = spec.Height * 0.5f, hz = spec.Depth * 0.5f;
                    verts.Add(new Vector3(-hx, -hy, -hz)); // 0
                    verts.Add(new Vector3( hx, -hy, -hz)); // 1
                    verts.Add(new Vector3( hx,  hy, -hz)); // 2
                    verts.Add(new Vector3(-hx,  hy, -hz)); // 3
                    verts.Add(new Vector3(-hx, -hy,  hz)); // 4
                    verts.Add(new Vector3( hx, -hy,  hz)); // 5
                    verts.Add(new Vector3( hx,  hy,  hz)); // 6
                    verts.Add(new Vector3(-hx,  hy,  hz)); // 7
                    Quad(0, 1, 2, 3); // front  (-Z)
                    Quad(5, 4, 7, 6); // back   (+Z)
                    Quad(4, 0, 3, 7); // left   (-X)
                    Quad(1, 5, 6, 2); // right  (+X)
                    Quad(3, 2, 6, 7); // top    (+Y)
                    Quad(4, 5, 1, 0); // bottom (-Y)
                    break;
                }
                case PrimitiveKind.Sphere:
                {
                    int sl = Math.Max(8, spec.Slices / 2), st = Math.Max(4, spec.Stacks / 2);
                    float R = spec.Radius;
                    for (int i = 0; i <= st; i++)
                    {
                        float v  = MathF.PI * i / st;
                        float sv = MathF.Sin(v), cv = MathF.Cos(v);
                        for (int j = 0; j <= sl; j++)
                        {
                            float u = MathF.Tau * j / sl;
                            verts.Add(new Vector3(R * sv * MathF.Cos(u), R * cv, R * sv * MathF.Sin(u)));
                        }
                    }
                    for (int i = 0; i < st; i++)
                    for (int j = 0; j < sl; j++)
                    {
                        uint a = (uint)(i * (sl + 1) + j);
                        uint b = a + 1;
                        uint c = (uint)((i + 1) * (sl + 1) + j);
                        uint d = c + 1;
                        Quad(a, b, d, c);
                    }
                    break;
                }
                case PrimitiveKind.Plane:
                {
                    float hw = spec.Width * 0.5f, hd = spec.Depth * 0.5f;
                    verts.Add(new Vector3(-hw, 0f, -hd)); // 0
                    verts.Add(new Vector3( hw, 0f, -hd)); // 1
                    verts.Add(new Vector3( hw, 0f,  hd)); // 2
                    verts.Add(new Vector3(-hw, 0f,  hd)); // 3
                    Quad(0, 1, 2, 3);
                    break;
                }
                case PrimitiveKind.Capsule:
                {
                    float r = spec.Radius, hh = MathF.Max(0f, spec.Height * 0.5f - r);
                    int sl = 12, hemi = 6;
                    for (int i = 0; i <= hemi; i++)
                    {
                        float v  = MathF.PI * 0.5f * i / hemi;
                        float sv = MathF.Sin(v), cv = MathF.Cos(v);
                        for (int j = 0; j <= sl; j++)
                        {
                            float u = MathF.Tau * j / sl;
                            verts.Add(new Vector3(r * sv * MathF.Cos(u),  hh + r * cv, r * sv * MathF.Sin(u)));
                        }
                    }
                    for (int i = 0; i <= hemi; i++)
                    {
                        float v  = MathF.PI * 0.5f + MathF.PI * 0.5f * i / hemi;
                        float sv = MathF.Sin(v), cv = MathF.Cos(v);
                        for (int j = 0; j <= sl; j++)
                        {
                            float u = MathF.Tau * j / sl;
                            verts.Add(new Vector3(r * sv * MathF.Cos(u), -hh + r * cv, r * sv * MathF.Sin(u)));
                        }
                    }
                    int rows = (hemi + 1) * 2;
                    for (int i = 0; i < rows - 1; i++)
                    for (int j = 0; j < sl; j++)
                    {
                        uint a = (uint)(i * (sl + 1) + j);
                        uint b = a + 1;
                        uint c = (uint)((i + 1) * (sl + 1) + j);
                        uint d = c + 1;
                        Quad(a, b, d, c);
                    }
                    break;
                }
                case PrimitiveKind.Cylinder:
                {
                    float r = spec.Radius, hy = spec.Height * 0.5f;
                    int sl = Math.Max(8, spec.Slices);
                    verts.Add(new Vector3(0f,  hy, 0f)); // 0: top center
                    verts.Add(new Vector3(0f, -hy, 0f)); // 1: bottom center
                    for (int j = 0; j <= sl; j++)
                    {
                        float u = MathF.Tau * j / sl;
                        verts.Add(new Vector3(r * MathF.Cos(u),  hy, r * MathF.Sin(u)));
                        verts.Add(new Vector3(r * MathF.Cos(u), -hy, r * MathF.Sin(u)));
                    }
                    for (int j = 0; j < sl; j++)
                    {
                        uint t0 = (uint)(2 + j * 2);
                        uint b0 = t0 + 1;
                        uint t1 = (uint)(2 + (j + 1) * 2);
                        uint b1 = t1 + 1;
                        Quad(t0, t1, b1, b0);
                        tris.Add(0);  tris.Add(t1); tris.Add(t0);
                        tris.Add(1);  tris.Add(b0); tris.Add(b1);
                    }
                    break;
                }
                case PrimitiveKind.Cone:
                case PrimitiveKind.Pyramid:
                {
                    int sides = Math.Max(3, spec.Kind == PrimitiveKind.Cone ? spec.Slices : spec.Sides);
                    float r = spec.Radius, hy = spec.Height * 0.5f;
                    verts.Add(new Vector3(0f,  hy, 0f)); // 0: apex
                    verts.Add(new Vector3(0f, -hy, 0f)); // 1: base center
                    for (int j = 0; j <= sides; j++)
                    {
                        float u = MathF.Tau * j / sides;
                        verts.Add(new Vector3(r * MathF.Cos(u), -hy, r * MathF.Sin(u)));
                    }
                    for (int j = 0; j < sides; j++)
                    {
                        uint a = (uint)(2 + j);
                        uint b = (uint)(2 + j + 1);
                        tris.Add(0); tris.Add(b); tris.Add(a);
                        tris.Add(1); tris.Add(a); tris.Add(b);
                    }
                    break;
                }
                case PrimitiveKind.Ring:
                {
                    float R = spec.Radius, r = spec.RingRadius;
                    int nU = Math.Max(16, spec.Rings / 2), nV = Math.Max(8, spec.Sides / 2);
                    for (int i = 0; i <= nU; i++)
                    {
                        float u  = MathF.Tau * i / nU;
                        float cu = MathF.Cos(u), su = MathF.Sin(u);
                        for (int j = 0; j <= nV; j++)
                        {
                            float v = MathF.Tau * j / nV;
                            float d = R + r * MathF.Cos(v);
                            verts.Add(new Vector3(d * cu, r * MathF.Sin(v), d * su));
                        }
                    }
                    for (int i = 0; i < nU; i++)
                    for (int j = 0; j < nV; j++)
                    {
                        uint a = (uint)(i * (nV + 1) + j);
                        uint b = a + 1;
                        uint c = (uint)((i + 1) * (nV + 1) + j);
                        uint d = c + 1;
                        Quad(a, b, d, c);
                    }
                    break;
                }
                default:
                {
                    verts.Add(new Vector3(-0.5f, -0.5f, -0.5f));
                    verts.Add(new Vector3( 0.5f, -0.5f, -0.5f));
                    verts.Add(new Vector3( 0.5f,  0.5f, -0.5f));
                    verts.Add(new Vector3(-0.5f,  0.5f, -0.5f));
                    verts.Add(new Vector3(-0.5f, -0.5f,  0.5f));
                    verts.Add(new Vector3( 0.5f, -0.5f,  0.5f));
                    verts.Add(new Vector3( 0.5f,  0.5f,  0.5f));
                    verts.Add(new Vector3(-0.5f,  0.5f,  0.5f));
                    Quad(0, 1, 2, 3); Quad(5, 4, 7, 6);
                    Quad(4, 0, 3, 7); Quad(1, 5, 6, 2);
                    Quad(3, 2, 6, 7); Quad(4, 5, 1, 0);
                    break;
                }
            }

            return (verts.ToArray(), tris.ToArray());
        }
    }
}
