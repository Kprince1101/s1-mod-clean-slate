using Il2CppScheduleOne.Vehicles;
using Il2CppScheduleOne.Vehicles.Modification;
using UnityEngine;

namespace LegionCore
{
    public interface IVehicleApi
    {
        bool IsReady { get; }

        // livery, if given, is applied as a decal on both sides of the spawned van (see
        // LegionCore.Vehicles.VanLivery) - optional so callers who don't care about branding
        // (or haven't got a logo loaded yet) aren't forced to pass one.
        LandVehicle? SpawnVan(Vector3 position, Quaternion rotation, EVehicleColor color, bool playerOwned = false, Sprite? livery = null);

        // Drives van to destination via the game's real road AI (VehicleAgent.Navigate) - the
        // same system vanilla traffic/delivery vehicles use, obstacle sweeps and all (see the
        // M1 spike in grqd-spec.md §3). onComplete, if given, fires exactly once: true on
        // arrival, false on path failure or an explicit stop. Returns false immediately (no
        // callback fired) if van has no VehicleAgent to drive with. VehicleAgent.Navigate
        // itself no-ops for non-host callers - true for any single-player session, the only
        // case GRQD supports today.
        bool Navigate(LandVehicle van, Vector3 destination, System.Action<bool>? onComplete = null);
    }

    public interface IDeliveryApi
    {
        bool IsReady { get; }

        // Registers a tile in the vanilla Delivery app's shop list. Runs the next time
        // DeliveryApp.Start() fires (or immediately if it already has). shopInterfaceName
        // with no matching ShopInterface is fine for now - the shop opens with no listings
        // and ordering is blocked, until a real order-flow ticket wires one up. description
        // and icon are optional (icon falls back to whatever the cloned template tile had).
        // onClick, if given, replaces the vanilla "open a DeliveryShop listing screen"
        // behavior entirely - the tile becomes a plain clickable button that runs onClick
        // instead (no DeliveryShop is even created). Use this when a mod wants the tile as
        // just an entry point into its own UI (see GRQD's Plugin.cs).
        void RegisterShopTile(string shopName, Color tileColor, string shopInterfaceName,
            string description = "", Sprite? icon = null, System.Action? onClick = null);
    }

    public interface INpcApi
    {
        bool IsReady { get; }
    }

    public interface IConfigurableApi
    {
        bool IsReady { get; }
    }

    public interface ISaveApi
    {
        bool GetBool(string key, bool defaultValue = false);
        void SetBool(string key, bool value);
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);
        float GetFloat(string key, float defaultValue = 0f);
        void SetFloat(string key, float value);
        string GetString(string key, string defaultValue = "");
        void SetString(string key, string value);
    }

    public interface INotificationsApi
    {
        bool IsReady { get; }
        void Send(string title, string subtitle, float duration = 5f, bool playSound = true);
    }
}
