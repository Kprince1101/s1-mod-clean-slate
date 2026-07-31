using System;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.UI.Phone;
using LegionCore.Delivery;
using LegionCore.Ui;
using MelonLoader;
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
    // all; its entry point is GRQD's own tile inside the vanilla Delivery app's shop list
    // (see LegionCore.Delivery.DeliveryShopTileFactory's custom-onClick path and GRQD/
    // Plugin.cs), wired to Toggle() below, without ever needing a new App<T> instantiation.
    // (A standalone home-screen icon was tried first via UiFactory.InstallHomeScreenIcon -
    // that method's still in LegionCore for reuse, just not used by GRQD anymore.)
    public class GRQDPanel : MonoBehaviour
    {
        public GRQDPanel(IntPtr ptr) : base(ptr) { }

        // Whitish, opaque background (was a translucent near-black - reported as "seems
        // transparent" once the missing HomeScreen-hide fix below stopped icons showing
        // through). Text/buttons switched to dark-on-light / teal-brand-on-light to match.
        private static readonly Color BackgroundColor = new Color(0.96f, 0.96f, 0.94f, 1f);
        private static readonly Color TextColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        private static readonly Color AccentColor = new Color(0f, 0.5f, 0.5f, 1f); // matches Plugin.ShopColor
        private static readonly Color TabInactiveColor = new Color(0.85f, 0.85f, 0.83f, 1f);

        private GameObject? _root;
        private RectTransform? _content;
        private Button? _setupTabButton;
        private Button? _routesTabButton;
        private int _activeTab;
        private int _pendingSourceIndex;
        private int _pendingDestIndex;

        // Plain MonoBehaviour Unity messages (Awake/Update/...) are dispatched by name under
        // Il2CppInterop, not via C# virtual override - unlike App<T>/PlayerSingleton<T>, which
        // declare them as real virtual methods. No `override` here.
        private void Awake()
        {
            // Piggyback on the SAME Canvas every real phone app uses (AppsCanvas.canvas - a
            // public field, no reflection needed) instead of building our own standalone
            // full-screen overlay canvas like the first version did. That rendered over the
            // ENTIRE game viewport, not just the phone screen (confirmed from a screenshot:
            // the panel extended well past the phone's bezel into the rest of the HUD).
            // Parenting here means our RectTransform inherits the exact same render mode/
            // position/scale as DeliveryApp, JournalApp, etc. - confined to the phone
            // automatically, no manual screen-space math needed on our end.
            Transform parent = transform;
            if (AppsCanvas.InstanceExists)
            {
                // Canvas enabling is now handled properly in Toggle() (mirroring App<T>.
                // SetOpen()'s AppsCanvas.SetIsOpen call) instead of being force-enabled
                // permanently here - see Toggle() for why that matters.
                parent = AppsCanvas.Instance.canvas.transform;
            }

            // Container itself is opaque now, not Color.clear - it used to just be a full-
            // stretch invisible raycast-blocker with a smaller 3%-97%-inset "Background" card
            // drawn inside it, which left a thin transparent band around all four edges. Mostly
            // unnoticeable, EXCEPT this panel opens on top of the Delivery app's own shop list
            // (see Toggle()), and that band let a sliver of it bleed through - most visible on
            // the left, where the shop list's colored tiles happen to sit (reported: "the space
            // to the lft is weird"). Making the container itself the opaque background removes
            // the gap entirely; the inner "Background" panel below is now just a wash of the
            // same color for organizational parenting, not a second visual layer.
            var container = UiFactory.CreatePanel(parent, BackgroundColor, "GRQDPanel_Root");
            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;
            _root = container.gameObject;
            _root.SetActive(false);

            var root = UiFactory.CreatePanel(container, Color.clear, "Background");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(6f, 6f);
            root.offsetMax = new Vector2(-6f, -6f);

            var title = UiFactory.CreateText(root, "Global Real Quick Delivery", 24, "Title", AccentColor);
            title.fontStyle = FontStyle.Bold;
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0.93f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(10f, 0f);
            titleRect.offsetMax = new Vector2(-10f, 0f);

            var closeButton = UiFactory.CreateButton(root, "Close", Toggle, "CloseButton",
                backgroundColor: new Color(0.82f, 0.82f, 0.8f, 1f), textColor: TextColor, fontSize: 16);
            var closeRect = (RectTransform)closeButton.transform;
            closeRect.anchorMin = new Vector2(0.8f, 0.935f);
            closeRect.anchorMax = new Vector2(0.99f, 0.995f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;

            // Tab bar - was previously one long unbroken list of every section at once, which
            // read as cramped/hard-to-read at the old smaller font sizes. Splitting into two
            // tabs (mirrors the vanilla Delivery app's own Shops/Active Orders/Past Orders tab
            // bar) leaves room for everything below to be sized up instead.
            var tabBar = UiFactory.CreatePanel(root, Color.clear, "TabBar");
            tabBar.anchorMin = new Vector2(0f, 0.82f);
            tabBar.anchorMax = new Vector2(1f, 0.92f);
            tabBar.offsetMin = new Vector2(10f, 2f);
            tabBar.offsetMax = new Vector2(-10f, -2f);

            _setupTabButton = UiFactory.CreateButton(tabBar, "Setup", () => SwitchTab(0), "SetupTabButton", fontSize: 16);
            var setupRect = (RectTransform)_setupTabButton.transform;
            setupRect.anchorMin = new Vector2(0f, 0f);
            setupRect.anchorMax = new Vector2(0.49f, 1f);
            setupRect.offsetMin = Vector2.zero;
            setupRect.offsetMax = Vector2.zero;

            _routesTabButton = UiFactory.CreateButton(tabBar, "Routes", () => SwitchTab(1), "RoutesTabButton", fontSize: 16);
            var routesRect = (RectTransform)_routesTabButton.transform;
            routesRect.anchorMin = new Vector2(0.51f, 0f);
            routesRect.anchorMax = new Vector2(1f, 1f);
            routesRect.offsetMin = Vector2.zero;
            routesRect.offsetMax = Vector2.zero;

            ApplyTabStyle(_setupTabButton, active: true);
            ApplyTabStyle(_routesTabButton, active: false);

            _content = UiFactory.CreatePanel(root, new Color(0f, 0f, 0f, 0f), "Content");
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(1f, 0.80f);
            _content.offsetMin = new Vector2(12f, 10f);
            _content.offsetMax = new Vector2(-12f, -6f);
        }

        // Wired directly to the GRQD shop tile's onClick (see GRQD/Plugin.cs and
        // LegionCore.Delivery.DeliveryShopTileFactory's custom-onClick tile path) - clicking
        // the tile inside the vanilla Delivery app opens this instead of a real DeliveryShop
        // listing screen.
        public void Toggle()
        {
            if (_root == null) return;
            bool willOpen = !_root.activeSelf;
            _root.SetActive(willOpen);
            // Opened from inside the Delivery app's shop list, which lives under the same
            // AppsCanvas - force ourselves to the top of the sibling order so our (opaque)
            // panel actually covers it rather than risking being drawn underneath.
            if (willOpen) _root.transform.SetAsLastSibling();

            // Mirrors the parts of the real App<T>.SetOpen() (see reference/decompiled/
            // ScheduleOne.UI/App.cs) that GRQDPanel doesn't get automatically since it isn't
            // a real App<T> (see the class comment above for why). Without these three calls:
            //  - AppsCanvas stayed permanently force-enabled (old code), which is harmless on
            //    its own, but combined with the next point made the screen look like a mess.
            //  - HomeScreen's icon grid never hid itself, so it kept rendering behind/through
            //    our panel - confirmed via screenshot showing app icons and our text
            //    overlapping. That's also why the background read as "transparent": it wasn't
            //    literally transparent, just visually competing with a fully lit icon grid.
            //  - Phone never rotated into the horizontal "reading" pose - real apps trigger
            //    this via Phone.SetIsHorizontal in SetOpen(), driven by the app's own
            //    Orientation setting. Delivery uses Horizontal; GRQD's content (side-by-side
            //    buttons, wide route rows) fits the same mold, and this is exactly what the
            //    user meant by "it needs to rotate the phone just like the delivery app does".
            if (AppsCanvas.InstanceExists) AppsCanvas.Instance.SetIsOpen(willOpen);
            if (HomeScreen.InstanceExists) HomeScreen.Instance.SetIsOpen(!willOpen);
            if (Phone.InstanceExists) Phone.Instance.SetIsHorizontal(willOpen);

            if (willOpen)
            {
                RefreshContent();

                // Requested: log the player's position every time the app is opened, to
                // gather real coordinates for pinning where the van should spawn each
                // game-day instead of the current "near the player" test-spawn offset.
                if (Player.Local != null)
                {
                    var pos = Player.Local.PlayerBasePosition;
                    MelonLogger.Msg($"GRQD: app opened at player position ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}).");
                }
            }
        }

        private void SwitchTab(int tab)
        {
            _activeTab = tab;
            ApplyTabStyle(_setupTabButton, active: tab == 0);
            ApplyTabStyle(_routesTabButton, active: tab == 1);
            RefreshContent();
        }

        private static void ApplyTabStyle(Button? button, bool active)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null) image.color = active ? AccentColor : TabInactiveColor;
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = active ? Color.white : TextColor;
        }

        private void RefreshContent()
        {
            if (_content == null) return;

            // Index-based child clear, not foreach - foreach (Transform child in transform)
            // throws under Il2CppInterop (see LegionCore.Delivery.DeliveryShopTileFactory).
            for (int i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);

            float y = 0f;
            // Sized up from the original crammed-in-one-screen layout now that each tab only
            // has to fit part of the content.
            const float rowHeight = 44f;
            const float gap = 8f;

            if (_activeTab == 0)
            {
                y = AddSectionHeader(_content, "Driver Locker", y, rowHeight);
                y = AddLockerRow(_content, y, rowHeight, gap);

                y = AddSectionHeader(_content, "Pickup Docks", y, rowHeight + gap);
                var properties = DockRegistry.GetEligibleProperties();
                for (int i = 0; i < properties.Count; i++)
                    y = AddDockRow(_content, properties[i], y, rowHeight, gap);
            }
            else
            {
                y = AddSectionHeader(_content, "Routes (Daily) - " + RouteManager.GetRoutes().Count + "/" + RouteManager.MaxRoutes, y, rowHeight);
                var routes = RouteManager.GetRoutes();
                for (int i = 0; i < routes.Count; i++)
                    y = AddRouteRow(_content, routes[i], i, y, rowHeight, gap);

                y = AddAddRouteRow(_content, y, rowHeight, gap);
            }
        }

        private static float AddSectionHeader(Transform parent, string label, float y, float rowHeight)
        {
            var text = UiFactory.CreateText(parent, label, 20, "Header", TextColor);
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

            var text = UiFactory.CreateText(parent, label, 18, "LockerLabel", TextColor);
            PositionRow((RectTransform)text.transform, y, rowHeight, widthFraction: 0.65f);

            var button = UiFactory.CreateButton(parent, options.Count == 0 ? "No lockers found" : "Cycle", () =>
            {
                if (options.Count == 0) return;
                var currentKey = LockerRegistry.GetAssignedKey();
                int currentIndex = options.FindIndex(o => o.Key == currentKey);
                int nextIndex = (currentIndex + 1) % options.Count;
                LockerRegistry.SetAssignedKey(options[nextIndex].Key);
            }, "LockerCycleButton", backgroundColor: AccentColor, textColor: Color.white, fontSize: 16);
            PositionRow((RectTransform)button.transform, y, rowHeight, xFraction: 0.68f, widthFraction: 0.32f);

            return y + rowHeight + gap;
        }

        private static float AddDockRow(Transform parent, Property property, float y, float rowHeight, float gap)
        {
            bool enabled = DockRegistry.IsEnabled(property.PropertyCode);
            var text = UiFactory.CreateText(parent, property.PropertyName, 18, "DockLabel", TextColor);
            PositionRow((RectTransform)text.transform, y, rowHeight, widthFraction: 0.65f);

            var button = UiFactory.CreateButton(parent, enabled ? "Enabled" : "Disabled", () =>
            {
                DockRegistry.SetEnabled(property.PropertyCode, !DockRegistry.IsEnabled(property.PropertyCode));
            }, "DockToggleButton", backgroundColor: AccentColor, textColor: Color.white, fontSize: 16);
            PositionRow((RectTransform)button.transform, y, rowHeight, xFraction: 0.68f, widthFraction: 0.32f);

            return y + rowHeight + gap;
        }

        private static float AddRouteRow(Transform parent, Route route, int index, float y, float rowHeight, float gap)
        {
            string label = $"{route.SourcePropertyCode} -> {route.DestinationPropertyCode} (dock #{route.DestinationLoadingDockIndex}, {route.Cadence})";
            var text = UiFactory.CreateText(parent, label, 16, "RouteLabel", TextColor);
            PositionRow((RectTransform)text.transform, y, rowHeight, widthFraction: 0.75f);

            var button = UiFactory.CreateButton(parent, "Remove", () => RouteManager.RemoveRouteAt(index), "RouteRemoveButton",
                backgroundColor: AccentColor, textColor: Color.white, fontSize: 16);
            PositionRow((RectTransform)button.transform, y, rowHeight, xFraction: 0.78f, widthFraction: 0.22f);

            return y + rowHeight + gap;
        }

        private float AddAddRouteRow(Transform parent, float y, float rowHeight, float gap)
        {
            var properties = DockRegistry.GetEligibleProperties();
            if (properties.Count == 0)
            {
                UiFactory.CreateText(parent, "No eligible properties yet - enable a pickup dock above first.", 16, "NoPropsLabel", TextColor);
                return y + rowHeight;
            }

            _pendingSourceIndex %= properties.Count;
            _pendingDestIndex %= properties.Count;

            var sourceButton = UiFactory.CreateButton(parent, "Source: " + properties[_pendingSourceIndex].PropertyName, () =>
            {
                _pendingSourceIndex = (_pendingSourceIndex + 1) % properties.Count;
                RefreshContent();
            }, "PendingSourceButton", backgroundColor: AccentColor, textColor: Color.white, fontSize: 16);
            PositionRow((RectTransform)sourceButton.transform, y, rowHeight, widthFraction: 0.48f);

            var destButton = UiFactory.CreateButton(parent, "Dest: " + properties[_pendingDestIndex].PropertyName, () =>
            {
                _pendingDestIndex = (_pendingDestIndex + 1) % properties.Count;
                RefreshContent();
            }, "PendingDestButton", backgroundColor: AccentColor, textColor: Color.white, fontSize: 16);
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
            }, "AddRouteButton", backgroundColor: AccentColor, textColor: Color.white, fontSize: 16);
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
