using MelonLoader;
using UnityEngine;

namespace LegionCore.Buildings
{
    // Builds a rectangular storefront shell entirely from primitives - no S1API/MeshVault
    // (AGENTS.md's no-S1API rule), same "spawn/configure plain UnityEngine objects at
    // runtime" approach VanLivery already uses for decal quads, just at building scale. Front
    // (sidewalk) and east (parking, per Legion's "right side should have a door too") walls
    // each get a true structural door gap; back and west (left, sewer entrance side - left
    // untouched per Legion's direction) stay solid. Windows are flush tinted-glass quads
    // overlaid on the solid wall segments rather than cut openings.
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
            if (o.EastDoorWidth >= o.Depth)
            {
                MelonLogger.Warning($"LegionCore-Buildings: east door width {o.EastDoorWidth} >= building depth {o.Depth} - clamping.");
                o.EastDoorWidth = Mathf.Max(0.5f, o.Depth - 1f);
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
            BuildEastWall(root.transform, o, floorY);
            BuildRoof(root.transform, o, floorY);

            MelonLogger.Msg($"LegionCore-Buildings: storefront shell built at {originSW} " +
                $"({o.Width}x{o.Depth}m, wall height {o.WallHeight}m).");
            return root;
        }

        private static void BuildFoundation(Transform parent, StorefrontShellOptions o)
        {
            var size = new Vector3(o.Width + o.FoundationOverhang * 2f, o.FoundationHeight, o.Depth + o.FoundationOverhang * 2f);
            var pos = new Vector3(o.Width / 2f, o.FoundationHeight / 2f, o.Depth / 2f);
            PrimitiveBuilder.CreateBox(parent, "Foundation", pos, size, o.FoundationColor);
        }

        private static void BuildFrontWall(Transform parent, StorefrontShellOptions o, float floorY)
        {
            float doorLeftX = (o.Width - o.DoorWidth) / 2f;
            float doorRightX = doorLeftX + o.DoorWidth;

            // Left segment, right segment, lintel above the door - leaves a true rectangular
            // gap the door's own width/height, instead of a solid wall with no way through.
            PrimitiveBuilder.CreateBox(parent, "Wall_Front_Left",
                new Vector3(doorLeftX / 2f, floorY + o.WallHeight / 2f, 0f),
                new Vector3(doorLeftX, o.WallHeight, o.WallThickness), o.WallColor);
            PrimitiveBuilder.CreateBox(parent, "Wall_Front_Right",
                new Vector3(doorRightX + (o.Width - doorRightX) / 2f, floorY + o.WallHeight / 2f, 0f),
                new Vector3(o.Width - doorRightX, o.WallHeight, o.WallThickness), o.WallColor);
            PrimitiveBuilder.CreateBox(parent, "Wall_Front_Lintel",
                new Vector3(o.Width / 2f, floorY + o.DoorHeight + (o.WallHeight - o.DoorHeight) / 2f, 0f),
                new Vector3(o.Width, Mathf.Max(0.05f, o.WallHeight - o.DoorHeight), o.WallThickness), o.WallColor);

            BuildWindowRows(parent, o, floorY, doorLeftX, doorRightX);
        }

        // East wall ("right side", per Legion's own left/right corner naming in
        // CleanSlate/Plugin.cs) - faces the parking pad, gets its own door. Same
        // left-segment/right-segment/lintel split as the front wall, just running along Z
        // instead of X.
        private static void BuildEastWall(Transform parent, StorefrontShellOptions o, float floorY)
        {
            if (o.EastDoorWidth <= 0f)
            {
                BuildSolidWall(parent, "Wall_East",
                    new Vector3(o.Width, floorY + o.WallHeight / 2f, o.Depth / 2f),
                    new Vector3(o.WallThickness, o.WallHeight, o.Depth), o.WallColor);
                return;
            }

            float doorFrontZ = (o.Depth - o.EastDoorWidth) / 2f;
            float doorBackZ = doorFrontZ + o.EastDoorWidth;

            PrimitiveBuilder.CreateBox(parent, "Wall_East_Front",
                new Vector3(o.Width, floorY + o.WallHeight / 2f, doorFrontZ / 2f),
                new Vector3(o.WallThickness, o.WallHeight, doorFrontZ), o.WallColor);
            PrimitiveBuilder.CreateBox(parent, "Wall_East_Back",
                new Vector3(o.Width, floorY + o.WallHeight / 2f, doorBackZ + (o.Depth - doorBackZ) / 2f),
                new Vector3(o.WallThickness, o.WallHeight, o.Depth - doorBackZ), o.WallColor);
            PrimitiveBuilder.CreateBox(parent, "Wall_East_Lintel",
                new Vector3(o.Width, floorY + o.EastDoorHeight + (o.WallHeight - o.EastDoorHeight) / 2f, o.Depth / 2f),
                new Vector3(o.WallThickness, Mathf.Max(0.05f, o.WallHeight - o.EastDoorHeight), o.Depth), o.WallColor);
        }

        // Splits WindowCount windows between the front wall's left/right solid segments
        // (proportional to each segment's usable width), instead of a naive even split across
        // the full wall - an even split silently dropped any window slot landing over the
        // door gap (only 2 of 4 requested windows were actually being built).
        private static void BuildWindowRows(Transform parent, StorefrontShellOptions o, float floorY, float doorLeftX, float doorRightX)
        {
            if (o.WindowCount <= 0) return;

            const float segMargin = 0.4f;
            float leftStart = segMargin;
            float leftEnd = doorLeftX - segMargin;
            float rightStart = doorRightX + segMargin;
            float rightEnd = o.Width - segMargin;

            float leftWidth = Mathf.Max(0f, leftEnd - leftStart);
            float rightWidth = Mathf.Max(0f, rightEnd - rightStart);
            float totalWidth = leftWidth + rightWidth;
            if (totalWidth <= 0f) return;

            int leftCount = Mathf.Clamp(Mathf.RoundToInt(o.WindowCount * leftWidth / totalWidth), 0, o.WindowCount);
            int rightCount = o.WindowCount - leftCount;

            PlaceWindowRow(parent, o, floorY, "L", leftStart, leftEnd, leftCount);
            PlaceWindowRow(parent, o, floorY, "R", rightStart, rightEnd, rightCount);
        }

        private static void PlaceWindowRow(Transform parent, StorefrontShellOptions o, float floorY, string label, float xStart, float xEnd, int count)
        {
            if (count <= 0 || xEnd <= xStart) return;
            float span = xEnd - xStart;

            for (int i = 0; i < count; i++)
            {
                float x = xStart + span * (i + 0.5f) / count;
                // Sits just proud of the wall's outward (-Z, front-facing) surface so it
                // doesn't clip into the wall mesh - same fix VanLivery needed for decals.
                var pos = new Vector3(x, floorY + o.WindowSillHeight + o.WindowHeight / 2f, -(o.WallThickness / 2f + 0.02f));
                float width = Mathf.Min(o.WindowWidth, span / count * 0.85f);
                var size = new Vector3(width, o.WindowHeight, 0.02f);
                PrimitiveBuilder.CreateBox(parent, $"Window_{label}{i}", pos, size, o.WindowColor);
            }
        }

        private static void BuildSolidWall(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
            => PrimitiveBuilder.CreateBox(parent, name, localPos, size, color);

        private static void BuildRoof(Transform parent, StorefrontShellOptions o, float floorY)
        {
            var size = new Vector3(o.Width + o.RoofOverhang * 2f, o.RoofThickness, o.Depth + o.RoofOverhang * 2f);
            var pos = new Vector3(o.Width / 2f, floorY + o.WallHeight + o.RoofThickness / 2f, o.Depth / 2f);
            PrimitiveBuilder.CreateBox(parent, "Roof", pos, size, o.RoofColor);
        }
    }
}
