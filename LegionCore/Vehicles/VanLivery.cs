using Il2CppScheduleOne.Graffiti;
using Il2CppScheduleOne.Vehicles;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Vehicles
{
    // Slaps a logo decal on both sides of a spawned van. Every van in-game already ships with
    // ScheduleOne.Graffiti.SpraySurface components (that's the player spray-paint "canvas" -
    // confirmed from a screenshot of real in-game van graffiti, and from LandVehicle.cs, which
    // keeps a private _spraySurfaces array populated via GetComponentsInChildren<SpraySurface>()
    // for save/load). Each SpraySurface exposes exactly the artist-placed real-world geometry
    // for its panel (BottomLeftPoint transform + Width/Height in "paint pixels", PIXEL_SIZE
    // converts pixels -> world units, CenterPoint is already computed) - so instead of guessing
    // where the side panels are, we read that data directly and place our own textured quad to
    // match it. We do NOT use the surface's actual stroke/drawing/networking system (that's a
    // NetworkBehaviour-driven, server-authoritative pixel painter meant for the player's spray
    // can tool - far more machinery than a static logo decal needs); this only borrows its
    // *placement* data. Falls back to a measured-bounds guess if a van has no SpraySurfaces at
    // all (shouldn't happen for a real vehicle, but cheap insurance against a modded/odd model).
    public static class VanLivery
    {
        private static Shader? _spriteShader;

        public static void Apply(LandVehicle? van, Sprite? logo)
        {
            if (van == null || logo == null)
            {
                MelonLogger.Warning($"GRQD-Livery: Apply skipped - van={(van != null)} logo={(logo != null)}");
                return;
            }

            var surfaces = van.GetComponentsInChildren<SpraySurface>();
            MelonLogger.Msg($"GRQD-Livery: found {surfaces.Length} SpraySurface(s) on van.");

            if (surfaces.Length == 0)
            {
                ApplyFallback(van, logo);
                return;
            }

            for (int i = 0; i < surfaces.Length; i++)
            {
                var surface = surfaces[i];
                if (surface == null || surface.BottomLeftPoint == null) continue;

                var worldWidth = surface.Width * SpraySurface.PIXEL_SIZE;
                var worldHeight = surface.Height * SpraySurface.PIXEL_SIZE;
                // Fit our square logo inside the panel with some margin rather than stretching
                // it to fill a (probably non-square) canvas.
                var decalSize = Mathf.Min(worldWidth, worldHeight) * 0.7f;
                // BottomLeftPoint.forward is the surface's outward paint normal (still the
                // right FACING direction - only its "up"/roll was wrong, fixed below). But
                // CenterPoint itself sits at local Z=0 on BottomLeftPoint with no depth offset
                // (confirmed from the decompiled SpraySurface.ToWorldPosition, whose own
                // `offset` param defaults to 0f) - the real in-game graffiti render goes through
                // a DecalProjector component with its own separately hand-placed Z depth
                // (ResizeProjector never touches Projector's Z from BottomLeftPoint), not a
                // flat quad sitting exactly on that point. Likely explanation for "van decal
                // didn't work": our quad is coplanar with, or literally behind/inside, the van's
                // own body mesh at that exact position, so it's fully occluded. Push it out
                // along the same outward normal so it sits proud of the paint layer instead.
                const float outwardOffset = 0.02f;
                var worldCenter = surface.CenterPoint + surface.BottomLeftPoint.forward * outwardOffset;
                // Using BottomLeftPoint.rotation wholesale also inherits its "up" (whatever roll
                // the artist gave it for their own pixel coordinate convention) - confirmed
                // wrong from a screenshot showing the decal lying flat, roughly 90 degrees from
                // vertical ("looks like is 90 deg to the ground"). Rebuilding the rotation from
                // the same forward vector but the van's actual world-up keeps the correct facing
                // while forcing the image upright.
                var worldRotation = Quaternion.LookRotation(surface.BottomLeftPoint.forward, van.transform.up);

                MelonLogger.Msg($"GRQD-Livery: surface[{i}] '{surface.name}' panel={surface.Width}x{surface.Height}px " +
                    $"({worldWidth:F2}x{worldHeight:F2}m) rawCenter={surface.CenterPoint} offsetCenter={worldCenter} decalSize={decalSize:F2}.");

                CreateDecal(van.transform, logo, worldCenter, worldRotation, decalSize);
            }
        }

        // Only reached if a spawned "van" genuinely has no graffiti-surface components at all -
        // measures the mesh bounds instead so there's still *something* visible rather than
        // nothing, using the standard Unity vehicle convention (local +X = right, +Z = forward).
        private static void ApplyFallback(LandVehicle van, Sprite logo)
        {
            var renderers = van.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0)
            {
                MelonLogger.Warning("GRQD-Livery: van has no MeshRenderer children either - can't place a fallback decal.");
                return;
            }

            Vector3 localMin = Vector3.positiveInfinity, localMax = Vector3.negativeInfinity;
            for (int i = 0; i < renderers.Length; i++)
            {
                var b = renderers[i].bounds;
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var worldCorner = b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz));
                    var localCorner = van.transform.InverseTransformPoint(worldCorner);
                    localMin = Vector3.Min(localMin, localCorner);
                    localMax = Vector3.Max(localMax, localCorner);
                }
            }

            var localSize = localMax - localMin;
            var localCenter = (localMin + localMax) * 0.5f;
            MelonLogger.Msg($"GRQD-Livery: fallback - van local bounds size={localSize} center={localCenter} (from {renderers.Length} renderers).");

            float decalSize = Mathf.Clamp(localSize.y * 0.5f, 0.4f, 2.5f);
            float sideOffset = localMax.x + 0.03f;
            float heightPos = localCenter.y + localSize.y * 0.05f;
            float forwardPos = localCenter.z;

            var rightWorldPos = van.transform.TransformPoint(new Vector3(sideOffset, heightPos, forwardPos));
            var leftWorldPos = van.transform.TransformPoint(new Vector3(-sideOffset, heightPos, forwardPos));
            var rightRot = Quaternion.LookRotation(van.transform.right, van.transform.up);
            var leftRot = Quaternion.LookRotation(-van.transform.right, van.transform.up);

            CreateDecal(van.transform, logo, rightWorldPos, rightRot, decalSize);
            CreateDecal(van.transform, logo, leftWorldPos, leftRot, decalSize);
        }

        private static void CreateDecal(Transform vanTransform, Sprite logo, Vector3 worldPosition, Quaternion worldRotation, float worldSize)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GRQD_LiveryDecal";

            // No physics purpose - a primitive comes with a collider by default, strip it so
            // it doesn't interfere with the van's own collision.
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            go.transform.position = worldPosition;
            go.transform.rotation = worldRotation;
            go.transform.localScale = new Vector3(worldSize, worldSize, 1f);
            // worldPositionStays: true - keeps the world placement we just set while still
            // following the van's own transform for movement/rotation from here on.
            go.transform.SetParent(vanTransform, true);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = GetSpriteShader();
                var material = shader != null ? new Material(shader) : renderer.material;
                material.mainTexture = logo.texture;
                material.color = Color.white;
                renderer.material = material;
                MelonLogger.Msg($"GRQD-Livery: decal placed at worldPos={worldPosition} scale={worldSize} shaderFound={shader != null}.");
            }
            else
            {
                MelonLogger.Warning($"GRQD-Livery: decal at worldPos={worldPosition} has no MeshRenderer - texture not applied.");
            }
        }

        // "Sprites/Default" is a safe bet - it's the shader every UI/world sprite in the game
        // already renders with, so it's guaranteed to be compiled into the build (unlike an
        // arbitrary shader name that might've been stripped).
        private static Shader? GetSpriteShader()
        {
            _spriteShader ??= Shader.Find("Sprites/Default");
            return _spriteShader;
        }
    }
}
