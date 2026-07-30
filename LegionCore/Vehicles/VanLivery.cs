using Il2CppScheduleOne.Vehicles;
using UnityEngine;

namespace LegionCore.Vehicles
{
    // Slaps a logo decal on both sides of a spawned van - two flat quads, textured with the
    // given sprite, parented to the van so they move/rotate with it. Not a real paint-job
    // (no UV work on the actual body mesh - see ScheduleOne.Vehicles.VehicleColor for how
    // vanilla recolors work, by cloning/recoloring a body material by index; there's no
    // equivalent per-mesh decal slot exposed for arbitrary logos), just two applique panels
    // floating just off the door surface. Position/size are a best guess (size, height,
    // and the +/-X side offset below) since there's no local build/render environment here
    // to check against the actual "veeper" van model - expect to need a screenshot-driven
    // tuning pass once this is actually visible in-game.
    public static class VanLivery
    {
        private static Shader? _spriteShader;

        public static void Apply(LandVehicle? van, Sprite? logo, float worldSize = 1.1f)
        {
            if (van == null || logo == null) return;

            CreateDecal(van.transform, logo, worldSize, sideSign: 1f);  // right side
            CreateDecal(van.transform, logo, worldSize, sideSign: -1f); // left side
        }

        private static void CreateDecal(Transform vanTransform, Sprite logo, float worldSize, float sideSign)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GRQD_LiveryDecal";

            // No physics purpose - a primitive comes with a collider by default, strip it so
            // it doesn't interfere with the van's own collision.
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            go.transform.SetParent(vanTransform, false);
            // Best-guess side-panel position: roughly door height, centered front-to-back,
            // offset outward along local right/left just past the body so it doesn't clip
            // into the mesh. Tune these three numbers once it's visible in-game.
            go.transform.localPosition = new Vector3(sideSign * 0.95f, 0.9f, 0f);
            go.transform.localRotation = Quaternion.LookRotation(sideSign * Vector3.right, Vector3.up);
            go.transform.localScale = new Vector3(worldSize, worldSize, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = GetSpriteShader();
                var material = shader != null ? new Material(shader) : renderer.material;
                material.mainTexture = logo.texture;
                material.color = Color.white;
                renderer.material = material;
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
