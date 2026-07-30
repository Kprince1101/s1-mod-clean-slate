using System.Reflection;
using LegionCore.Delivery;
using LegionCore.Notifications;
using LegionCore.Npcs;
using LegionCore.Persistence;
using LegionCore.Properties;
using LegionCore.Vehicles;

namespace LegionCore
{
    public static class Api
    {
        public static IVehicleApi Vehicles { get; } = new VehicleApi();
        public static IDeliveryApi Delivery { get; } = new DeliveryApi();
        public static INpcApi Npcs { get; } = new NpcApi();
        public static IConfigurableApi Configurables { get; } = new ConfigurableApi();
        public static ISaveApi Save { get; } = new SaveApi();
        public static INotificationsApi Notifications { get; } = new NotificationsApi();

        public static bool IsGameReady => Readiness.Check();

        private static bool _initialized;

        // Call once per mod's OnInitializeMelon. Only the first call does anything - both
        // mods share one LegionCore.dll copy in the Mods folder, so this guards against
        // patching the same Harmony patches twice.
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            new HarmonyLib.Harmony("LegionCore").PatchAll(Assembly.GetExecutingAssembly());
        }

        // Call every OnUpdate tick; no-ops until the game is ready, then fires at most once.
        public static void CheckVersion() => VersionGuard.CheckOnce();
    }
}
