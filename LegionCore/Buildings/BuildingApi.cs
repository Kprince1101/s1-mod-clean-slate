using UnityEngine;

namespace LegionCore.Buildings
{
    internal sealed class BuildingApi : IBuildingApi
    {
        public bool IsReady => Readiness.Check();

        public GameObject? SpawnStorefrontShell(Vector3 originSW, Quaternion rotation, StorefrontShellOptions? options = null)
            => StorefrontFactory.Build(originSW, rotation, options ?? new StorefrontShellOptions());

        public bool PrepareSite(Transform buildingTransform, SitePrepOptions options)
            => TerrainSitePrep.Run(buildingTransform, options);

        public GameObject? SpawnParkingPad(Vector3 originLocalZero, Quaternion rotation, ParkingPadOptions? options = null)
            => ParkingPadFactory.Build(originLocalZero, rotation, options ?? new ParkingPadOptions());
    }
}
