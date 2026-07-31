namespace LegionCore.Buildings
{
    // Building-local-space rectangle (relative to the building's own transform - see
    // StorefrontFactory's local axes: +X = width/right, +Z = depth/back) for terrain site
    // prep. Each side gets independent padding so a side that should stay untouched (Legion:
    // "to the left of the building can stay the way it is, theres a sewer entrance there")
    // can use 0 instead of extending past that wall.
    public class SitePrepOptions
    {
        public float LocalXMin;
        public float LocalXMax;
        public float LocalZMin;
        public float LocalZMax;

        // Target flatten height, relative to the building transform's own Y (its ground-level
        // anchor corner) - 0 keeps the site at the same elevation as that corner.
        public float FlattenLocalY;
    }
}
