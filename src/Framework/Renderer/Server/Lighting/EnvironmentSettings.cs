// EnvironmentSettings.cs — project-global IBL/skybox configuration authored in the editor.
//
// Drives RenderingServer.ApplyEnvironment. Persisted in the project config so the chosen
// sky survives restarts. Kept deliberately small (no full Godot sun/fog/GI setup).

using System;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    public enum EnvironmentMode
    {
        Procedural = 0,  // analytic sky/sun baker (cross-platform, no assets)
        Cubemap    = 1,  // 6 pre-baked face images from the project's Assets
    }

    public sealed class EnvironmentSettings
    {
        /// <summary>Number of cubemap faces (+X,-X,+Y,-Y,+Z,-Z).</summary>
        public const int FaceCount = 6;

        public EnvironmentMode Mode = EnvironmentMode.Cubemap;

        /// <summary>Assets-relative folder holding skybox_{px,nx,py,ny,pz,nz}.jpg.</summary>
        public string CubemapFolder = "skyboxes/skybox";

        /// <summary>
        /// Optional per-face overrides (Assets-relative paths). An empty entry falls back to
        /// the folder's default skybox_*.jpg name. Order: +X,-X,+Y,-Y,+Z,-Z.
        /// </summary>
        public readonly string[] FaceOverrides = new string[FaceCount];

        public float Intensity      = 1f;
        public float RotationDegrees = 0f;
        public bool  ShowSkybox     = true;

        /// <summary>
        /// Populates <paramref name="s"/> from the project's persisted environment fields. Shared by
        /// the editor's Environment panel and standalone/game builds (which have no panel), so the
        /// config→settings mapping lives in exactly one place.
        /// </summary>
        public static void LoadInto(EnvironmentSettings s, ProjectConfig c)
        {
            s.Mode            = string.Equals(c.EnvironmentMode, "procedural", StringComparison.OrdinalIgnoreCase)
                                ? EnvironmentMode.Procedural : EnvironmentMode.Cubemap;
            s.CubemapFolder   = string.IsNullOrWhiteSpace(c.EnvironmentFolder) ? "skyboxes/skybox" : c.EnvironmentFolder;
            s.Intensity       = c.EnvironmentIntensity;
            s.RotationDegrees = c.EnvironmentRotation;
            s.ShowSkybox      = c.EnvironmentShowSkybox;
            string[] parts = (c.EnvironmentFaces ?? "").Split('|');
            for (int i = 0; i < FaceCount; i++)
                s.FaceOverrides[i] = i < parts.Length ? parts[i] : "";
        }

        public EnvironmentSettings Clone()
        {
            var c = new EnvironmentSettings
            {
                Mode            = Mode,
                CubemapFolder   = CubemapFolder,
                Intensity       = Intensity,
                RotationDegrees = RotationDegrees,
                ShowSkybox      = ShowSkybox,
            };
            Array.Copy(FaceOverrides, c.FaceOverrides, FaceCount);
            return c;
        }

        /// <summary>True when the env must be rebuilt (vs. only intensity/rotation/show tweaked).</summary>
        public bool RequiresRebuild(EnvironmentSettings other)
        {
            if (Mode != other.Mode) return true;
            if (Mode == EnvironmentMode.Cubemap)
            {
                if (!string.Equals(CubemapFolder, other.CubemapFolder, StringComparison.Ordinal)) return true;
                for (int i = 0; i < FaceCount; i++)
                    if (!string.Equals(FaceOverrides[i] ?? "", other.FaceOverrides[i] ?? "", StringComparison.Ordinal))
                        return true;
            }
            return false;
        }
    }
}
