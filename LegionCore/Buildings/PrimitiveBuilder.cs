using UnityEngine;

namespace LegionCore.Buildings
{
    // Shared plain-cube builder for StorefrontFactory/ParkingPadFactory - same "spawn a
    // primitive, set local transform, tint its material" pattern as VanLivery's decals,
    // including VanLivery's fix: GameObject.CreatePrimitive's default material uses whatever
    // shader Unity assigned it, which isn't guaranteed to survive this Il2Cpp build's shader
    // stripping (suspected root cause of the shell reporting a successful build but rendering
    // nothing - "there is no building"). Force "Sprites/Default" instead, same shader
    // VanLivery already confirmed renders here.
    internal static class PrimitiveBuilder
    {
        private static Shader? _shader;

        public static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var shader = GetShader();
                var material = shader != null ? new Material(shader) : renderer.material;
                material.color = color;
                renderer.material = material;
            }
            return go;
        }

        private static Shader? GetShader()
        {
            _shader ??= Shader.Find("Sprites/Default");
            return _shader;
        }
    }
}
