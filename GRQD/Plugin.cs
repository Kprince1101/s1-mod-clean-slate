using Il2CppInterop.Runtime.Injection;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.Vehicles;
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

        // Fixed test-van spawn point, given directly by Legion as two real in-game readings:
        // standing at the front of a van, then at the back, each captured via the "app opened
        // at player position" log line. Spawns at the midpoint, facing along the front->back
        // line - replaces the old "4m in front of wherever the player happens to be facing"
        // logic, which put the van in a different spot every single session and was never
        // reliably findable. This is a known, fixed world location now.
        private static readonly Vector3 TestVanFrontPoint = new Vector3(37.63f, 0.14f, 73.99f);
        private static readonly Vector3 TestVanBackPoint = new Vector3(42.70f, 0.14f, 74.36f);

        private bool _sent;
        private bool _testVanSpawned;
        private bool _appSpawned;
        private bool _vanSettledLogged;
        private float _vanSpawnTime;
        private Sprite? _icon;
        private GRQDPanel? _panel;
        private LandVehicle? _testVan;

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

            // Temporary: spawns one test van at a known, fixed location the first frame the
            // game is ready. Proves spawn + color end-to-end. Safe to leave alongside the real
            // app. See TestVanFrontPoint/TestVanBackPoint above for where that location came
            // from and why it's fixed instead of player-relative now.
            if (!_testVanSpawned && Api.IsGameReady)
            {
                _testVanSpawned = true;

                var spawnPos = (TestVanFrontPoint + TestVanBackPoint) * 0.5f;
                var forward = (TestVanFrontPoint - TestVanBackPoint).normalized;
                var spawnRot = Quaternion.LookRotation(forward, Vector3.up);

                var van = Api.Vehicles.SpawnVan(spawnPos, spawnRot, EVehicleColor.Cyan, livery: _icon);
                if (van != null)
                {
                    _testVan = van;
                    _vanSpawnTime = Time.time;
                    LoggerInstance.Msg($"GRQD test van spawned at fixed point {spawnPos}.");
                    if (Api.Notifications.IsReady)
                        Api.Notifications.Send("GRQD", "Test van spawned at its usual spot.");
                }
                else
                {
                    LoggerInstance.Msg("GRQD test van spawn failed.");
                }
            }

            // One-shot follow-up log ~1.5s after spawn: vehicles are rigidbody-driven, so if
            // the spawn point wasn't perfectly clear the van may slide/fall after spawning.
            // This confirms whether the van's final resting position matches where we placed
            // it, instead of requiring another "still can't find it" round-trip to debug.
            if (_testVan != null && !_vanSettledLogged && Time.time - _vanSpawnTime > 1.5f)
            {
                _vanSettledLogged = true;
                var pos = _testVan.transform.position;
                LoggerInstance.Msg($"GRQD test van settled at ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}).");
            }

            if (_sent || !Api.Notifications.IsReady) return;

            Api.Notifications.Send("GRQD", "Plugin loaded and LegionCore is working.");
            LoggerInstance.Msg("GRQD: sent proof-of-life notification via LegionCore.");
            _sent = true;
        }
    }
}
