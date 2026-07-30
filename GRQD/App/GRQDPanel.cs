using System;
using Il2CppScheduleOne.Property;
using LegionCore.Delivery;
using LegionCore.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace GRQD.App
{
    // GRQD's own delivery-management screen. Originally built as a proper phone App<GRQDApp>
    // (see git history) - reverted after confirming via a real in-game log that deriving from
    // a generic Il2Cpp base class this way is a dead end under Il2CppInterop: ClassInjector.
    // RegisterTypeInIl2Cpp<GRQDApp>() threw a NullReferenceException from inside App`1's own
    // Il2Cpp type-initializer, because App<GRQDApp> is a *brand new* closed generic
    // instantiation that was never AOT-baked into the game's Il2Cpp metadata (the game only
    // ever shipped App<DeliveryApp>, App<MapApp>, etc. - not App<T> for an unknown T). That's
    // a structural limitation, not something callable order or a cast can work around.
    //
    // This is a plain MonoBehaviour instead - the same safe, proven injection category as
    // LegionCore.Delivery.PickupDock. It doesn't integrate with App<T>/PlayerSingleton<T> at
    // all; instead UiFactory.InstallHomeScreenIcon reflects into HomeScreen's own protected
    // appIconContainer to add a real icon to the phone's home screen, wired to Toggle() below,
    // without ever needing a new App<T> instantiation.
    public class GRQDPanel : MonoBehaviour
    {
        public GRQDPanel(IntPtr ptr) : base(ptr) { }

        private GameObject? _root;
        private RectTransform? _content;
        private int _pendingSourceIndex;
        private int _pendingDestIndex;

        // Plain MonoBehaviour Unity messages (Awake/Update/...) are dispatched by name under
        // Il2CppInterop, not via C# virtual override - unlike App<T>/PlayerSingleton<T>, which
        // declare them as real virtual methods. No `override` here.
        private void Awake()
        {
            var canvas = UiFactory.CreateRootCanvas("GRQDPanel_Canvas", transform);
            canvas.sortingOrder = 10;
            _root = canvas.gameObject;
            _root.SetActive(false);

            var root = UiFactory.CreatePanel(canvas.transform, new Color(0.05f, 0.05f, 0.05f, 0.92f), "Root");
            root.anchorMin = new Vector2(0.25f, 0.1f);
            root.anchorMax = new Vector2(0.75f, 0.9f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var title = UiFactory.CreateText(root, "Global Real Quick Delivery", 32, "Title");
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0.94f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(20f, 0f);
            titleRect.offsetMax = new Vector2(-20f, 0f);

            var closeButton = UiFactory.CreateButton(root, "Close", Toggle, "CloseButton");
            var closeRect = (RectTransform)closeButton.transform;
            closeRect.anchorMin = new Vector2(0.86f, 0.945f);
            closeRect.anchorMax = new Vector2(0.99f, 0.995f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;

            _content = UiFactory.CreatePanel(root, new Color(0f, 0f, 0f, 0f), "Content");
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(1f, 0.93f);
            _content.offsetMin = new Vector2(20f, 20f);
            _content.offsetMax = new Vector2(-20f, -10f);
        }

        // Wired directly to the home-screen icon's onClick (see UiFactory.InstallHomeScreenIcon
        // in GRQD/Plugin.cs) - no Phone/HomeScreen open-state hook, keep it simple for v1.
        public void Toggle()
        {
            if (_root == null) return;
            bool willOpen = !_root.activeSelf;
            _root.SetActive(willOpen);
            if (willOpen) RefreshContent();
        }

        private void RefreshContent()
        {
            if (_content == null) return;

            // Index-based child clear, not foreach - foreach (Transform child in transform)
            // throws under Il2CppInterop (see LegionCore.Delivery.DeliveryShopTileFactory).
            for (int i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);

            float y = 0f;
            const float rowHeight = 44f;
            const float gap = 6f;

            y = AddSectionHeader(_content, "Driver Locker", y, rowHeight);
            y = AddLockerRow(_content, y, rowHeight, gap);

            y = AddSectionHeader(_content, "Pickup Docks", y, rowHeight + gap);
            var properties = DockRegistry.GetEligibleProperties();
            for (int i = 0; i < properties.Count; i++)
                y = AddDockRow(_content, properties[i], y, rowHeight, gap);

            y = AddSectionHeader(_content, "Routes (Daily) - " + RouteManager.GetRoutes().Count + "/" + RouteManager.MaxRoutes, y, rowHeight + gap);
            var routes = RouteManager.GetRoutes();
            for (int i = 0; i < routes.Count; i++)
                y = AddRouteRow(_content, routes[i], i, y, rowHeight, gap);

            y = AddAddRouteRow(_content, y, rowHeight, gap);
        }

        private static float AddSectionHeader(Transform parent, string label, float y, float rowHeight)
        {
            var text = UiFactory.CreateText(parent, label, 22, "Header");
            var rect = (RectTransform)text.transform;
            PositionRow(rect, y, rowHeight);
            text.fontStyle = FontStyle.Bold;
            return y + rowHeight;
        }

        private static float AddLockerRow(Transform parent, float y, float rowHeight, float gap)
        {
            var assigned = LockerRegistry.GetAssignedLocker();
            var options = LockerRegistry.GetEligibleLockers();
            string label = assigned != null
                ? $"Locker: {assigned.StorageEntityName} ({assigned.name})"
                : "Locker: (none assigned)";

            var text = UiFactory.CreateText(parent, label, 20, "LockerLabel");
            PositionRow((RectTransform)text.transform, y, rowHeight, widthFraction: 0.65f);

            var button = UiFactory.CreateButton(parent, options.Count == 0 ? "No lockers found" : "Cycle", () =>
            {
                if (options.Count == 0) return;
                var currentKey = LockerRegistry.GetAssignedKey();
                int currentIndex = options.FindIndex(o => o.Key == currentKey);
                int nextIndex = (currentIndex + 1) % options.Count;
                LockerRegistry.SetAssignedKey(options[nextIndex].Key);
            }, "LockerCycleButton");
            PositionRow((RectTransform)button.transform, y, rowHeight, xFraction: 0.68f, widthFraction: 0.32f);

            return y + rowHeight + gap;
        }

        private static float AddDockRow(Transform parent, Property property, float y, float rowHeight, float gap)
        {
            bool enabled = DockRegistry.IsEnabled(property.PropertyCode);
            var text = UiFactory.CreateText(parent, property.PropertyName, 20, "DockLabel");
            PositionRow((RectTransform)text.transform, y, rowHeight, widthFraction: 0.65f);

            var button = UiFactory.CreateButton(parent, enabled ? "Enabled" : "Disabled", () =>
            {
                DockRegistry.SetEnabled(property.PropertyCode, !DockRegistry.IsEnabled(property.PropertyCode));
            }, "DockToggleButton");
            PositionRow((RectTransform)button.transform, y, rowHeight, xFraction: 0.68f, widthFraction: 0.32f);

            return y + rowHeight + gap;
        }

        private static float AddRouteRow(Transform parent, Route route, int index, float y, float rowHeight, float gap)
        {
            string label = $"{route.SourcePropertyCode} -> {route.DestinationPropertyCode} (dock #{route.DestinationLoadingDockIndex}, {route.Cadence})";
            var text = UiFactory.CreateText(parent, label, 18, "RouteLabel");
            PositionRow((RectTransform)text.transform, y, rowHeight, widthFraction: 0.75f);

            var button = UiFactory.CreateButton(parent, "Remove", () => RouteManager.RemoveRouteAt(index), "RouteRemoveButton");
            PositionRow((RectTransform)button.transform, y, rowHeight, xFraction: 0.78f, widthFraction: 0.22f);

            return y + rowHeight + gap;
        }

        private float AddAddRouteRow(Transform parent, float y, float rowHeight, float gap)
        {
            var properties = DockRegistry.GetEligibleProperties();
            if (properties.Count == 0)
            {
                UiFactory.CreateText(parent, "No eligible properties yet - enable a pickup dock above first.", 18, "NoPropsLabel");
                return y + rowHeight;
            }

            _pendingSourceIndex %= properties.Count;
            _pendingDestIndex %= properties.Count;

            var sourceButton = UiFactory.CreateButton(parent, "Source: " + properties[_pendingSourceIndex].PropertyName, () =>
            {
                _pendingSourceIndex = (_pendingSourceIndex + 1) % properties.Count;
                RefreshContent();
            }, "PendingSourceButton");
            PositionRow((RectTransform)sourceButton.transform, y, rowHeight, widthFraction: 0.48f);

            var destButton = UiFactory.CreateButton(parent, "Dest: " + properties[_pendingDestIndex].PropertyName, () =>
            {
                _pendingDestIndex = (_pendingDestIndex + 1) % properties.Count;
                RefreshContent();
            }, "PendingDestButton");
            PositionRow((RectTransform)destButton.transform, y, rowHeight, xFraction: 0.5f, widthFraction: 0.48f);

            y += rowHeight + gap;

            var addButton = UiFactory.CreateButton(parent, "Add Daily Route", () =>
            {
                var route = new Route
                {
                    SourcePropertyCode = properties[_pendingSourceIndex].PropertyCode,
                    DestinationPropertyCode = properties[_pendingDestIndex].PropertyCode,
                    DestinationLoadingDockIndex = 0,
                    Cadence = RouteCadence.Daily,
                };
                RouteManager.TryAddRoute(route, out _);
                RefreshContent();
            }, "AddRouteButton");
            PositionRow((RectTransform)addButton.transform, y, rowHeight, widthFraction: 1f);

            return y + rowHeight + gap;
        }

        // Rows are laid out top-down by absolute offset from the content panel's top edge -
        // simplest robust option given we're building this without a VerticalLayoutGroup pass.
        private static void PositionRow(RectTransform rect, float yFromTop, float height, float xFraction = 0f, float widthFraction = 1f)
        {
            rect.anchorMin = new Vector2(xFraction, 1f);
            rect.anchorMax = new Vector2(xFraction + widthFraction, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -yFromTop);
            rect.sizeDelta = new Vector2(0f, height);
        }
    }
}
