using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.Modification;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Vehicles
{
    // Isolates the one game-update-prone construction sequence: spawn, then color via the
    // real replicated path (SendOwnedColor), never the cosmetic-only ApplyColor.
    internal static class VehicleFactory
    {
        public static LandVehicle? Create(string vehicleCode, Vector3 position, Quaternion rotation, EVehicleColor color, bool playerOwned)
        {
            if (!VehicleManager.InstanceExists) return null;

            var van = VehicleManager.Instance.SpawnAndReturnVehicle(vehicleCode, position, rotation, playerOwned);
            if (van == null)
            {
                MelonLogger.Warning($"LegionCore: SpawnAndReturnVehicle('{vehicleCode}') returned null - bad vehicle code?");
                return null;
            }

            van.SendOwnedColor(color);
            return van;
        }
    }
}
