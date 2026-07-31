using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.AI;
using Il2CppScheduleOne.Vehicles.Modification;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Vehicles
{
    internal sealed class VehicleApi : IVehicleApi
    {
        public bool IsReady => Readiness.Check();

        public LandVehicle? SpawnVan(Vector3 position, Quaternion rotation, EVehicleColor color, bool playerOwned = false, Sprite? livery = null)
        {
            if (!IsReady) return null;
            var van = VehicleFactory.Create(VanCode, position, rotation, color, playerOwned);
            if (livery != null) VanLivery.Apply(van, livery);
            return van;
        }

        public bool Navigate(LandVehicle van, Vector3 destination, System.Action<bool>? onComplete = null)
        {
            if (van == null) return false;

            var agent = van.Agent;
            if (agent == null)
            {
                MelonLogger.Warning("LegionCore: van has no VehicleAgent component, can't Navigate.");
                return false;
            }

            // VehicleAgent.NavigationCallback is a bespoke delegate type declared on the game's
            // own MonoBehaviour, not a BCL/UnityAction type - same interop pattern as the
            // UnityAction cast in DeliveryShopTileFactory: a plain C# lambda needs an explicit
            // cast to the game's own delegate type before Il2CppInterop will accept it.
            VehicleAgent.NavigationCallback? callback = null;
            if (onComplete != null)
            {
                callback = (VehicleAgent.NavigationCallback)((VehicleAgent.ENavigationResult result) =>
                    onComplete(result == VehicleAgent.ENavigationResult.Complete));
            }

            agent.Navigate(destination, null, callback);
            return true;
        }

        // Confirmed via VehicleCodeProbe against the real VehiclePrefabs registry.
        private const string VanCode = "veeper";
    }
}
