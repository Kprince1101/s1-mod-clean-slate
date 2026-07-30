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
            LegionCore.Api.Initialize();
            LoggerInstance.Msg("Clean Slate loaded, waiting for NotificationsManager...");
        }

        public override void OnUpdate()
        {
            LegionCore.Api.CheckVersion();

            if (_sent || !LegionCore.Api.Notifications.IsReady) return;

            LegionCore.Api.Notifications.Send("Clean Slate", "Plugin loaded and wrapper is working.");
            LoggerInstance.Msg("Clean Slate: sent proof-of-life notification via LegionCore.");
            _sent = true;
        }
    }
}
