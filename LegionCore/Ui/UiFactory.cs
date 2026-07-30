using Il2CppScheduleOne.UI.Phone;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LegionCore.Ui
{
    // Small procedural-UI helpers shared by any mod building a custom phone App<T> (GRQD's
    // Delivery app now, Clean Slate's Store app later). No AssetBundle/prefab dependency -
    // everything is built from plain UnityEngine.UI components at runtime, which is a
    // well-established safe pattern under Il2CppInterop (unlike foreach-over-Transform,
    // AddComponent<T>() on built-in Unity component types works fine).
    public static class UiFactory
    {
        // A fresh screen-space-overlay canvas, suitable as an App<T>'s appContainer root.
        public static Canvas CreateRootCanvas(string name, Transform? parent = null)
        {
            // GameObject(string, params Type[]) needs Il2CppSystem.Type, not System.Type -
            // typeof() gives the latter, so plain construction + AddComponent<RectTransform>()
            // (which Unity upgrades the default Transform into, same as any uGUI element) is
            // the safe path under Il2CppInterop.
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreatePanel(Transform parent, Color color, string name = "Panel")
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return (RectTransform)go.transform;
        }

        public static Text CreateText(Transform parent, string content, int fontSize = 24, string name = "Text")
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.text = content;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        // Takes a real System.Action, not UnityAction - under Il2CppInterop, UnityAction (like
        // Il2CppSystem.Action<T> - see DeliveryShopTileFactory's OnSelect cast) isn't a true
        // delegate type, so a bare lambda can't implicitly convert to it at the call site. One
        // explicit cast here beats needing (UnityAction)(() => ...) at every call site.
        public static Button CreateButton(Transform parent, string label, System.Action onClick, string name = "Button")
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.15f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener((UnityAction)onClick);

            var label_ = CreateText(go.transform, label, 20, "Label");
            label_.alignment = TextAnchor.MiddleCenter;
            var labelRect = (RectTransform)label_.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        // Adds a real icon to the vanilla phone home screen without going through App<T> /
        // HomeScreen.GenerateAppIcon<T> - deriving a new type from the generic App<T> base
        // and injecting it via ClassInjector fails under Il2CppInterop (confirmed via a real
        // in-game crash log: App<T> is a brand new closed generic instantiation the game's
        // Il2Cpp metadata never AOT-baked, and its own type initializer NullRefs). This
        // reflects into HomeScreen's protected appIconContainer instead (a live Transform on
        // an already-existing, already-working vanilla instance - no new generic type needed)
        // and drops a plain button there, matching every other home-screen icon's parent.
        // Returns null if HomeScreen isn't ready yet or the field can't be found by reflection.
        public static Button? InstallHomeScreenIcon(string label, System.Action onClick, Sprite? icon = null, string name = "HomeIcon")
        {
            if (!HomeScreen.InstanceExists) return null;
            var homeScreen = HomeScreen.Instance;

            var container = GetMemberValue<RectTransform>(homeScreen, "appIconContainer");
            if (container == null) return null;

            // Prefer cloning the REAL vanilla icon prefab - same Instantiate + Find("Mask/
            // Image")/Find("Label") pattern App<T>.GenerateHomeScreenIcon uses (confirmed from
            // the decompiled source), just done by hand instead of through App<T>. Gives
            // identical sizing/masking/label placement to every other home-screen icon. Falls
            // back to a hand-built button only if the prefab field can't be found or the
            // expected children aren't there - still functional, just plainer.
            var prefab = GetMemberValue<GameObject>(homeScreen, "appIconPrefab");
            if (prefab != null)
            {
                var clone = UnityEngine.Object.Instantiate(prefab, container);
                clone.name = name;

                var iconImage = clone.transform.Find("Mask/Image")?.GetComponent<Image>();
                var labelText = clone.transform.Find("Label")?.GetComponent<Text>();
                var clonedButton = clone.GetComponent<Button>();

                if (iconImage != null && labelText != null && clonedButton != null)
                {
                    if (icon != null) iconImage.sprite = icon;
                    labelText.text = label;

                    // Vanilla apps use this for unread-message-style badges - not relevant to
                    // us, hide it so a stray "0" doesn't show.
                    var notifications = clone.transform.Find("Notifications");
                    if (notifications != null) notifications.gameObject.SetActive(false);

                    clonedButton.onClick.RemoveAllListeners();
                    clonedButton.onClick.AddListener((UnityAction)onClick);
                    return clonedButton;
                }

                UnityEngine.Object.Destroy(clone);
            }

            var button = CreateButton(container, label, onClick, name);
            if (icon != null)
            {
                var image = button.GetComponent<Image>();
                image.sprite = icon;
                image.color = Color.white;
            }
            return button;
        }

        // Reflection helper for reaching non-public members on vanilla singletons (like
        // HomeScreen.appIconContainer above). Checks both fields and properties, since
        // Il2CppInterop-generated wrapper classes aren't guaranteed to represent every native
        // member the same way - safer than assuming GetField alone will find it.
        private static T? GetMemberValue<T>(object instance, string memberName) where T : class
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

            var field = instance.GetType().GetField(memberName, flags);
            if (field != null) return field.GetValue(instance) as T;

            var property = instance.GetType().GetProperty(memberName, flags);
            return property?.GetValue(instance) as T;
        }

        // Loads a PNG baked into an assembly as an EmbeddedResource (see GRQD.csproj's
        // <EmbeddedResource>/<LogicalName>) into a Sprite. Returns null if the resource name
        // doesn't exist or fails to decode - callers should fall back to CreateAppIconSprite
        // or CreateSolidSprite in that case rather than crash on a missing/renamed asset.
        public static Sprite? LoadEmbeddedSprite(System.Reflection.Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            using var memory = new System.IO.MemoryStream();
            stream.CopyTo(memory);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(memory.ToArray())) return null;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        // A flat-color square sprite - used for a mod's app icon when no real art exists yet.
        public static Sprite CreateSolidSprite(Color color, int size = 64)
        {
            var tex = new Texture2D(size, size);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        // Procedural placeholder logo - a white ringed planet (Planet Express-adjacent, per
        // request) on a flat color background, drawn pixel-by-pixel since there's no way to
        // ship real art assets from here. Meant to be swapped for hand-drawn art later: if a
        // real PNG shows up, load it via File.ReadAllBytes + Texture2D.LoadImage instead and
        // this call site doesn't need to change (still returns a Sprite either way).
        public static Sprite CreateAppIconSprite(Color background, int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            var center = new Vector2(size / 2f, size * 0.47f);
            float planetRadius = size * 0.20f;
            float ringRadiusX = size * 0.38f;
            float ringRadiusY = size * 0.12f;
            float ringThickness = size * 0.05f;
            float ringAngleRad = -16f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(-ringAngleRad);
            float sin = Mathf.Sin(-ringAngleRad);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    bool onPlanet = p.magnitude <= planetRadius;

                    // Rotate into ring-local space so the ring reads as tilted, not a flat oval.
                    var rp = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
                    float ringNorm = Mathf.Sqrt((rp.x * rp.x) / (ringRadiusX * ringRadiusX) + (rp.y * rp.y) / (ringRadiusY * ringRadiusY));
                    bool onRing = Mathf.Abs(ringNorm - 1f) * Mathf.Min(ringRadiusX, ringRadiusY) <= ringThickness;

                    pixels[y * size + x] = (onPlanet || onRing) ? Color.white : background;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
