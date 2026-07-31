using MelonLoader;
using UnityEngine;

namespace LegionCore.Buildings
{
    // Builds a flat asphalt pad with painted divider/boundary stripes from primitives - same
    // approach and same PrimitiveBuilder (safe-shader) as StorefrontFactory, just a simpler
    // flat prop. See ParkingPadOptions for why this isn't the vanilla functional ParkingLot
    // system.
    internal static class ParkingPadFactory
    {
        public static GameObject? Build(Vector3 originLocalZero, Quaternion rotation, ParkingPadOptions o)
        {
            if (o.Length <= 0f || o.Depth <= 0f)
            {
                MelonLogger.Warning($"LegionCore-Buildings: ParkingPad build skipped - length={o.Length} depth={o.Depth}.");
                return null;
            }

            var root = new GameObject("ParkingPad");
            root.transform.SetPositionAndRotation(originLocalZero, rotation);

            PrimitiveBuilder.CreateBox(root.transform, "Asphalt",
                new Vector3(o.Length / 2f, o.PadThickness / 2f, o.Depth / 2f),
                new Vector3(o.Length, o.PadThickness, o.Depth), o.AsphaltColor);

            const float stripeWidth = 0.1f;
            float stripeY = o.PadThickness + 0.005f;

            // Boundary stripe along the outer (away-from-wall) edge.
            PrimitiveBuilder.CreateBox(root.transform, "Stripe_Outer",
                new Vector3(o.Length / 2f, stripeY, o.Depth - stripeWidth / 2f),
                new Vector3(o.Length, 0.01f, stripeWidth), o.LineColor);

            // Space dividers, evenly spaced along Length.
            int spaces = Mathf.Max(1, o.SpaceCount);
            for (int i = 1; i < spaces; i++)
            {
                float x = o.Length * i / spaces;
                PrimitiveBuilder.CreateBox(root.transform, $"Stripe_Divider_{i}",
                    new Vector3(x, stripeY, o.Depth / 2f),
                    new Vector3(stripeWidth, 0.01f, o.Depth), o.LineColor);
            }

            MelonLogger.Msg($"LegionCore-Buildings: parking pad built at {originLocalZero} ({o.Length}x{o.Depth}m, {spaces} space(s)).");
            return root;
        }
    }
}
