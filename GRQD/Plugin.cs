using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Vehicles.Modification;
using LegionCore;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(GRQD.Plugin), "Global Real Quick Delivery", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace GRQD
{
    public class Plugin : MelonMod
    {
        // GRQD's own branding - see docs/grqd-spec.md "Aesthetic / Branding". The shop tile
        // clones a vanilla template's model/prefab via LegionCore; only ShopColor is ours.
        private const string ShopName = "Global Real Quick Delivery";
        private static readonly Color ShopColor = new Color(0f, 0.5f, 0.5f); // teal

        private bool _sent;
        private bool _testVanSpawned;
        private float _nextDiagLogTime;

        public override void OnInitializeMelon()
        {
            Api.Initialize();

            // Queued immediately - LegionCore installs it the next time DeliveryApp.Start()
            // fires, whenever that is. shopInterfaceName left blank: LegionCore blocks
            // ordering on any managed shop with no real ShopInterface, so this is safe until
            // real order-flow work wires one up (see LegionCore/Interfaces.cs).
            Api.Delivery.RegisterShopTile(ShopName, ShopColor, string.Empty);

            LoggerInstance.Msg("GRQD loaded, waiting for NotificationsManager...");
        }

        // Temporary: spawns one test van near the local player the first frame the game is
        // ready. Proves spawn + color end-to-end before real route scheduling exists. Remove
        // once routes drive real spawns (see docs/grqd-spec.md Core Mechanics).
        public override void OnUpdate()
        {
            Api.CheckVersion();

            // TEMP diagnostic: the van/shop-tile hooks below never fired in testing despite
            // several minutes of play, with no exceptions logged either - meaning Api.IsGameReady
            // (LegionCore.Readiness.Check()) itself is likely never flipping true. This prints
            // every ~3s until it does, so we can see exactly which sub-condition is stuck.
            // Remove once confirmed working.
            if (!Api.IsGameReady && Time.time >= _nextDiagLogTime)
            {
                _nextDiagLogTime = Time.time + 3f;
                bool lmExists = LoadManager.InstanceExists;
                LoggerInstance.Msg("GRQD diag: LoadManager.InstanceExists=" + lmExists
                    + ", IsGameLoaded=" + (lmExists ? LoadManager.Instance.IsGameLoaded.ToString() : "n/a")
                    + ", LoadStatus=" + (lmExists ? LoadManager.Instance.LoadStatus.ToString() : "n/a")
                    + ", Player.Local!=null=" + (Player.Local != null));
            }

            if (!_testVanSpawned && Api.IsGameReady)
            {
                _testVanSpawned = true;
                var spawnPos = Player.Local.PlayerBasePosition + new Vector3(5f, 0f, 5f);
                var van = Api.Vehicles.SpawnVan(spawnPos, Quaternion.identity, EVehicleColor.Cyan);
                LoggerInstance.Msg(van != null
                    ? $"GRQD test van spawned near player at {spawnPos}."
                    : "GRQD test van spawn failed.");
            }

            if (_sent || !Api.Notifications.IsReady) return;

            Api.Notifications.Send("GRQD", "Plugin loaded and LegionCore is working.");
            LoggerInstance.Msg("GRQD: sent proof-of-life notification via LegionCore.");
            _sent = true;
        }
    }
}
