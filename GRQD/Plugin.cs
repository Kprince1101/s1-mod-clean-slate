using LegionCore;
using MelonLoader;

[assembly: MelonInfo(typeof(GRQD.Plugin), "Global Real Quick Delivery", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace GRQD
{
    public class Plugin : MelonMod
    {
        // Scoped down to an innocuous LegionCore proof-of-life check first: does the
        // ProjectReference/Harmony cross-assembly wiring actually work in-game. Van spawn +
        // shop tile registration come back once this round is confirmed clean - see
        // git history for the removed OnUpdate block.
        private bool _sent;

        public override void OnInitializeMelon()
        {
            Api.Initialize();
            LoggerInstance.Msg("GRQD loaded, waiting for NotificationsManager...");
        }

        public override void OnUpdate()
        {
            Api.CheckVersion();

            if (_sent || !Api.Notifications.IsReady) return;

            Api.Notifications.Send("GRQD", "Plugin loaded and LegionCore is working.");
            LoggerInstance.Msg("GRQD: sent proof-of-life notification via LegionCore.");
            _sent = true;
        }
    }
}
