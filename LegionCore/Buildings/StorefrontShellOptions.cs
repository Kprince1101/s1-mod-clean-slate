using UnityEngine;

namespace LegionCore.Buildings
{
    // Tunable dimensions/colors for StorefrontFactory.Build. Defaults sized to Clean Slate's
    // candidate lot (docs/clean-slate-spec.md "Storefront Site"), styled in the spirit of
    // OTC's Big Dispensary (long, flat-roofed, dark brick, front windows + centered door) per
    // Legion's direction - built from scratch with primitives, not copied assets.
    public class StorefrontShellOptions
    {
        public float Width = 11f;

        // Provisional: the spec's back-of-lot reading duplicated the front reading, so the
        // real lot depth isn't confirmed yet. Widen once a real back reading comes in.
        public float Depth = 10f;

        public float WallHeight = 4f;
        public float WallThickness = 0.3f;
        public float FoundationHeight = 0.2f;
        public float FoundationOverhang = 0.15f;
        public float RoofThickness = 0.2f;
        public float RoofOverhang = 0.3f;

        public float DoorWidth = 2f;
        public float DoorHeight = 2.4f;

        public int WindowCount = 4;
        public float WindowWidth = 1.2f;
        public float WindowHeight = 1.6f;
        public float WindowSillHeight = 1.0f;

        public Color WallColor = new(0.16f, 0.1f, 0.09f);
        public Color RoofColor = new(0.08f, 0.08f, 0.08f);
        public Color FoundationColor = new(0.35f, 0.35f, 0.35f);
        public Color WindowColor = new(0.03f, 0.05f, 0.08f);
    }
}
