using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.Modification;
using UnityEngine;

namespace DeliveryDriver.Middleware
{
    public static class DeliveryVehicles
    {
        // Confirmed via VehicleCodeProbe against the real VehiclePrefabs registry.
        public const string VanCode = "veeper";
        public static readonly EVehicleColor DefaultColor = EVehicleColor.Cyan;

        public static bool IsReady => VehicleManager.InstanceExists;

        public static LandVehicle? SpawnVan(Vector3 position, Quaternion rotation, bool playerOwned = false)
        {
            if (!IsReady) return null;

            var van = VehicleManager.Instance.SpawnAndReturnVehicle(VanCode, position, rotation, playerOwned);
            if (van == null) return null;

            van.ApplyColor(DefaultColor);
            return van;
        }
    }
}
