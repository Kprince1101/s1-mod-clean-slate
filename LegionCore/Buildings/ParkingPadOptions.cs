using UnityEngine;

namespace LegionCore.Buildings
{
    // Cosmetic flat asphalt pad with painted space-divider stripes - NOT a functional
    // ScheduleOne.Map.ParkingLot/ParkingSpot (that vanilla system is built around
    // pre-existing, GUID-baked scene lots for NPC/vanilla traffic; wiring a freshly spawned
    // one into it is a separate, bigger ticket if actually wanted later). Local axes: +X runs
    // along the building wall it sits beside, +Z extends away from that wall.
    public class ParkingPadOptions
    {
        public float Length = 8f;
        public float Depth = 5f;
        public int SpaceCount = 2;
        public float PadThickness = 0.05f;

        public Color AsphaltColor = new(0.12f, 0.12f, 0.13f);
        public Color LineColor = new(0.82f, 0.82f, 0.78f);
    }
}
