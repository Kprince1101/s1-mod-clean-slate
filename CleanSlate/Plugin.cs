using MelonLoader;

[assembly: MelonInfo(typeof(CleanSlate.Plugin), "Clean Slate", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CleanSlate
{
    /// <summary>
    /// Step 1 of the plan: prove the pipeline (build -> deploy -> MelonLoader load -> our own
    /// wrapper reaching into the game) works before writing a single line of real feature code.
    /// Uses Game.Notifications (our own thin wrapper around Il2CppScheduleOne.UI.NotificationsManager)
    /// to show an in-game notification, not just a log line -- proves the actual Il2Cpp interop
    /// path works, not just that the assembly loaded. Nothing else belongs in this file until
    /// this is confirmed working in-game.
    /// </summary>
    public class Plugin : MelonMod
    {
        private bool _sent;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Clean Slate loaded, waiting for NotificationsManager...");
        }

        public override void OnUpdate()
        {
            if (_sent) return;
            if (!Game.Notifications.IsReady) return;

            Game.Notifications.Send("Clean Slate", "Plugin loaded and wrapper is working.");
            LoggerInstance.Msg("Clean Slate: sent proof-of-life notification via our own wrapper.");
            _sent = true;
        }
    }
}
