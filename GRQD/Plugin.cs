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
        // GRQD's own branding - see docs/grqd-spec.md "Aesthetic / Branding".
        private static readonly Color ShopColor = new Color(0f, 0.5f, 0.5f); // teal

        private bool _sent;
        private bool _testVanSpawned;
        private bool _appSpawned;
        private Sprite? _icon;
        private GRQDPanel? _panel;

        public override void OnInitializeMelon()
        {
            Api.Initialize();

            // Real logo art (pill-in-badge, teal/cream/crimson) baked into the DLL via
            // GRQD.csproj's EmbeddedResource - falls back to the procedural placeholder if
            // the resource is ever missing/renamed rather than crashing.
            _icon = UiFactory.LoadEmbeddedSprite(typeof(Plugin).Assembly, "GRQD.icon.png")
                ?? UiFactory.CreateAppIconSprite(ShopColor);

            // Plain MonoBehaviour injection - safe (see PickupDock). Deriving GRQD's UI from
            // the generic App<T> and injecting *that* is NOT safe - confirmed via an in-game
            // crash log that it NullRefs inside App`1's own Il2Cpp type initializer, since
            // App<GRQDPanel-that-would-have-been> is a closed generic instantiation the game
            // never AOT-baked. See GRQD/App/GRQDPanel.cs for the full explanation.
            ClassInjector.RegisterTypeInIl2Cpp<GRQDPanel>();

            // Went back and forth on shop-tile-vs-dedicated-app-icon as GRQD's entry point;
            // landed on tile only, per direct preference ("liked the shop tile more than the
            // app idea"). Custom onClick below replaces the vanilla "open a DeliveryShop
            // listing screen" behavior entirely (see DeliveryShopTileFactory) - clicking the
            // tile just opens our own GRQDPanel instead. The transparent-cornered icon art
            // reads fine here since the tile row itself is already tileColor (teal) - that's
            // also why no separate composited-background icon is needed for this path (that
            // was only a problem for a standalone home-screen icon, which no longer exists).
            Api.Delivery.RegisterShopTile("Global Real Quick Delivery", ShopColor, string.Empty,
                "Same-Day Product Delivery", _icon, onClick: () =>
                {
                    if (_panel != null) _panel.Toggle();
                    else LoggerInstance.Warning("GRQD: shop tile clicked before GRQDPanel was ready.");
                });

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

                _panel = new GameObject("GRQDPanel").AddComponent<GRQDPanel>();
            }

            // Temporary: spawns one test van near the local player the first frame the game
            // is ready. Proves spawn + color end-to-end. Safe to leave alongside the real app.
            if (!_testVanSpawned && Api.IsGameReady)
            {
                _testVanSpawned = true;
                var spawnPos = Player.Local.PlayerBasePosition + new Vector3(5f, 0f, 5f);
                var van = Api.Vehicles.SpawnVan(spawnPos, Quaternion.identity, EVehicleColor.Cyan, livery: _icon);
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
