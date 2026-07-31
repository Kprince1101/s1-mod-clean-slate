using System.Collections.Generic;
using System.Linq;
using Il2CppScheduleOne.Delivery;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.Modification;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Delivery
{
    // Defines *what* the driver does and *when* - up to 5 routes (source pickup dock ->
    // destination property + loading dock index), each on its own schedule. Persisted as a
    // hand-rolled delimited string via LegionCore.Api.Save (MelonPreferences-backed) - see
    // the GetRoutes/SaveRoutes comment for why not UnityEngine.JsonUtility.
    //
    // Build Order items 2-4 (grqd-spec.md): storage transfer helper (StorageTransfer.cs),
    // Navigate wrapper (VehicleApi.Navigate), and the end-to-end integration below are now
    // wired up. Not yet in scope, per the spec's own "decide in build" list: pay-locker debit
    // (cost table still TBD), per-trip load capacity beyond the flat VanCapacityUnits below,
    // and any in-van handler NPC. None of this has been run in-game yet - first real test is
    // the next build.
    public static class RouteManager
    {
        // "Unlimited v1, or limit from the start?" (grqd-spec.md Open Questions #3) - picked
        // a real number instead of true-unlimited so one route can't drain an entire warehouse
        // into a single van. Not exposed/tuned yet - easy to revisit once that question is
        // actually decided.
        private const int VanCapacityUnits = 200;
        private const string PrefKey = "GRQD_Routes";
        public const int MaxRoutes = 5;

        private static bool _dayPassHooked;

        // UnityEngine.JsonUtility is the Il2Cpp-side type under interop - it (de)serializes
        // Il2CppSystem.Object instances via native reflection, and can't touch a plain C#
        // POCO like Route at all (confirmed: ToJson(RouteList) fails to even compile, "cannot
        // convert from RouteList to Il2CppSystem.Object"). Hand-rolled delimited format
        // instead - same idea as DockRegistry's comma-joined property codes. Property codes
        // are short internal slugs (see reference/decompiled), not expected to contain '|' or
        // ';', so this is safe without needing to escape anything.
        private const char RouteDelimiter = ';';
        private const char FieldDelimiter = '|';

        public static List<Route> GetRoutes()
        {
            var raw = LegionCore.Api.Save.GetString(PrefKey, string.Empty);
            var routes = new List<Route>();
            if (string.IsNullOrEmpty(raw)) return routes;

            foreach (var chunk in raw.Split(RouteDelimiter))
            {
                if (string.IsNullOrEmpty(chunk)) continue;
                var parts = chunk.Split(FieldDelimiter);
                if (parts.Length != 5) continue;
                if (!int.TryParse(parts[2], out var dockIndex)) continue;
                if (!System.Enum.TryParse<RouteCadence>(parts[3], out var cadence)) continue;
                if (!int.TryParse(parts[4], out var lastFiredDay)) lastFiredDay = -1;

                routes.Add(new Route
                {
                    SourcePropertyCode = parts[0],
                    DestinationPropertyCode = parts[1],
                    DestinationLoadingDockIndex = dockIndex,
                    Cadence = cadence,
                    LastFiredDay = lastFiredDay,
                });
            }
            return routes;
        }

        private static void SaveRoutes(List<Route> routes)
        {
            var chunks = routes.Select(r => string.Join(FieldDelimiter.ToString(),
                r.SourcePropertyCode, r.DestinationPropertyCode, r.DestinationLoadingDockIndex,
                r.Cadence, r.LastFiredDay));
            LegionCore.Api.Save.SetString(PrefKey, string.Join(RouteDelimiter.ToString(), chunks));
        }

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

        // Full delivery loop per grqd-spec.md "Delivery loop (one route's run)": find
        // source/destination, bail if the destination dock is full, spawn+load the van,
        // drive it over via the real road AI, transfer cargo in on arrival, despawn. Runs
        // fire-and-forget - Navigate's callback can land seconds to minutes after this method
        // returns, well after CheckDueRoutes has moved on.
        private static void OnRouteDue(Route route)
        {
            var eligible = DockRegistry.GetEligibleProperties();

            var source = eligible.FirstOrDefault(p => p.PropertyCode == route.SourcePropertyCode);
            if (source == null)
            {
                MelonLogger.Warning($"LegionCore: route source property '{route.SourcePropertyCode}' not found, skipping.");
                return;
            }

            var destination = eligible.FirstOrDefault(p => p.PropertyCode == route.DestinationPropertyCode);
            if (destination == null)
            {
                MelonLogger.Warning($"LegionCore: route destination property '{route.DestinationPropertyCode}' not found, skipping.");
                return;
            }

            if (route.DestinationLoadingDockIndex < 0 || route.DestinationLoadingDockIndex >= destination.LoadingDocks.Length)
            {
                MelonLogger.Warning($"LegionCore: route destination dock index {route.DestinationLoadingDockIndex} " +
                    $"is out of range for '{destination.PropertyName}' ({destination.LoadingDocks.Length} dock(s)), skipping.");
                return;
            }

            if (!NetworkSingleton<DeliveryManager>.InstanceExists ||
                !NetworkSingleton<DeliveryManager>.Instance.IsLoadingBayFree(destination, route.DestinationLoadingDockIndex))
            {
                MelonLogger.Msg($"LegionCore: route {route.SourcePropertyCode} -> {route.DestinationPropertyCode} due today, " +
                    $"but destination dock #{route.DestinationLoadingDockIndex} is full - skipping this run.");
                return;
            }

            // Spawn at the property's own pickup dock if one is enabled there, otherwise fall
            // back to the same fixed offset DockRegistry itself uses to place a dock - keeps
            // van placement consistent whether or not the player actually toggled the dock on
            // for this particular source property.
            var spawnPos = DockRegistry.TryGetDock(route.SourcePropertyCode, out var dock) && dock != null
                ? dock.transform.position
                : source.transform.position + new Vector3(3f, 0f, 3f);

            var van = LegionCore.Api.Vehicles.SpawnVan(spawnPos, Quaternion.identity, EVehicleColor.Cyan);
            if (van == null)
            {
                MelonLogger.Warning("LegionCore: route van spawn failed, skipping this run.");
                return;
            }

            // TODO(grqd-spec.md Open Questions #2): pay-locker debit isn't implemented yet -
            // the cost table is still explicitly TBD in the spec, not worth faking a number.
            // Routes currently run for free.

            int loaded = LoadFromSource(source, van.Storage);
            if (loaded <= 0)
            {
                MelonLogger.Msg($"LegionCore: route {route.SourcePropertyCode} -> {route.DestinationPropertyCode} due today, " +
                    "but source had nothing to move - despawning van.");
                UnityEngine.Object.Destroy(van.gameObject);
                return;
            }

            MelonLogger.Msg($"LegionCore: route van loaded {loaded} unit(s) from '{source.PropertyName}', " +
                $"departing for '{destination.PropertyName}' dock #{route.DestinationLoadingDockIndex}.");

            var targetPos = destination.LoadingDocks[route.DestinationLoadingDockIndex].transform.position;

            LegionCore.Api.Vehicles.Navigate(van, targetPos, success =>
            {
                if (van == null)
                {
                    MelonLogger.Warning("LegionCore: route van no longer exists by the time navigation finished - nothing to deliver.");
                    return;
                }

                if (!success)
                {
                    MelonLogger.Warning($"LegionCore: route van failed to reach '{destination.PropertyName}' - " +
                        "leaving it wherever it stopped for now rather than losing the cargo.");
                    return;
                }

                int delivered = DeliverToDestination(van.Storage, destination);
                MelonLogger.Msg($"LegionCore: route van delivered {delivered} unit(s) to '{destination.PropertyName}' - trip complete.");
                UnityEngine.Object.Destroy(van.gameObject);
            });
        }

        // Pulls from every StorageEntity at the source property (routes don't carry a
        // specific locker selection yet - see RouteModels.cs) into the van's own storage, up
        // to VanCapacityUnits total.
        private static int LoadFromSource(Property source, StorageEntity vanStorage)
        {
            int moved = 0;
            var storages = source.GetComponentsInChildren<StorageEntity>(true);
            for (int i = 0; i < storages.Length && moved < VanCapacityUnits; i++)
            {
                moved += StorageTransfer.MoveAll(storages[i], vanStorage, VanCapacityUnits - moved);
            }
            return moved;
        }

        // Spreads the van's cargo across whatever StorageEntity(s) exist at the destination -
        // vanilla's own LoadingDock.OutputSlots/InputSlots plumbing isn't used here (it's
        // wired for a parked-vehicle-triggers-unload flow that isn't confirmed to drain on
        // its own from mod code); this is a direct, self-contained transfer instead, matching
        // the "own thin middleware layer" design principle in grqd-spec.md. The destination
        // dock is still the real capacity gate via IsLoadingBayFree above.
        private static int DeliverToDestination(StorageEntity vanStorage, Property destination)
        {
            int moved = 0;
            var storages = destination.GetComponentsInChildren<StorageEntity>(true);
            for (int i = 0; i < storages.Length; i++)
            {
                moved += StorageTransfer.MoveAll(vanStorage, storages[i]);
            }
            return moved;
        }
    }
}
