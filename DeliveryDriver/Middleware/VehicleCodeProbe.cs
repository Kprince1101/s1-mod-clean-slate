using HarmonyLib;
using Il2CppScheduleOne.Vehicles;
using MelonLoader;

namespace DeliveryDriver.Middleware
{
    // Dev tool, not shipped functionality. Logs every vehicleCode/vehicleName VehicleManager
    // has registered, straight from its own prefab list - no need to buy a van, order a
    // delivery, or get in one. Delete this file once DeliveryVehicles.VanCodePlaceholder is
    // filled in with the real code.
    [HarmonyPatch(typeof(VehicleManager), "Start")]
    internal static class VehicleManager_Start_Probe
    {
        private static bool _logged;

        [HarmonyPostfix]
        private static void Postfix(VehicleManager __instance)
        {
            if (_logged) return;
            _logged = true;

            var prefabs = __instance.VehiclePrefabs;
            if (prefabs == null)
            {
                MelonLogger.Warning("[VehicleCodeProbe] VehiclePrefabs is null.");
                return;
            }

            MelonLogger.Msg($"[VehicleCodeProbe] {prefabs.Count} registered vehicle prefabs:");
            foreach (var v in prefabs)
                MelonLogger.Msg($"[VehicleCodeProbe]   code='{v.VehicleCode}' name='{v.VehicleName}'");
        }
    }
}
