using MelonLoader;

[assembly: MelonInfo(typeof(CleanSlate.Plugin), "Clean Slate", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace CleanSlate
{
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
