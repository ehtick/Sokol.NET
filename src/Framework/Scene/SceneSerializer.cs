using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// Serializes/deserializes scenes to/from a simple JSON format.
    /// Uses manual StringBuilder for writing (AOT-safe) and JsonDocument for reading.
    /// </summary>
    public static class SceneSerializer
    {
        public static string Serialize(Scene scene)
        {
            var world = ECSWorld.Instance;
            var sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(Esc(scene.Name)).Append("\",\"entities\":[");

            bool firstEntity = true;
            foreach (Entity id in world.Entities)
            {
                if (!firstEntity) sb.Append(',');
                firstEntity = false;

                sb.Append("{\"id\":").Append(id.GetHashCode()).Append(",\"c\":{");
                bool fc = true;

                if (world.TryGetComponent<NameTag>(id, out var nt))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"NameTag\":{\"Name\":\"").Append(Esc(nt.Name)).Append("\"}");
                }

                if (world.TryGetComponent<ActiveFlag>(id, out var af))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"ActiveFlag\":{\"Active\":").Append(af.Active ? "true" : "false").Append('}');
                }

                if (world.TryGetComponent<Transform>(id, out var tr))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Transform\":{\"P\":").Append(V3(tr.Position))
                      .Append(",\"E\":").Append(V3(tr.EulerAngles))
                      .Append(",\"S\":").Append(V3(tr.Scale));
                    if (tr.Parent.HasValue) sb.Append(",\"Par\":").Append(tr.Parent.Value.GetHashCode());
                    sb.Append('}');
                }

                if (world.TryGetComponent<MeshRenderer>(id, out var mr))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"MeshRenderer\":{\"Path\":\"").Append(Esc(mr.MeshPath ?? ""))
                      .Append("\",\"Vis\":").Append(mr.Visible ? "true" : "false").Append('}');
                }

                if (world.TryGetComponent<CameraComponent>(id, out var cam))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Camera\":{\"Fov\":").Append(F(cam.Fov))
                      .Append(",\"Near\":").Append(F(cam.NearZ))
                      .Append(",\"Far\":").Append(F(cam.FarZ))
                      .Append(",\"Main\":").Append(cam.IsMain ? "true" : "false")
                      .Append(",\"Ortho\":").Append(cam.IsOrthographic ? "true" : "false")
                      .Append(",\"OrthoSize\":").Append(F(cam.OrthoSize)).Append('}');
                }

                if (world.TryGetComponent<LightComponent>(id, out var lt))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Light\":{\"Type\":\"").Append(lt.Type.ToString())
                      .Append("\",\"Color\":").Append(V3(lt.Color))
                      .Append(",\"Int\":").Append(F(lt.Intensity))
                      .Append(",\"Range\":").Append(F(lt.Range))
                      .Append(",\"Inner\":").Append(F(lt.InnerAngle))
                      .Append(",\"Outer\":").Append(F(lt.OuterAngle)).Append('}');
                }

                if (world.TryGetComponent<RigidbodyComponent>(id, out var rb))
                {
                    Comma(sb, ref fc);
                                        sb.Append("\"Rigidbody\":{\"MotionType\":\"").Append(rb.MotionType.ToString())
                                            .Append("\",\"Static\":").Append(rb.IsStatic ? "true" : "false")
                      .Append(",\"Mass\":").Append(F(rb.Mass))
                        .Append(",\"Gravity\":").Append(rb.UseGravity ? "true" : "false")
                                                .Append(",\"Friction\":").Append(F(rb.Friction))
                                                .Append(",\"Restitution\":").Append(F(rb.Restitution))
                                                .Append(",\"LinearDamping\":").Append(F(rb.LinearDamping))
                                                .Append(",\"AngularDamping\":").Append(F(rb.AngularDamping));
                    // Shapes array (geometry is not serialised — repopulated at play time from MeshRenderer)
                    sb.Append(",\"Shapes\":[");
                    var shapes = rb.Shapes;
                    if (shapes != null)
                    {
                        for (int si = 0; si < shapes.Count; si++)
                        {
                            if (si > 0) sb.Append(',');
                            var se = shapes[si];
                            sb.Append("{\"S\":\"").Append(se.Shape.ToString()).Append('"')
                              .Append(",\"HX\":").Append(F(se.HalfExtent.X))
                              .Append(",\"HY\":").Append(F(se.HalfExtent.Y))
                              .Append(",\"HZ\":").Append(F(se.HalfExtent.Z))
                              .Append(",\"R\":").Append(F(se.Radius))
                              .Append(",\"H\":").Append(F(se.HalfHeight))
                              .Append(",\"OX\":").Append(F(se.Offset.X))
                              .Append(",\"OY\":").Append(F(se.Offset.Y))
                              .Append(",\"OZ\":").Append(F(se.Offset.Z))
                              .Append('}');
                        }
                    }
                    sb.Append(']');
                    sb.Append(",\"Trigger\":").Append(rb.IsTrigger ? "true" : "false")
                        .Append(",\"Layer\":").Append(rb.Layer)
                        .Append(",\"LayerMask\":").Append(rb.LayerMask)
                        .Append('}');
                }

                if (world.TryGetComponent<ScriptComponent>(id, out var sc))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Script\":{\"Type\":\"").Append(Esc(sc.TypeName ?? "")).Append('"');
                    if (sc.Properties != null && sc.Properties.Count > 0)
                    {
                        sb.Append(",\"Props\":{");
                        bool fp = true;
                        foreach (var kvp in sc.Properties)
                        {
                            if (!fp) sb.Append(',');
                            fp = false;
                            sb.Append('"').Append(Esc(kvp.Key)).Append("\":\"").Append(Esc(kvp.Value)).Append('"');
                        }
                        sb.Append('}');
                    }
                    sb.Append('}');
                }

                if (world.TryGetComponent<ScriptCollectionComponent>(id, out var scc)
                    && scc.Scripts != null && scc.Scripts.Count > 0)
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Scripts\":[");
                    bool firstScript = true;
                    foreach (var sc2 in scc.Scripts)
                    {
                        if (!firstScript) sb.Append(',');
                        firstScript = false;
                        sb.Append("{\"Type\":\"").Append(Esc(sc2.TypeName ?? "")).Append('"');
                        if (sc2.Properties != null && sc2.Properties.Count > 0)
                        {
                            sb.Append(",\"Props\":{");
                            bool fp = true;
                            foreach (var kvp in sc2.Properties)
                            {
                                if (!fp) sb.Append(',');
                                fp = false;
                                sb.Append('"').Append(Esc(kvp.Key)).Append("\":\"").Append(Esc(kvp.Value)).Append('"');
                            }
                            sb.Append('}');
                        }
                        sb.Append('}');
                    }
                    sb.Append(']');
                }

                if (world.TryGetComponent<ConstraintComponent>(id, out var con))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Constraint\":{\"T\":\"").Append(con.Type.ToString()).Append('"')
                      .Append(",\"A\":").Append(!con.BodyA.IsNull ? con.BodyA.GetHashCode() : -1)
                      .Append(",\"B\":").Append(!con.BodyB.IsNull ? con.BodyB.GetHashCode() : -1)
                      .Append(",\"AA\":").Append(V3(con.LocalAnchorA))
                      .Append(",\"AB\":").Append(V3(con.LocalAnchorB))
                      .Append(",\"XA\":").Append(V3(con.LocalAxisA))
                      .Append(",\"XB\":").Append(V3(con.LocalAxisB))
                      .Append(",\"Min\":").Append(F(con.MinLimit))
                      .Append(",\"Max\":").Append(F(con.MaxLimit))
                      .Append('}');
                }

                if (world.TryGetComponent<CharacterComponent>(id, out var chComp))
                {
                    Comma(sb, ref fc);
                    sb.Append("\"Character\":{\"Mode\":").Append((int)chComp.Mode)
                      .Append(",\"Height\":").Append(F(chComp.Height))
                      .Append(",\"Radius\":").Append(F(chComp.Radius))
                      .Append(",\"Slope\":").Append(F(chComp.MaxSlopeAngle))
                      .Append(",\"Strength\":").Append(F(chComp.MaxStrength))
                      .Append(",\"Mass\":").Append(F(chComp.Mass))
                      .Append(",\"Friction\":").Append(F(chComp.Friction))
                      .Append(",\"Gravity\":").Append(F(chComp.GravityFactor))
                      .Append(",\"CtTol\":").Append(F(chComp.CollisionTolerance))
                      .Append(",\"OfsX\":").Append(F(chComp.ShapeOffset.X))
                      .Append(",\"OfsY\":").Append(F(chComp.ShapeOffset.Y))
                      .Append(",\"OfsZ\":").Append(F(chComp.ShapeOffset.Z))
                      .Append(",\"Layer\":").Append(chComp.Layer)
                      .Append(",\"Mask\":").Append(chComp.LayerMask)
                      .Append('}');
                }

                sb.Append("}}"); // close components + entity
            }

            sb.Append("]}"); // close entities + root
            return sb.ToString();
        }

        public static void Deserialize(string json, Scene scene)
        {
            scene.Clear();

            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch { return; }

            var root = doc.RootElement;
            if (root.TryGetProperty("name", out var nameProp))
                scene.Name = nameProp.GetString() ?? scene.Name;

            if (!root.TryGetProperty("entities", out var entitiesEl))
                return;

            var world = ECSWorld.Instance;
            // Map from saved-ID (int) → new runtime Entity (for parent fixup)
            var idMap = new Dictionary<int, Entity>();
            // Entities that have a saved parent int needing fixup
            var parentFixups = new Dictionary<Entity, int>();
            // Entities with a ConstraintComponent whose BodyA/BodyB IDs need remapping
            var constraintFixups = new Dictionary<Entity, (int, int)>();

            // First pass — create entities and load all components
            foreach (var eEl in entitiesEl.EnumerateArray())
            {
                int savedId = eEl.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                Entity newId = world.CreateEntity(); // auto-adds NameTag/ActiveFlag/Transform
                idMap[savedId] = newId;

                if (!eEl.TryGetProperty("c", out var c)) continue;

                if (c.TryGetProperty("NameTag", out var nEl))
                    world.AddComponent(newId, new NameTag { Name = nEl.GetProperty("Name").GetString() ?? "Entity" });

                if (c.TryGetProperty("ActiveFlag", out var aEl))
                    world.AddComponent(newId, new ActiveFlag { Active = aEl.GetProperty("Active").GetBoolean() });

                if (c.TryGetProperty("Transform", out var tEl))
                {
                    if (tEl.TryGetProperty("Par", out var pEl))
                        parentFixups[newId] = pEl.GetInt32();
                    world.AddComponent(newId, new Transform
                    {
                        Position    = RV3(tEl, "P"),
                        EulerAngles = RV3(tEl, "E"),
                        Scale       = RV3(tEl, "S"),
                        Parent      = null  // fixed up in second pass
                    });
                }

                if (c.TryGetProperty("MeshRenderer", out var mrEl))
                    world.AddComponent(newId, new MeshRenderer
                    {
                        MeshPath = mrEl.GetProperty("Path").GetString() ?? "",
                        Visible  = mrEl.GetProperty("Vis").GetBoolean()
                    });

                if (c.TryGetProperty("Camera", out var camEl))
                    world.AddComponent(newId, new CameraComponent
                    {
                        Fov            = camEl.GetProperty("Fov").GetSingle(),
                        NearZ          = camEl.GetProperty("Near").GetSingle(),
                        FarZ           = camEl.GetProperty("Far").GetSingle(),
                        IsMain         = camEl.GetProperty("Main").GetBoolean(),
                        IsOrthographic = camEl.TryGetProperty("Ortho", out var orthoEl) && orthoEl.GetBoolean(),
                        OrthoSize      = camEl.TryGetProperty("OrthoSize", out var osEl) ? osEl.GetSingle() : 5f
                    });

                if (c.TryGetProperty("Light", out var ltEl))
                    world.AddComponent(newId, new LightComponent
                    {
                        Type       = Enum.Parse<LightType>(ltEl.GetProperty("Type").GetString()!),
                        Color      = RV3(ltEl, "Color"),
                        Intensity  = ltEl.GetProperty("Int").GetSingle(),
                        Range      = ltEl.GetProperty("Range").GetSingle(),
                        InnerAngle = ltEl.TryGetProperty("Inner", out var innerEl) ? innerEl.GetSingle() : 25f,
                        OuterAngle = ltEl.TryGetProperty("Outer", out var outerEl) ? outerEl.GetSingle() : 35f
                    });

                if (c.TryGetProperty("Rigidbody", out var rbEl))
                {
                    List<ShapeEntry> loadedShapes;
                    if (rbEl.TryGetProperty("Shapes", out var shapesEl) && shapesEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        loadedShapes = new List<ShapeEntry>();
                        foreach (var seEl in shapesEl.EnumerateArray())
                        {
                            var se = new ShapeEntry
                            {
                                Shape      = seEl.TryGetProperty("S", out var sEl2) ? Enum.Parse<GameEditor.Framework.Physics.ColliderShape>(sEl2.GetString() ?? "Box") : GameEditor.Framework.Physics.ColliderShape.Box,
                                HalfExtent = new System.Numerics.Vector3(
                                    seEl.TryGetProperty("HX", out var hxEl) ? hxEl.GetSingle() : 0.5f,
                                    seEl.TryGetProperty("HY", out var hyEl) ? hyEl.GetSingle() : 0.5f,
                                    seEl.TryGetProperty("HZ", out var hzEl) ? hzEl.GetSingle() : 0.5f),
                                Radius     = seEl.TryGetProperty("R", out var rEl) ? rEl.GetSingle() : 0.5f,
                                HalfHeight = seEl.TryGetProperty("H", out var hEl) ? hEl.GetSingle() : 0.5f,
                                Offset     = new System.Numerics.Vector3(
                                    seEl.TryGetProperty("OX", out var oxEl) ? oxEl.GetSingle() : 0f,
                                    seEl.TryGetProperty("OY", out var oyEl) ? oyEl.GetSingle() : 0f,
                                    seEl.TryGetProperty("OZ", out var ozEl) ? ozEl.GetSingle() : 0f),
                                OffsetRotation = System.Numerics.Quaternion.Identity,
                            };
                            loadedShapes.Add(se);
                        }
                        if (loadedShapes.Count == 0)
                            loadedShapes.Add(ShapeEntry.Default());
                    }
                    else
                    {
                        // Backward compat: old format had a single "Shape" string field
                        var legacyShape = rbEl.TryGetProperty("Shape", out var shapeEl)
                            ? Enum.Parse<GameEditor.Framework.Physics.ColliderShape>(shapeEl.GetString() ?? "Box")
                            : GameEditor.Framework.Physics.ColliderShape.Box;
                        loadedShapes = new List<ShapeEntry> { ShapeEntry.Default(legacyShape) };
                    }
                    world.AddComponent(newId, new RigidbodyComponent
                    {
                        MotionType = ReadMotionType(rbEl),
                        Mass       = rbEl.GetProperty("Mass").GetSingle(),
                        UseGravity = rbEl.GetProperty("Gravity").GetBoolean(),
                        Friction   = rbEl.TryGetProperty("Friction", out var frictionEl) ? frictionEl.GetSingle() : 0.5f,
                        Restitution = rbEl.TryGetProperty("Restitution", out var restitutionEl) ? restitutionEl.GetSingle() : 0f,
                        LinearDamping = rbEl.TryGetProperty("LinearDamping", out var linearDampingEl) ? linearDampingEl.GetSingle() : 0f,
                        AngularDamping = rbEl.TryGetProperty("AngularDamping", out var angularDampingEl) ? angularDampingEl.GetSingle() : 0.05f,
                        Shapes     = loadedShapes,
                        IsTrigger  = rbEl.TryGetProperty("Trigger", out var triggerEl) && triggerEl.GetBoolean(),
                        Layer      = rbEl.TryGetProperty("Layer", out var layerEl) ? (ushort)layerEl.GetInt32() : (ushort)1,
                        LayerMask  = rbEl.TryGetProperty("LayerMask", out var maskEl) ? (ushort)maskEl.GetInt32() : (ushort)0xFFFF
                    });
                }

                if (c.TryGetProperty("Script", out var sEl))
                {
                    var sc = new ScriptComponent
                    {
                        TypeName = sEl.GetProperty("Type").GetString() ?? ""
                    };
                    if (sEl.TryGetProperty("Props", out var propsEl))
                    {
                        sc.Properties = new Dictionary<string, string>();
                        foreach (var p in propsEl.EnumerateObject())
                            sc.Properties[p.Name] = p.Value.GetString() ?? "";
                    }
                    world.AddComponent(newId, sc);
                }

                if (c.TryGetProperty("Scripts", out var saEl) && saEl.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<ScriptComponent>();
                    foreach (var sxEl in saEl.EnumerateArray())
                    {
                        var sc = new ScriptComponent
                        {
                            TypeName = sxEl.TryGetProperty("Type", out var txEl) ? (txEl.GetString() ?? "") : ""
                        };
                        if (sxEl.TryGetProperty("Props", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
                        {
                            sc.Properties = new Dictionary<string, string>();
                            foreach (var p in propsEl.EnumerateObject())
                                sc.Properties[p.Name] = p.Value.GetString() ?? "";
                        }
                        list.Add(sc);
                    }
                    if (list.Count > 0)
                        world.AddComponent(newId, new ScriptCollectionComponent { Scripts = list });
                }

                if (c.TryGetProperty("Constraint", out var conEl))
                {
                    int savedA = conEl.TryGetProperty("A", out var conAEl) ? conAEl.GetInt32() : -1;
                    int savedB = conEl.TryGetProperty("B", out var conBEl) ? conBEl.GetInt32() : -1;
                    world.AddComponent(newId, new ConstraintComponent
                    {
                        Type         = conEl.TryGetProperty("T", out var tEl2) &&
                                       Enum.TryParse(tEl2.GetString(), out GameEditor.Framework.Physics.ConstraintType ct)
                                       ? ct : GameEditor.Framework.Physics.ConstraintType.Fixed,
                        LocalAnchorA = RV3(conEl, "AA"),
                        LocalAnchorB = RV3(conEl, "AB"),
                        LocalAxisA   = RV3(conEl, "XA"),
                        LocalAxisB   = RV3(conEl, "XB"),
                        MinLimit     = conEl.TryGetProperty("Min", out var minEl) ? minEl.GetSingle() : 0f,
                        MaxLimit     = conEl.TryGetProperty("Max", out var maxEl) ? maxEl.GetSingle() : 0f,
                    });
                    if (savedA >= 0 || savedB >= 0)
                        constraintFixups[newId] = (savedA, savedB);
                }

                if (c.TryGetProperty("Character", out var charEl))
                {
                    world.AddComponent(newId, new CharacterComponent
                    {
                        Mode               = charEl.TryGetProperty("Mode",     out var modeEl) ? (CharacterMode)modeEl.GetInt32() : CharacterComponent.Default.Mode,
                        Height             = charEl.TryGetProperty("Height",   out var hEl)    ? hEl.GetSingle()                  : CharacterComponent.Default.Height,
                        Radius             = charEl.TryGetProperty("Radius",   out var rEl)    ? rEl.GetSingle()                  : CharacterComponent.Default.Radius,
                        MaxSlopeAngle      = charEl.TryGetProperty("Slope",    out var slEl2)  ? slEl2.GetSingle()                : CharacterComponent.Default.MaxSlopeAngle,
                        MaxStrength        = charEl.TryGetProperty("Strength", out var stEl)   ? stEl.GetSingle()                 : CharacterComponent.Default.MaxStrength,
                        Mass               = charEl.TryGetProperty("Mass",     out var mEl)    ? mEl.GetSingle()                  : CharacterComponent.Default.Mass,
                        Friction           = charEl.TryGetProperty("Friction", out var frEl)   ? frEl.GetSingle()                 : CharacterComponent.Default.Friction,
                        GravityFactor      = charEl.TryGetProperty("Gravity",  out var gfEl)   ? gfEl.GetSingle()                 : CharacterComponent.Default.GravityFactor,
                        CollisionTolerance = charEl.TryGetProperty("CtTol",    out var ctEl)   ? ctEl.GetSingle()                 : CharacterComponent.Default.CollisionTolerance,
                        ShapeOffset        = new System.Numerics.Vector3(
                            charEl.TryGetProperty("OfsX", out var oxEl) ? oxEl.GetSingle() : 0f,
                            charEl.TryGetProperty("OfsY", out var oyEl) ? oyEl.GetSingle() : 0f,
                            charEl.TryGetProperty("OfsZ", out var ozEl) ? ozEl.GetSingle() : 0f),
                        Layer              = charEl.TryGetProperty("Layer",    out var lEl)    ? (ushort)lEl.GetInt32()           : CharacterComponent.Default.Layer,
                        LayerMask          = charEl.TryGetProperty("Mask",     out var mkEl)   ? (ushort)mkEl.GetInt32()          : CharacterComponent.Default.LayerMask,
                    });
                }
            }

            // Second pass — remap parent IDs from saved ints to live Entities
            foreach (var (entity, savedParentId) in parentFixups)
            {
                if (!idMap.TryGetValue(savedParentId, out Entity parentEntity)) continue;
                if (world.TryGetComponent<Transform>(entity, out var tr))
                {
                    tr.Parent = parentEntity;
                    world.AddComponent(entity, tr);
                }
            }

            // Third pass — remap constraint BodyA/BodyB saved IDs to live Entities
            foreach (var (entity, savedIds) in constraintFixups)
            {
                if (!world.TryGetComponent<ConstraintComponent>(entity, out var cc)) continue;
                if (savedIds.Item1 >= 0 && idMap.TryGetValue(savedIds.Item1, out Entity ea))
                    cc.BodyA = ea;
                if (savedIds.Item2 >= 0 && idMap.TryGetValue(savedIds.Item2, out Entity eb))
                    cc.BodyB = eb;
                world.AddComponent(entity, cc);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static void Comma(StringBuilder sb, ref bool first)
        {
            if (!first) sb.Append(',');
            first = false;
        }

        private static string F(float v) => v.ToString("G", CultureInfo.InvariantCulture);

        private static string V3(Vector3 v) => $"[{F(v.X)},{F(v.Y)},{F(v.Z)}]";

        private static Vector3 RV3(JsonElement parent, string prop)
        {
            if (!parent.TryGetProperty(prop, out var el)) return Vector3.Zero;
            float x = 0, y = 0, z = 0;
            int i = 0;
            foreach (var n in el.EnumerateArray())
            {
                if      (i == 0) x = n.GetSingle();
                else if (i == 1) y = n.GetSingle();
                else if (i == 2) z = n.GetSingle();
                i++;
            }
            return new Vector3(x, y, z);
        }

        private static string Esc(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        private static GameEditor.Framework.Physics.RigidbodyMotionType ReadMotionType(JsonElement rbEl)
        {
            if (rbEl.TryGetProperty("MotionType", out var mtEl))
            {
                string? mt = mtEl.GetString();
                if (!string.IsNullOrEmpty(mt) && Enum.TryParse(mt, true, out GameEditor.Framework.Physics.RigidbodyMotionType parsed))
                    return parsed;
            }

            if (rbEl.TryGetProperty("Static", out var staticEl) && staticEl.ValueKind == JsonValueKind.True)
                return GameEditor.Framework.Physics.RigidbodyMotionType.Static;

            return GameEditor.Framework.Physics.RigidbodyMotionType.Dynamic;
        }
    }
}
