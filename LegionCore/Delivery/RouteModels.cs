using System;
using System.Collections.Generic;

namespace LegionCore.Delivery
{
    // v1 only supports Daily. More cadences (Weekly, EveryNDays, ...) are slated for later -
    // keep this enum growable without touching Route's shape.
    public enum RouteCadence
    {
        Daily
    }

    [Serializable]
    public class Route
    {
        public string SourcePropertyCode = string.Empty;
        public string DestinationPropertyCode = string.Empty;

        // Index into the destination Property's LoadingDocks array (vanilla docks are the
        // valid finish per grqd-spec.md - GRQD's own PickupDock is pickup-only).
        public int DestinationLoadingDockIndex;

        public RouteCadence Cadence = RouteCadence.Daily;

        // Bumped by RouteManager whenever this route last actually fired, so a day-pass
        // event that fires more than once (or a route added mid-day) doesn't double-run.
        public int LastFiredDay = -1;
    }

    // Wrapper class purely so UnityEngine.JsonUtility has a top-level object to (de)serialize
    // - it can't serialize a bare List<T> directly.
    [Serializable]
    public class RouteList
    {
        public List<Route> Routes = new();
    }
}
