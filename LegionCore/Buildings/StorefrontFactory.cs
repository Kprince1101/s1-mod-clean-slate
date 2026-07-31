using MelonLoader;
using UnityEngine;

namespace LegionCore.Buildings
{
    // Builds a rectangular storefront shell entirely from primitives - no S1API/MeshVault
    // (AGENTS.md's no-S1API rule), same "spawn/configure plain UnityEngine objects at
    // runtime" approach VanLivery already uses for decal quads, just at building scale. Only
    // the front door is a true structural gap (walkthrough today, NPC pathing target once
    // M3's customer loop exists); windows are flush tinted-glass quads overlaid on the solid
    // wall rather than cut openings, so the wall only needs two segments plus a lintel instead
    // of a full per-window segmented mesh.
    internal static class StorefrontFactory
    {
        public static GameObject? Build(Vector3 originSW, Quaternion rotation, StorefrontShellOptions o)
        {
            if (o.Width <= 0f || o.Depth <= 0f)
            {
                MelonLogger.Warning($"LegionCore-Buildings: Build skipped - width={o.Width} depth={o.Depth}.");
                return null;
            }
            if (o.DoorWidth >= o.Width)
            {
                MelonLogger.Warning($"LegionCore-Buildings: door width {o.DoorWidth} >= building width {o.Width} - clamping.");
                o.DoorWidth = Mathf.Max(0.5f, o.Width - 1f);
            }

            var root = new GameObject("StorefrontShell");
            root.transform.SetPositionAndRotation(originSW, rotation);

            float floorY = o.FoundationHeight;

            BuildFoundation(root.transform, o);
            BuildFrontWall(root.transform, o, floorY);
            BuildSolidWall(root.transform, "Wall_Back",
                new Vector3(o.Width / 2f, floorY + o.WallHeight / 2f, o.Depth),
                new Vector3(o.Width, o.WallHeight, o.WallThickness), o.WallColor);
            BuildSolidWall(root.transform, "Wall_West",
                new Vector3(0f, floorY + o.WallHeight / 2f, o.Depth / 2f),
                new Vector3(o.WallThickness, o.WallHeight, o.Depth), o.WallColor);
            BuildSolidWall(root.transform, "Wall_East",
                new Vector3(o.Width, floorY + o.WallHeight / 2f, o.Depth / 2f),
                new Vector3(o.WallThickness, o.WallHeight, o.Depth), o.WallColor);
            BuildRoof(root.transform, o, floorY);

            MelonLogger.Msg($"LegionCore-Buildings: storefront shell built at {originSW} " +
                $"({o.Width}x{o.Depth}m, wall height {o.WallHeight}m).");
            return root;
        }

        private static void BuildFoundation(Transform parent, StorefrontShellOptions o)
        {
            var size = new Vector3(o.Width + o.FoundationOverhang * 2f, o.FoundationHeight, o.Depth + o.FoundationOverhang * 2f);
            var pos = new Vector3(o.Width / 2f, o.FoundationHeight / 2f, o.Depth / 2f);
            CreateBox(parent, "Foundation", pos, size, o.FoundationColor);
        }

        private static void BuildFrontWall(Transform parent, StorefrontShellOptions o, float floorY)
        {
            float doorLeftX = (o.Width - o.DoorWidth) / 2f;
            float doorRightX = doorLeftX + o.DoorWidth;

            // Left segment, right segment, lintel above the door - leaves a true rectangular
            // gap the door's own width/height, instead of a solid wall with no way through.
            CreateBox(parent, "Wall_Front_Left",
                new Vector3(doorLeftX / 2f, floorY + o.WallHeight / 2f, 0f),
                new Vector3(doorLeftX, o.WallHeight, o.WallThickness), o.WallColor);
            CreateBox(parent, "Wall_Front_Right",
                new Vector3(doorRightX + (o.Width - doorRightX) / 2f, floorY + o.WallHeight / 2f, 0f),
                new Vector3(o.Width - doorRightX, o.WallHeight, o.WallThickness), o.WallColor);
            CreateBox(parent, "Wall_Front_Lintel",
                new Vector3(o.Width / 2f, floorY + o.DoorHeight + (o.WallHeight - o.DoorHeight) / 2f, 0f),
                new Vector3(o.Width, Mathf.Max(0.05f, o.WallHeight - o.DoorHeight), o.WallThickness), o.WallColor);

            BuildWindows(parent, o, floorY, doorLeftX, doorRightX);
        }

        private static void BuildWindows(Transform parent, StorefrontShellOptions o, float floorY, float doorLeftX, float doorRightX)
        {
            if (o.WindowCount <= 0) return;

            // Evenly spaced across the wall, skipping any slot that would overlap the door
            // gap - a plain even split reads fine for a roughly door-centered front.
            float margin = o.Width * 0.08f;
            float span = o.Width - margin * 2f;

            for (int i = 0; i < o.WindowCount; i++)
            {
                float x = margin + span * (i + 0.5f) / o.WindowCount;
                if (x > doorLeftX - o.WindowWidth / 2f && x < doorRightX + o.WindowWidth / 2f) continue;

                // Sits just proud of the wall's outward (-Z, front-facing) surface so it
                // doesn't z-fight/clip into the wall mesh - same occlusion issue VanLivery
                // hit with flush-placed decals, same fix (a small outward offset).
                var pos = new Vector3(x, floorY + o.WindowSillHeight + o.WindowHeight / 2f, -(o.WallThickness / 2f + 0.02f));
                var size = new Vector3(o.WindowWidth, o.WindowHeight, 0.02f);
                CreateBox(parent, $"Window_{i}", pos, size, o.WindowColor);
            }
        }

        private static void BuildSolidWall(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
            => CreateBox(parent, name, localPos, size, color);

        private static void BuildRoof(Transform parent, StorefrontShellOptions o, float floorY)
        {
            var size = new Vector3(o.Width + o.RoofOverhang * 2f, o.RoofThickness, o.Depth + o.RoofOverhang * 2f);
            var pos = new Vector3(o.Width / 2f, floorY + o.WallHeight + o.RoofThickness / 2f, o.Depth / 2f);
            CreateBox(parent, "Roof", pos, size, o.RoofColor);
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var material = renderer.material;
                material.color = color;
                renderer.material = material;
            }
            return go;
        }
    }
}
