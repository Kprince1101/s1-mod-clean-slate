using DeliveryDriver.Middleware;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(DeliveryDriver.Plugin), "Global Real Quick Delivery", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DeliveryDriver
{
    public class Plugin : MelonMod
    {
        private HarmonyLib.Harmony? _harmony;
        private bool _testVanSpawned;

        public override void OnInitializeMelon()
        {
            _harmony = new HarmonyLib.Harmony("DeliveryDriver");
            _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            LoggerInstance.Msg("Delivery Driver loaded.");
        }

        // Temporary: spawns one test van the first frame VehicleManager is ready, at an
        // arbitrary world position. Proves spawn + color end-to-end before real route
        // scheduling exists. Remove once routes drive real spawns (DD2 step 5).
        public override void OnUpdate()
        {
            if (_testVanSpawned || !DeliveryVehicles.IsReady) return;
            _testVanSpawned = true;

            var van = DeliveryVehicles.SpawnVan(Vector3.zero, Quaternion.identity);
            LoggerInstance.Msg(van != null
                ? "GRQD test van spawned at Vector3.zero."
                : "GRQD test van spawn failed.");
        }
    }
}
