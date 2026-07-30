using Il2CppScheduleOne.UI;

namespace LegionCore.Notifications
{
    internal sealed class NotificationsApi : INotificationsApi
    {
        public bool IsReady => NotificationsManager.InstanceExists;

        public void Send(string title, string subtitle, float duration = 5f, bool playSound = true)
        {
            if (!IsReady) return;
            NotificationsManager.Instance.SendNotification(title, subtitle, null, duration, playSound);
        }
    }
}
