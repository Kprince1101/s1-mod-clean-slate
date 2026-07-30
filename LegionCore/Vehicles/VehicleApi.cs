using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.Modification;
using UnityEngine;

namespace LegionCore.Vehicles
{
    internal sealed class VehicleApi : IVehicleApi
    {
        public bool IsReady => Readiness.Check();

        public LandVehicle? SpawnVan(Vector3 position, Quaternion rotation, EVehicleColor color, bool playerOwned = false)
        {
            if (!IsReady) return null;
            return VehicleFactory.Create(VanCode, position, rotation, color, playerOwned);
        }

        // Confirmed via VehicleCodeProbe against the real VehiclePrefabs registry.
        private const string VanCode = "veeper";
    }
}
