using Il2CppScheduleOne.UI;

namespace CleanSlate.Middleware
{
    public static class Notifications
    {
        public static bool IsReady => NotificationsManager.InstanceExists;

        public static void Send(string title, string subtitle, float duration = 5f, bool playSound = true)
        {
            NotificationsManager.Instance.SendNotification(title, subtitle, null, duration, playSound);
        }
    }
}
