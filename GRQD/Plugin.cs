using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.Vehicles.Modification;
using GRQD.App;
using LegionCore;
using LegionCore.Delivery;
using LegionCore.Ui;
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
        // Kept alongside GRQDPanel for now as a cheap secondary smoke test - not the primary
        // UI going forward (see conversation history: GRQD gets its own dedicated phone icon
        // instead of living inside the vanilla Delivery app).
        private const string ShopName = "Global Real Quick Delivery";
        private static readonly Color ShopColor = new Color(0f, 0.5f, 0.5f); // teal

        private bool _sent;
        private bool _testVanSpawned;
        private bool _appSpawned;

        public override void OnInitializeMelon()
        {
            Api.Initialize();

            // Plain MonoBehaviour injection - safe (see PickupDock). Deriving GRQD's UI from
            // the generic App<T> and injecting *that* is NOT safe - confirmed via an in-game
            // crash log that it NullRefs inside App`1's own Il2Cpp type initializer, since
            // App<GRQDPanel-that-would-have-been> is a closed generic instantiation the game
            // never AOT-baked. See GRQD/App/GRQDPanel.cs for the full explanation and the
            // replacement approach (UiFactory.InstallHomeScreenIcon).
            ClassInjector.RegisterTypeInIl2Cpp<GRQDPanel>();

            // Queued immediately - LegionCore installs it the next time DeliveryApp.Start()
            // fires, whenever that is. shopInterfaceName left blank: LegionCore blocks
            // ordering on any managed shop with no real ShopInterface, so this is safe until
            // real order-flow work wires one up (see LegionCore/Interfaces.cs).
            Api.Delivery.RegisterShopTile(ShopName, ShopColor, string.Empty);

            LoggerInstance.Msg("GRQD loaded, waiting for NotificationsManager...");
        }

        public override void OnUpdate()
        {
            Api.CheckVersion();

            // Also gated on Phone/HomeScreen existing - both are PlayerSingleton<T>s tied to
            // the local player's own UI and should be up by the time Api.IsGameReady flips,
            // but this avoids a race if they initialize a beat later than Player.Local does.
            if (!_appSpawned && Api.IsGameReady && Phone.InstanceExists && HomeScreen.InstanceExists)
            {
                _appSpawned = true;
                DockRegistry.RestoreFromSave();
                RouteManager.EnsureScheduleHooked();

                var panel = new GameObject("GRQDPanel").AddComponent<GRQDPanel>();
                var icon = UiFactory.InstallHomeScreenIcon("GRQD", panel.Toggle,
                    UiFactory.CreateSolidSprite(ShopColor), "GRQD_HomeIcon");
                LoggerInstance.Msg(icon != null
                    ? "GRQD: home-screen icon installed."
                    : "GRQD: failed to install home-screen icon (appIconContainer not found via reflection).");
            }

            // Temporary: spawns one test van near the local player the first frame the game
            // is ready. Proves spawn + color end-to-end. Safe to leave alongside the real app.
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
