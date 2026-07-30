# Shared Middleware Architecture

**Status: implemented** (`LegionCore/`, both mods wired, `AGENTS.md` updated). Scope for this
pass follows option 3 from the "how full is 'full API'" discussion: build what either mod
actually uses today (all 6 domains below get an interface; `Vehicles`/`Delivery`/
`Notifications` are fully wired, `Npcs`/`Configurables` are stubs with no consumer yet,
`Save` is wired but unused so far), structured so a new domain is "add one file," not "start
over." Not yet re-verified in-game post-move — that's the next step.

Supersedes the "each mod owns its own middleware" rule in `AGENTS.md`. Based on direct
reads of the real decompiled Mono source (`reference/decompiled/`, gitignored, local-only)
by 6 research passes covering vehicles, delivery/dock/storage, NPCs, property/management,
the DeliveryApp UI, and singleton/persistence patterns. This is the proposal from the
"lets keep planning until we have something locked" ask — needs a yes before any code
changes below happen.

## 1. Project

New project at repo root, sibling to `CleanSlate/` and `GRQD/`:

```
LegionCore/
  LegionCore.csproj   # net6.0, same Il2Cpp/interop refs as the mods
  Api.cs                                     # the Facade — single static entry point
  Interfaces.cs                              # every interface contract, one file, grouped
  Readiness.cs                               # LoadManager-based composite ready check
  Version.cs                                 # SupportedGameVersion + mismatch alert
  Vehicles/
    VehicleApi.cs                            # IVehicleApi impl
    VehicleFactory.cs                        # spawn/color creation logic
  Delivery/
    DeliveryApi.cs                           # IDeliveryApi impl (shop tile + dock/storage)
    DeliveryShopTileFactory.cs               # Button+DeliveryShop clone, _shopElements inject
  Npcs/
    NpcApi.cs                                # INpcApi impl (dealer redirect)
    DeliveryLocationFactory.cs               # runtime DeliveryLocation creation
  Properties/
    ConfigurableApi.cs                       # IConfigurableApi impl
  Persistence/
    SaveApi.cs                               # ISaveApi impl, MelonPreferences-backed
  Notifications/
    NotificationsApi.cs                      # moved from CleanSlate/Middleware/Notifications.cs
```

Both mods add a `ProjectReference` to this project and delete their own `Middleware/`
folders (`CleanSlate/Middleware/Notifications.cs`, `GRQD/Middleware/*.cs`) — logic
moves in, call sites update to call the shared API instead. `AGENTS.md`'s middleware rule
gets rewritten to point here.

## 2. Pattern: Facade + Factory

**Facade** — one entry point, `Api`, exposing domain sub-APIs by interface:

```csharp
public static class Api
{
    public static IVehicleApi Vehicles { get; } = new VehicleApi();
    public static IDeliveryApi Delivery { get; } = new DeliveryApi();
    public static INpcApi Npcs { get; } = new NpcApi();
    public static IConfigurableApi Configurables { get; } = new ConfigurableApi();
    public static ISaveApi Save { get; } = new SaveApi();
    public static INotificationsApi Notifications { get; } = new NotificationsApi();
    public static bool IsGameReady => Readiness.Check();
}
```

Mods only ever call `Api.Vehicles.SpawnVan(...)`, never touch `Il2CppScheduleOne.*` types
directly. That boundary is what makes rule #6 ("if things in the game change we need to NOT
shit the bed") enforceable — a game update breaks an implementation file, not every call
site.

**Factory** — anywhere the middleware *creates* a game object (vehicle, shop tile, delivery
location), that construction lives in its own `*Factory` class behind the domain API, not
inline. When the game changes a constructor/spawn signature, only the factory changes.

```csharp
public interface IVehicleApi
{
    bool IsReady { get; }
    LandVehicle? SpawnVan(Vector3 position, Quaternion rotation, bool playerOwned = false);
}
```

`VehicleApi.SpawnVan` calls into `VehicleFactory.Create(...)`, which wraps
`VehicleManager.SpawnAndReturnVehicle` (confirmed: does the FishNet network spawn
internally) and fixes the known color bug — `SendOwnedColor(EVehicleColor)` (the real
`ServerRpc`-backed persistence path), not `ApplyColor` (cosmetic-only, confirmed non-
persisting, currently wrong in `DeliveryVehicles.cs`).

## 3. Interfaces — grouped, not scattered

Per rule #6, every interface contract lives in `Interfaces.cs`, one file:

```csharp
public interface IVehicleApi { /* spawn, color */ }
public interface IDeliveryApi { /* shop tile registration, dock/storage transfer */ }
public interface INpcApi { /* dealer redirect */ }
public interface IConfigurableApi { /* see §6 */ }
public interface ISaveApi { /* Get<T>/Set<T> by key */ }
public interface INotificationsApi { /* Send(title, subtitle, ...) */ }
```

Implementations live next to their factories in per-domain folders — that split is about
file size, not about re-scattering the contracts.

## 4. Readiness

Every API method starts by checking `Readiness.Check()` instead of ad-hoc
`InstanceExists` checks per call site:

```csharp
internal static class Readiness
{
    public static bool Check() =>
        LoadManager.InstanceExists
        && LoadManager.Instance.IsGameLoaded
        && LoadManager.Instance.LoadStatus == ELoadStatus.None
        && Player.Local != null;
}
```

Confirmed as the correct composite check — `Singleton<T>`/`NetworkSingleton<T>`/
`PlayerSingleton<T>` all have bare unguarded `Instance` getters, so any single check alone is
a race condition.

## 5. Version-mismatch alert (non-blocking)

```csharp
internal static class Version
{
    public const string SupportedGameVersion = "0.4.6f9";

    public static void CheckOnLoad()
    {
        if (!Readiness.Check()) return;
        // compare via SaveManager's own version-string parser, not raw string equality
        if (SaveManager.GetVersionNumber(Application.version) != SaveManager.GetVersionNumber(SupportedGameVersion))
        {
            Api.Notifications.Send("Mod update needed",
                "The game updated — some features may be unstable until this mod catches up.");
        }
    }
}
```

Called once from each mod's `Plugin.OnInitializeMelon` (or first ready `OnUpdate` tick).
Never blocks load — matches requirement #8 exactly ("go export their game right now, come
back have fun good luck").

## 6. Open implementation decisions (recommendations, not facts — flagging per the no-guessing rule since these are judgment calls, not things only you know)

- **`IConfigurable`/`EConfigurableType` is a closed, non-extensible 15-value enum** — no
  vanilla slot exists for a custom mod type without patching the enum or bypassing
  `ManagementInterface` entirely. Recommendation: bypass it for v1 — GRQD's route/pay-locker
  config gets its own small world-space UI, not a `ManagementClipboard` panel. Revisit
  patching the enum later only if a real need shows up.
- **Persistence**: recommend `ISaveApi` backed by MelonLoader's own preferences file
  (`MelonPreferences` categories/entries), not vanilla's `IGenericSaveable`/
  `GenericSaveablesManager`. Keeps the middleware as the sole API boundary — no shared state
  with vanilla's save schema to break on a game update.
- **DeliveryShop reuse**: clone for the visible tile (`DeliveryShopTileFactory`, see below),
  but do not reuse `CanOrder`/`SubmitOrder` — they're hardwired to vendor-purchase semantics.
  GRQD's order logic gets its own Harmony prefix-skip overrides, backed by a synthetic
  `ShopInterface` kept in sync with player inventory to avoid null refs in `Initialize()`.

## 7. `DeliveryShopTileFactory` — the concrete fix for the invisible-tile bug

Root cause (confirmed by direct source read): `_shopElements` — not `deliveryShops` — is
the real render list, `[SerializeField]`, Editor-only-populated, zero runtime writers
anywhere. The factory:

1. Clones a template `Button` (`Object.Instantiate(template.Button, template.Button.transform.parent)`).
2. Clones a template `DeliveryShop` (as today), sets `ShopColor`/name.
3. Builds a `DeliveryShopElement { Shop, Button }` pair.
4. Injects it into `_shopElements` via `Traverse` (private field, no public setter).
5. Replicates vanilla's own wiring exactly: `SetActive`, `onClick.AddListener`, `OnSelect`
   combine, `Initialize()`.
6. Runs as a **Postfix on `Start()`**, not the current `Awake()` prefix — `Start()` is where
   vanilla itself builds `_shopElements`, so the postfix lands after vanilla's own list is
   built and doesn't fight the ordering.

## 8. `DeliveryLocationFactory` — Clean Slate M3 dealer redirect

Street-deal geometry is driven entirely by `Contract.DeliveryLocation.CustomerStandPoint`,
read by both `DealerAttendDealBehaviour` and `Customer.IsAtDealLocation()`. Non-destructive
redirect: create a runtime `DeliveryLocation` at the storefront counter, register it with a
runtime GUID, swap the contract's `DeliveryLocation` reference to point at it. No vanilla
Dealer/Customer/Behaviour code touched. **Gotcha to respect:** `NPCBehaviour.behaviourStack`
is Editor-only-populated — never add a new `Behaviour` component to a dealer at runtime,
reuse an existing stack member instead.

## 9. The "update the middleware" tool (requirement #3)

Separate dev-only console project, not shipped in either mod's DLL:

```
tools/mw-update-check/
  mw-update-check.csproj      # plain net6.0 console app, Mono.Cecil reference
  watched-members.txt         # curated list: Type.Member lines the middleware depends on
  Program.cs                  # diffs two Assembly-CSharp/interop DLL sets, one old one new
```

Run manually on your machine (needs both game versions' `Managed/` folders — old kept
locally, new after a Steam update). Loads both assemblies with Cecil, resolves every entry
in `watched-members.txt` against both, and emits a Markdown report: unchanged / signature
changed / removed, per member. You read the report, fix whatever broke in the relevant
`*Factory`/`*Api` file, manually re-run mods against the new version (requirement #4 — no
auto-migration). `watched-members.txt` grows every time a new game type gets wrapped.

## 10. What ships once this is locked

1. Scaffold `LegionCore/`, wire into the `.sln`.
2. Move `Notifications.cs` logic in as `NotificationsApi`.
3. Port + fix `DeliveryVehicles.cs` → `VehicleApi`/`VehicleFactory` (the `SendOwnedColor` fix).
4. Rewrite `DeliveryAppListing.cs` → `DeliveryApi`/`DeliveryShopTileFactory` (the `_shopElements` fix).
5. Add `Readiness`, `Version` (alert-on-mismatch), wire both into each `Plugin.cs`.
6. Update both mods' `Plugin.cs`/call sites to use `Api.*`, delete both `Middleware/` folders.
7. Update `AGENTS.md` (single shared middleware, no per-mod middleware).
8. Scaffold `tools/mw-update-check/` (can lag behind — not blocking mod functionality).
9. Commit/push to `restructure/fry-driver-monorepo`, hand you the compare URL as usual.

`IConfigurableApi`/`ISaveApi` get interface + minimal stub implementations in this pass;
full route/pay-locker UI (DD2 step 5) and Clean Slate M3 dealer redirect are separate,
later tickets that will *consume* `NpcApi`/`ConfigurableApi` once those features are
actually being built — not built out fully in this pass.
