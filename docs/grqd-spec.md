# GRQD (Global Real Quick Delivery) — Schedule I Logistics Mod

**Company name:** Global Real Quick Delivery (GRQD)
**Type:** Schedule I content mod (MelonLoader / C#, current game version 0.4.6f9+)
**Status:** Spec — build this FIRST (de-risks NPC/vehicle/storage questions before Clean Slate depends on them)
**Author:** Legion

---

## One-line pitch

A repeatable product-delivery van service (Global Real Quick Delivery) that moves your owned product between properties on a schedule, paid daily from a designated locker. Works standalone with vanilla Schedule I and integrates optionally with Clean Slate.

---

## Design Principle

**Own thin middleware layer only.** We build directly against `Assembly-CSharp` for exactly what our mods need — vehicle spawn, schedule/pathing, product movement, locker access. No S1API. No other mod dependencies. Third-party mod dependencies are fragile, chain you to someone else's maintenance cadence, and break on version drift (see: OTC). Our layer exposes and interfaces with only what we need — it does not pretend to be the entire game. That narrow surface is dramatically more maintainable. When the game updates, we fix our small owned layer instead of waiting on anyone.

---

## Foundation Decision: Own Layer Only (Version B eliminated)

**No bake-off. We build on our own thin middleware layer against `Assembly-CSharp` directly.**

The bake-off (own layer vs. S1API) was cut after reviewing OTC's actual crash chain. The crashes traced into `PatchS1APIBugs` — IL patches OTC had to write because the *currently maintained* S1API fork ships buggy custom-NPC code. Those patches then crashed inside Mono.Cecil. That is not a stale-fork problem — that is the current, maintained S1API having the same fundamental bug for the exact capability a custom NPC would need (creation and scheduling). GRQD is van-only and never needs that capability at all, but the same bug would hit any future NPC work. A bake-off would re-discover what the crash logs already documented. Version B is eliminated.

Build on our own layer. Own the version-compat story. When the game updates, we fix our small surface — not theirs.

---

## Spike Results (all three feasibility questions: GREEN)

Confirmed via Mono.Cecil static inspection of `Assembly-CSharp.dll`. Not yet tested in-game — that is M2.

### 1. Custom dock — feasible
`LoadingDock` is a plain `MonoBehaviour`, not a `GridItem`. Not tied to the tile/placement system. We register our own `MonoBehaviour` via `ClassInjector.RegisterTypeInIl2Cpp` (standard Il2CppInterop) and attach to a `GameObject` at any `Transform`. No dependency on vanilla's per-property dock array.

### 2. Storage-to-storage transfer — feasible, no physical handler needed
`ItemSlot.InsertItem` / `TryInsertItemIntoSet(List<ItemSlot>, ItemInstance)` gives a direct API. A locker-A to locker-B move: pull `ItemInstance` off a slot in A (`ClearStoredInstance`), insert into B via `TryInsertItemIntoSet`. `StorageEntity.ItemSlots : List<ItemSlot>` gives direct slot access. **Constraint:** `StorageEntity` extends `NetworkBehaviour` — transfer must run server-side. Same constraint vanilla lives under, nothing mod-specific.

### 3. Van spawn + navigation — feasible, real road AI included for free
`VehicleManager.SpawnAndReturnVehicle(string vehicleCode, Vector3 position, Quaternion rotation, bool playerOwned)` spawns any vehicle at any world position. `VehicleAgent.Navigate(Vector3 location, NavigationSettings settings, callback)` routes via the game's real road AI (road graph, obstacle sweeps) — same system vanilla traffic and vanilla deliveries use. Real road driving costs nothing extra; we just call the API. Van-only, no driver NPC — the van moves on its own.

**Paint persistence — confirmed free.** `LandVehicle.OwnedColor`/`ApplyColor(EVehicleColor)` and `Persistence.Datas.VehicleData.Color` (a `string`) ride on vanilla's own vehicle save/load pipeline. A van spawned via `VehicleManager` and registered normally needs no custom persistence code — if the player repaints it (vanilla spray/customization), the choice survives save/load the same as any vanilla-owned vehicle.

**Resolved.** Full vehicle registry confirmed in-game via `VehicleCodeProbe` (now removed, its job is done): `shitbox`, `shitbox_police`, `bruiser`, `hounddog`, `bruiser_police`, `dinkler`, `cheetah`, `veeper`, `hotbox`. Legion confirmed `veeper` is the van — that's the van code used by `LegionCore.Api.Vehicles.SpawnVan`. (Other body types stay an option later — SUV/truck/beater/novelty — but the van is the v1 pick.)

---

## Aesthetic / Branding

- **Company:** Global Real Quick Delivery
- **Abbreviation:** GRQD
- **Van:** teal, rough hand-tagged GRQD logo on the sides. No literal "Teal" exists in
  vanilla's `EVehicleColor` enum — closest built-in value is `Cyan` (`EVehicleColor.Cyan`),
  used as the default paint applied on spawn.
- **Phone app page:** teal, branded as Global Real Quick Delivery.

---

## Core Mechanics (v1.0 — the public release)

### Routes
- Player defines up to 5 routes. UX should mirror the vanilla assignment pattern, likely
  found: `Il2CppScheduleOne.Tools.ManagementClipboard` — an equippable tool. Player equips
  it, points at/selects a world object implementing `Il2CppScheduleOne.Management.
  IConfigurable`, and a config UI opens for the selection. `Property` already exposes
  `Configurables`/`AddConfigurable` (found in the M1 spike), so this is very likely the same
  system used for employee/route-style assignment generally, not something named `Handler`
  in code. `EConfigurableType` is now confirmed closed (15 vanilla values, no mod slot without
  an enum/IL patch) — real confirmation needs an in-game look at whether that's actually
  workable for route assignment, but the pattern (equip tool, point-select, config UI) is
  confirmed real and is what to mirror, or bypass entirely (see
  `docs/shared-middleware-architecture.md` §6).
- Each route is defined independently, in two separate steps:
  1. **Schedule** — daily, weekly, etc. Cost scales with cadence (exact cost table TBD).
  2. **Start and finish**, set separately:
     - **Start** — a storage unit (property), optionally a specific locker/shelf within it.
     - **Finish** — without Clean Slate: any owned property's vanilla `LoadingDock` that
       isn't full. With Clean Slate installed: the storefront is also a valid finish.
- Each route runs on its **own** configured schedule — not one shared cycle across all 5. A
  daily route fires every day regardless of what a weekly route is doing.

### Pickup docks — source-side only, not a general dock
- Properties that need one: storage unit, bungalow, docks warehouse, barn, manor, and
  technically the storefront (Clean Slate).
- Only three of those are distinct C# types (`Bungalow`, `Manor`, and the generic
  `Business`) — confirmed via static inspection, no `Barn`, `Warehouse`, or `StorageUnit`
  class exists. Storage unit / docks warehouse / barn are `Property`/`Business` *instances*
  distinguished by `PropertyCode`/`PropertyName`, not separate types. Dock-position lookup
  needs to key off that code/name string, not a C# type switch — a type switch silently
  can't handle half this list.
- Our own dock `MonoBehaviour`, spawned at a fixed position per property (keyed by code/name
  per the above). Exact positioning doesn't need to be pretty or realistic — an arbitrary
  workable point per property is fine for v1, not worth blocking on. Player doesn't place it
  in v1.
- **This dock is pickup-only.** It exists solely as a staging point at a route's *start*: the
  van loads from it after product is debited from the source locker/shelf into it. It is not
  a delivery destination and has no function beyond that one job.
- **Destinations are vanilla docks, not ours.** A route's *finish* targets an existing
  `Il2CppScheduleOne.Delivery.LoadingDock` already on the destination property (or the
  storefront, with Clean Slate) — not a second instance of our pickup dock.

### Destination capacity check
- "Not full" is checked with the vanilla API already identified in the M1 spike:
  `DeliveryManager.IsLoadingBayFree(Property destination, int loadingDockIndex)`. No need to
  write our own capacity logic for vanilla-dock destinations. Clean Slate storefront capacity
  check is separate, TBD in that mod.

### Product movement
- Source locker/shelf to pickup-dock staging: storage transfer (server-side
  `TryInsertItemIntoSet`)
- Pickup dock to van: van loads from the dock on arrival
- Van to destination `LoadingDock`/storefront storage: storage transfer on arrival
- Moves owned product only — does not create product

### Payment
- Pay locker location doesn't matter — resolved. Not tied to a route's first stop after all.
  Assigned through the app the same way vanilla assigns things: point-select via
  `ManagementClipboard` (see Routes above), targeting any `IConfigurable` locker the player
  owns. One clean mechanism instead of two (route assignment and pay-locker assignment both
  go through the same clipboard-style flow).
- Cost still scales with a route's schedule cadence (see Routes above).
- Empty pay locker behavior: TBD (warn player, van stops — decide in build)

### Delivery loop (one route's run)
1. That route's configured schedule fires (independent per route)
2. Check source locker/shelf has product, debit via storage transfer to the pickup dock
3. Spawn GRQD van at the pickup dock; debit wage from the player-assigned pay locker
4. Van navigates to the destination `LoadingDock`/storefront via `VehicleAgent.Navigate`
5. Check destination not full (`IsLoadingBayFree` for vanilla dock destinations; storefront
   check TBD)
6. Credit destination storage via storage transfer
7. Van despawns

---

## Standalone + Integration

- **GRQD is fully standalone.** Works with vanilla Schedule I, no dependency on Clean Slate,
  general-purpose logistics for any multi-property operation.
- **The dependency runs one direction.** Clean Slate doesn't hard-require GRQD installed to
  function, but its core loop assumes it: without automated delivery the player is stuck
  manually restocking constantly, which defeats running an actual storefront operation.
  That's the real reason GRQD is built first — not just de-risking shared capabilities, but
  because Clean Slate isn't practically playable without it. "Highly suggested," not
  hard-enforced.

---

## v1 Scope Line

**In:** GRQD van, up to 5 independently-scheduled routes, product movement between a source locker/shelf and a vanilla `LoadingDock` (or storefront, with Clean Slate), pickup-only custom staging dock, pay collected at each route's first stop, GRQD branding + teal app page, vanilla-compatible, Clean-Slate-aware.

**Explicitly deferred:** player-placed dock positioning, load-capacity limits per trip, in-van handler NPC walking to grab product.

---

## Middleware Layer

GRQD has no `Middleware/` folder of its own — all game access goes through the shared
`LegionCore` project (see `docs/shared-middleware-architecture.md`).

**`LegionCore.Vehicles` (`VehicleApi`/`VehicleFactory`)** — spawn confirmed working in-game
(log showed the spawn call succeeding). First test used `Vector3.zero` as the drop point,
which put it somewhere Legion couldn't find in-game (likely underground/void) — switched to
spawning at `Player.Local.PlayerBasePosition + (5, 0, 5)`. Color now goes through
`SendOwnedColor` (the real replicated/persisted path) instead of the cosmetic-only
`ApplyColor` the first draft used. Navigate wrapper over `VehicleAgent.Navigate` not yet
added (waiting on integration step).

**`LegionCore.Delivery` (`DeliveryApi`/`DeliveryShopTileFactory`)** — rewritten from the
original first draft, which was **confirmed broken**: it injected into `deliveryShops` only,
but the actual rendered tile list is `_shopElements` (private, `[SerializeField]`,
Editor-populated only, zero runtime writers anywhere in vanilla — confirmed by direct read of
the real decompiled `DeliveryApp.cs`/`DeliveryShop.cs`). The fix clones both a template
`Button` and `DeliveryShop`, builds a real `DeliveryApp.DeliveryShopElement` pair, and injects
it into `_shopElements` via a Harmony postfix on `DeliveryApp.Start()` (after vanilla's own
loop already ran, not before via `Awake()` as the first draft tried). A Harmony prefix on
`DeliveryShop.CanOrder` blocks ordering on managed shops (no real `ShopInterface` behind
`MatchingShopInterfaceName` yet, so `WillCartFitInVehicle()` would otherwise NPE) until Route
UX (build order step 5) wires up real order logic. **Not yet re-verified in-game.**

**Docks** — not yet built. Custom dock component via `ClassInjector.RegisterTypeInIl2Cpp`. Fixed world positions per property type. Pickup-side staging only, for product pending van pickup — not used as a delivery destination (destinations are vanilla `LoadingDock`s or the storefront).

**Storage transfer** — not yet built. Locker-to-locker item move helper wrapping `ItemSlot` / `TryInsertItemIntoSet`. Generic: takes source `List<ItemSlot>` and dest `List<ItemSlot>`, moves N units of a given item type. Server-side. Reused by Clean Slate (dock to shelf).

---

## Open Questions (decide in build, not before)

1. Can a van get robbed mid-route, or is it inherently safe?
2. Empty pay locker behavior — warn and stop, or warn and continue?
3. Load capacity per trip — unlimited v1, or limit from the start?
4. Confirm in-game which classes actually implement `IConfigurable` and that
   `ManagementClipboard` is really the mechanism vanilla uses for route/employee-style
   assignment, and whether that's workable given the closed `EConfigurableType` enum — or
   whether GRQD's route/pay-locker config should bypass `ManagementInterface` entirely (see
   `docs/shared-middleware-architecture.md` §6).
5. Whether cloning an existing `DeliveryShop` for the app listing is the right long-term
   approach, or whether it should be a from-scratch prefab once we know we need to fully
   override `CanOrder`/`SubmitOrder` anyway.

Not blocking, explicitly resolved as non-issues: exact dock world positions per property
(arbitrary is fine), pay-locker location (app-assigned via clipboard-style selection, not
tied to route stops), GRQD van model (`veeper`, confirmed by Legion).

---

## Build Order

0. Van spawn/color and the GRQD Delivery-app tile — moved into `LegionCore` with both known
   bugs fixed (color persistence, tile visibility). Not yet re-verified in-game post-move.
1. Custom dock at a fixed test position.
2. Locker-to-locker storage transfer helper.
3. Navigate wrapper over `VehicleAgent.Navigate`, added to `LegionCore`'s vehicle domain.
4. Integration — wire dock, transfer, and vehicle spawn/navigate into one end-to-end delivery loop, test in-game on Vortex against 0.4.6f9
5. Route UX — GRQD phone app page (up to 5 routes, pay locker designation, route status), plus redirecting the cloned `DeliveryShop`'s `CanOrder`/`SubmitOrder` away from vanilla vendor checkout into GRQD's own route logic
6. Ship — Thunderstore, IP-safe logo pass, photosensitivity note if applicable
