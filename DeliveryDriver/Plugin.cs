using DeliveryDriver.Middleware;
using HarmonyLib;
using Il2CppScheduleOne.PlayerScripts;
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

        // Temporary: spawns one test van near the local player the first frame both
        // VehicleManager and Player.Local are ready. Proves spawn + color end-to-end before
        // real route scheduling exists. Remove once routes drive real spawns (DD2 step 5).
        public override void OnUpdate()
        {
            if (_testVanSpawned || !DeliveryVehicles.IsReady || Player.Local == null) return;
            _testVanSpawned = true;

            var spawnPos = Player.Local.PlayerBasePosition + new Vector3(5f, 0f, 5f);
            var van = DeliveryVehicles.SpawnVan(spawnPos, Quaternion.identity);
            LoggerInstance.Msg(van != null
                ? $"GRQD test van spawned near player at {spawnPos}."
                : "GRQD test van spawn failed.");
        }
    }
}
