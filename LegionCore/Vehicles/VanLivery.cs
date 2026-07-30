using Il2CppScheduleOne.Vehicles;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Vehicles
{
    // Slaps a logo decal on both sides of a spawned van - two flat quads, textured with the
    // given sprite, parented to the van so they move/rotate with it. Not a real paint-job
    // (no UV work on the actual body mesh - see ScheduleOne.Vehicles.VehicleColor for how
    // vanilla recolors work, by cloning/recoloring a body material by index; there's no
    // equivalent per-mesh decal slot exposed for arbitrary logos), just two applique panels
    // floating just off the door surface. Position/size used to be a blind guess (no local
    // build/render environment here to check against the real "veeper" van model) - now
    // measured from the van's actual MeshRenderer bounds at spawn time instead, so placement
    // adapts to the real model. Still logs the measured numbers via MelonLogger in case a
    // screenshot shows it needs further tuning.
    public static class VanLivery
    {
        private static Shader? _spriteShader;

        public static void Apply(LandVehicle? van, Sprite? logo, float worldSize = 1.1f)
        {
            if (van == null || logo == null)
            {
                MelonLogger.Warning($"GRQD-Livery: Apply skipped - van={(van != null)} logo={(logo != null)}");
                return;
            }

            // The old hardcoded offsets (localPosition ~0.95/0.9, scale 1.1) were pure
            // guesswork with no real van dimensions to check against - almost certainly why
            // "van has no livery" was reported (decal buried inside the mesh, or floating far
            // outside it). Measure the van's ACTUAL local-space bounding box instead and derive
            // position/size from that, so placement is correct regardless of the real model's
            // dimensions (assumes the standard Unity vehicle convention already used elsewhere
            // in this file - local +X = right, +Y = up, +Z = forward).
            var renderers = van.GetComponentsInChildren<MeshRenderer>();
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

            if (renderers.Length == 0)
            {
                MelonLogger.Warning("GRQD-Livery: van has no MeshRenderer children - can't measure bounds, falling back to old guessed offsets.");
                CreateDecal(van.transform, logo, new Vector3(0.95f, 0.9f, 0f), worldSize);
                CreateDecal(van.transform, logo, new Vector3(-0.95f, 0.9f, 0f), worldSize);
                return;
            }

            var localSize = localMax - localMin;
            var localCenter = (localMin + localMax) * 0.5f;
            MelonLogger.Msg($"GRQD-Livery: van local bounds size={localSize} center={localCenter} (from {renderers.Length} renderers).");

            // Decal size: a fraction of van height, clamped to something reasonable in case the
            // measured bounds are degenerate (e.g. a single tiny placeholder collider mesh).
            float decalSize = Mathf.Clamp(localSize.y * 0.5f, 0.4f, 2.5f);
            // Just past the outer edge of the body so it sits on the surface instead of inside
            // the mesh or floating far off it.
            float sideOffset = localMax.x + 0.03f;
            float heightPos = localCenter.y + localSize.y * 0.05f; // roughly door height, slightly above center
            float forwardPos = localCenter.z;

            CreateDecal(van.transform, logo, new Vector3(sideOffset, heightPos, forwardPos), decalSize);   // right side
            CreateDecal(van.transform, logo, new Vector3(-sideOffset, heightPos, forwardPos), decalSize, mirrored: true); // left side
        }

        private static void CreateDecal(Transform vanTransform, Sprite logo, Vector3 localPosition, float worldSize, bool mirrored = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GRQD_LiveryDecal";

            // No physics purpose - a primitive comes with a collider by default, strip it so
            // it doesn't interfere with the van's own collision.
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            go.transform.SetParent(vanTransform, false);
            go.transform.localPosition = localPosition;
            // Face outward along local +X or -X depending on side, so the decal is visible from
            // outside the van rather than facing into the body.
            var faceDir = mirrored ? Vector3.left : Vector3.right;
            go.transform.localRotation = Quaternion.LookRotation(faceDir, Vector3.up);
            go.transform.localScale = new Vector3(worldSize, worldSize, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = GetSpriteShader();
                var material = shader != null ? new Material(shader) : renderer.material;
                material.mainTexture = logo.texture;
                material.color = Color.white;
                renderer.material = material;
                MelonLogger.Msg($"GRQD-Livery: decal placed at localPos={localPosition} scale={worldSize} shaderFound={shader != null}.");
            }
            else
            {
                MelonLogger.Warning($"GRQD-Livery: decal at localPos={localPosition} has no MeshRenderer - texture not applied.");
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
