using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.Modification;
using UnityEngine;

namespace DeliveryDriver.Middleware
{
    public static class DeliveryVehicles
    {
        // TODO: confirm real vehicle code in-game (not derivable from Assembly-CSharp.dll
        // alone — vehicle codes live in a Unity asset registry, not compiled IL). Run with
        // VehicleCodeProbe.cs to log the full registry, then fill this in and delete the
        // probe.
        public const string VanCodePlaceholder = "TODO_VAN_CODE";
        public static readonly EVehicleColor DefaultColor = EVehicleColor.Cyan;

        public static bool IsReady => VehicleManager.InstanceExists;

        public static LandVehicle? SpawnVan(string vehicleCode, Vector3 position, Quaternion rotation, bool playerOwned = false)
        {
            if (!IsReady) return null;

            var van = VehicleManager.Instance.SpawnAndReturnVehicle(vehicleCode, position, rotation, playerOwned);
            if (van == null) return null;

            van.ApplyColor(DefaultColor);
            return van;
        }
    }
}
