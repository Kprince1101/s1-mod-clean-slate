using MelonLoader;
using UnityEngine;

namespace LegionCore.Buildings
{
    // One-time diagnostic: lists every shader actually compiled into this build, filtered to
    // likely opaque/solid-color candidates. Added after "Sprites/Default" (used for the
    // storefront shell's primitives - see PrimitiveBuilder) turned out to render as a
    // translucent "ghost" in-game - it's a transparent-blend shader meant for sprites with
    // alpha, not solid architecture, so overlapping wall/window/roof boxes blend back-to-front
    // instead of properly occluding each other. Picking a real opaque replacement needs the
    // actual compiled shader set for this build, not another guess - GetShaderCount/
    // GetShaderInfo enumerate exactly what survived this build's shader stripping, at runtime.
    public static class ShaderDiagnostics
    {
        public static void LogOpaqueCandidates()
        {
            int count = Shader.GetShaderCount();
            MelonLogger.Msg($"LegionCore-Buildings: {count} shaders compiled into this build. Opaque/solid-color candidates:");

            string[] keywords = { "unlit", "diffuse", "standard", "simple", "color", "opaque", "lit", "universal render pipeline", "hdrp", "particles" };
            int matched = 0;

            for (int i = 0; i < count; i++)
            {
                var info = Shader.GetShaderInfo(i);
                var lower = info.name.ToLowerInvariant();

                bool isCandidate = false;
                foreach (var kw in keywords)
                {
                    if (lower.Contains(kw)) { isCandidate = true; break; }
                }
                if (!isCandidate) continue;

                matched++;
                MelonLogger.Msg($"  [{i}] '{info.name}' supported={info.supported} hasErrors={info.hasErrors}");
            }

            MelonLogger.Msg($"LegionCore-Buildings: {matched} candidate shaders logged (of {count} total).");
        }
    }
}
