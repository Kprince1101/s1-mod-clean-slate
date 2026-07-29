using Il2CppScheduleOne.UI;

namespace CleanSlate.Game
{
    /// <summary>
    /// The first piece of our own thin wrapper layer around the game's raw Il2Cpp types --
    /// written directly against Il2CppScheduleOne.UI.NotificationsManager (confirmed via
    /// inspecting Assembly-CSharp.dll: a Singleton&lt;NotificationsManager&gt; with a
    /// SendNotification(title, subtitle, icon, duration, playSound) method). No S1API, no
    /// third-party wrapper of any kind in this call path -- if this works, it proves our own
    /// code can genuinely reach into the game, not just that MelonLoader loaded an assembly.
    /// </summary>
    public static class Notifications
    {
        /// <summary>
        /// True once the game's NotificationsManager singleton exists and is ready to receive
        /// calls. The manager is scene-based, so this is false for a while after plugin init --
        /// callers should poll this (e.g. from MelonMod.OnUpdate) rather than calling Send()
        /// immediately on load.
        /// </summary>
        public static bool IsReady => NotificationsManager.InstanceExists;

        public static void Send(string title, string subtitle, float duration = 5f, bool playSound = true)
        {
            NotificationsManager.Instance.SendNotification(title, subtitle, null, duration, playSound);
        }
    }
}
