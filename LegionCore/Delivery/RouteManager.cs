using System.Collections.Generic;
using System.Linq;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Delivery
{
    // Defines *what* the driver does and *when* - up to 5 routes (source pickup dock ->
    // destination property + loading dock index), each on its own schedule. Persisted as
    // JSON via LegionCore.Api.Save (MelonPreferences-backed).
    //
    // Scope note: this pass covers configuration + the daily fire trigger only. The actual
    // pickup -> drive -> drop-off -> item-transfer choreography (storage transfer helper,
    // VehicleAgent.Navigate wiring) is still open per grqd-spec.md's Build Order items 2-4 -
    // OnRouteDue below is the hook point for that follow-up work, not the full implementation.
    public static class RouteManager
    {
        private const string PrefKey = "GRQD_RoutesJson";
        public const int MaxRoutes = 5;

        private static bool _dayPassHooked;

        public static List<Route> GetRoutes()
        {
            var raw = LegionCore.Api.Save.GetString(PrefKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return new List<Route>();
            try
            {
                return JsonUtility.FromJson<RouteList>(raw)?.Routes ?? new List<Route>();
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("LegionCore: failed to parse saved GRQD routes, resetting. " + ex.Message);
                return new List<Route>();
            }
        }

        private static void SaveRoutes(List<Route> routes) =>
            LegionCore.Api.Save.SetString(PrefKey, JsonUtility.ToJson(new RouteList { Routes = routes }));

        public static bool TryAddRoute(Route route, out string error)
        {
            var routes = GetRoutes();
            if (routes.Count >= MaxRoutes)
            {
                error = $"Already at the max of {MaxRoutes} routes.";
                return false;
            }
            routes.Add(route);
            SaveRoutes(routes);
            error = string.Empty;
            return true;
        }

        public static void RemoveRouteAt(int index)
        {
            var routes = GetRoutes();
            if (index < 0 || index >= routes.Count) return;
            routes.RemoveAt(index);
            SaveRoutes(routes);
        }

        // Wire the daily-fire check into TimeManager.onDayPass. Safe to call repeatedly -
        // only hooks once per process. Call once readiness is confirmed.
        //
        // onDayPass is an Il2CppSystem.Action, not a real System.Delegate - System.Delegate.
        // Combine doesn't apply to it (see DeliveryShopTileFactory's OnSelect cast for the
        // same interop gotcha with Il2CppSystem.Action<T>). Il2CppInterop generates +/- combine
        // operators on these wrapper types instead, so += combines onto vanilla's own
        // subscribers rather than clobbering them.
        public static void EnsureScheduleHooked()
        {
            if (_dayPassHooked || !NetworkSingleton<TimeManager>.InstanceExists) return;
            _dayPassHooked = true;
            NetworkSingleton<TimeManager>.Instance.onDayPass += (Il2CppSystem.Action)CheckDueRoutes;
        }

        private static void CheckDueRoutes()
        {
            if (!NetworkSingleton<TimeManager>.InstanceExists) return;
            int today = NetworkSingleton<TimeManager>.Instance.ElapsedDays;
            var routes = GetRoutes();
            bool changed = false;

            foreach (var route in routes)
            {
                bool due = route.Cadence switch
                {
                    RouteCadence.Daily => route.LastFiredDay != today,
                    _ => false,
                };
                if (!due) continue;

                route.LastFiredDay = today;
                changed = true;
                OnRouteDue(route);
            }

            if (changed) SaveRoutes(routes);
        }

        // TODO(build order #2-4): pull product from the assigned locker into the source
        // dock, spawn/navigate the van dock -> destination loading dock, transfer into the
        // destination's storage. For now this just proves the schedule fires and confirms
        // the van itself works end to end (reuses the same spawn path as the OnUpdate test
        // van in GRQD/Plugin.cs).
        private static void OnRouteDue(Route route)
        {
            MelonLogger.Msg($"LegionCore: route {route.SourcePropertyCode} -> {route.DestinationPropertyCode} " +
                $"(dock #{route.DestinationLoadingDockIndex}) is due today - pickup/drive/drop-off not yet implemented.");

            var source = DockRegistry.GetEligibleProperties().FirstOrDefault(p => p.PropertyCode == route.SourcePropertyCode);
            if (source == null)
            {
                MelonLogger.Warning($"LegionCore: route source property '{route.SourcePropertyCode}' not found, skipping spawn.");
                return;
            }

            var spawnPos = source.transform.position + new Vector3(3f, 0f, 3f);
            var van = LegionCore.Api.Vehicles.SpawnVan(spawnPos, Quaternion.identity, Il2CppScheduleOne.Vehicles.Modification.EVehicleColor.Cyan);
            MelonLogger.Msg(van != null
                ? $"LegionCore: route van spawned at '{source.PropertyName}' ({spawnPos})."
                : "LegionCore: route van spawn failed.");
        }
    }
}
