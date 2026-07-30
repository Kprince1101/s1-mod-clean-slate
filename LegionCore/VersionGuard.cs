using Il2CppScheduleOne.Persistence;
using UnityEngine;

namespace LegionCore
{
    internal static class VersionGuard
    {
        public const string SupportedGameVersion = "0.4.6f10";

        private static bool _checked;

        // Non-blocking: only warns. Mods keep running on an unexpected game version.
        public static void CheckOnce()
        {
            if (_checked || !Readiness.Check()) return;
            _checked = true;

            float supported = SaveManager.GetVersionNumber(SupportedGameVersion);
            float running = SaveManager.GetVersionNumber(Application.version);
            if (running == supported) return;

            Api.Notifications.Send(
                "Mod update needed",
                $"Schedule I updated ({Application.version}, mods verified against {SupportedGameVersion}) - some features may be unstable.");
        }
    }
}
