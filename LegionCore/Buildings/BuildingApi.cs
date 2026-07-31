using UnityEngine;

namespace LegionCore.Buildings
{
    internal sealed class BuildingApi : IBuildingApi
    {
        public bool IsReady => Readiness.Check();

        public GameObject? SpawnStorefrontShell(Vector3 originSW, Quaternion rotation, StorefrontShellOptions? options = null)
            => StorefrontFactory.Build(originSW, rotation, options ?? new StorefrontShellOptions());
    }
}
