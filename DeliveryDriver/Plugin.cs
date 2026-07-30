using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles.Modification;
using LegionCore;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(DeliveryDriver.Plugin), "Global Real Quick Delivery", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DeliveryDriver
{
    public class Plugin : MelonMod
    {
        private const string ShopName = "Global Real Quick Delivery";
        private static readonly Color ShopColor = new(0f, 0.5f, 0.5f); // teal
        private static readonly EVehicleColor VanColor = EVehicleColor.Cyan;

        private bool _testVanSpawned;

        public override void OnInitializeMelon()
        {
            Api.Initialize();
            Api.Delivery.RegisterShopTile(ShopName, ShopColor, ShopName);
            LoggerInstance.Msg("Delivery Driver loaded.");
        }

        public override void OnUpdate()
        {
            Api.CheckVersion();

            // Temporary: spawns one test van near the local player the first frame it's
            // possible. Proves spawn + color end-to-end before real route scheduling exists.
            // Remove once routes drive real spawns (DD2 step 5).
            if (_testVanSpawned || !Api.Vehicles.IsReady || Player.Local == null) return;
            _testVanSpawned = true;

            var spawnPos = Player.Local.PlayerBasePosition + new Vector3(5f, 0f, 5f);
            var van = Api.Vehicles.SpawnVan(spawnPos, Quaternion.identity, VanColor);
            LoggerInstance.Msg(van != null
                ? $"GRQD test van spawned near player at {spawnPos}."
                : "GRQD test van spawn failed.");
        }
    }
}
