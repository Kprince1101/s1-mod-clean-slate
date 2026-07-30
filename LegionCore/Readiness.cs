using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.PlayerScripts;

namespace LegionCore
{
    internal static class Readiness
    {
        public static bool Check() =>
            LoadManager.InstanceExists
            && LoadManager.Instance.IsGameLoaded
            && LoadManager.Instance.LoadStatus == LoadManager.ELoadStatus.None
            && Player.Local != null;
    }
}
