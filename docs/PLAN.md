# Plan

Read `AGENTS.md` first — it has the non-negotiables (no S1API, middleware-interface-only
game access, no narrative comments, ticket-per-change, PR workflow, beta-only). This doc is
the repo-wide roadmap; per-mod/per-feature specs live in `docs/`.

This repo is a monorepo of independently-shippable mods — see `AGENTS.md` for the
OTC/OTCLoader-style structure. Two mods so far: `CleanSlate/` and `DeliveryDriver/`.

## Concept

**Clean Slate:** a storefront system. Dealers work a zone (roughly one per zone) to legally
sell product. Businesses the player owns generate income and cost rent weekly. Full spec:
`docs/clean-slate-spec.md`.

**Delivery Driver (Global Real Quick Delivery / GRQD, driver "Fry"):** a van service with up
to 5 independently-scheduled routes, moving owned product from a source locker/shelf to a
vanilla dock (or the storefront, with Clean Slate), pay collected at each route's first
stop. v1 is van-only — no physical Fry NPC spawn, he texts the player via an avatar instead.
Fully standalone — no dependency on Clean Slate. Full spec: `docs/delivery-driver-spec.md`.

Build order: **Delivery Driver first, and it's not just about de-risking shared
capabilities (custom dock placement, storage transfer, vehicle spawn/navigation).** The real
reason: **Clean Slate depends on GRQD to be practically playable.** Not a hard technical
requirement — Clean Slate won't crash without it — but without automated delivery the player
is stuck manually restocking constantly, which defeats running an actual storefront
operation. The dependency runs one direction only: GRQD needs nothing from Clean Slate,
Clean Slate needs GRQD to make sense. Since delivery now fully belongs to GRQD, Clean
Slate's own first real build step is the storefront itself, not delivery.

**Third mod, under consideration, not yet started:** a QoL mod handling rent/passive-income
generically — for any owned property, not storefront-specific. Floated as a way to pull that
concern out of Clean Slate rather than building it as Clean Slate's own M4. Not named, not
scaffolded, not decided — parked here until it's worth committing to.

## Milestones

### Delivery Driver

#### DD0 — Foundation ✅

Minimal MelonLoader Il2Cpp plugin scaffold in `DeliveryDriver/`, same pattern as Clean
Slate's M0. Proves the second mod builds and loads independently in the monorepo.

#### DD1 — Own-layer vs. S1API decision ✅

Decided, not just recommended: no bake-off. OTC's crash chain traced into `PatchS1APIBugs` —
the *currently maintained* S1API fork ships buggy custom-NPC code that needed runtime IL
patches, and the patcher itself crashed inside Mono.Cecil. Own layer only. See "Foundation
Decision" in `docs/delivery-driver-spec.md`.

#### DD2 — Build (per the spec's build order)

0. `Middleware/DeliveryVehicles.cs` (van spawn, default teal-substitute color via
   `EVehicleColor.Cyan`) and `Middleware/DeliveryAppListing.cs` (GRQD tile in the vanilla
   Delivery phone app, via a Harmony prefix on `DeliveryApp.Start()` cloning an existing
   `DeliveryShop`) — ✅ written, **not yet verified in-game**. Real van `vehicleCode` still a
   placeholder (not derivable from `Assembly-CSharp.dll` alone). Listing entry still runs the
   cloned shop's vanilla buy/checkout flow until Route UX (step 5) redirects it.
1. `Middleware/Docks.cs` — custom dock via `ClassInjector`, fixed test position.
2. `Middleware/StorageTransfer.cs` — locker-to-locker transfer helper.
3. Navigate wrapper over `VehicleAgent.Navigate`, folded into `DeliveryVehicles.cs`.
4. Integration — dock → transfer → van pickup → transfer as one tested loop, in-game on
   Vortex.
5. Route UX — up to 5 routes, pay-locker assignment, status, and redirecting the cloned
   `DeliveryShop`'s `CanOrder`/`SubmitOrder` into GRQD's own route logic. Likely mechanism
   found: `ManagementClipboard` + `IConfigurable` (equip tool, point-select a world object,
   config UI opens) — the same pattern for both route assignment and pay-locker assignment.
   Not fully confirmed (interop metadata didn't expose the full `IConfigurable` implementor
   list) — verify in-game before building.
6. Ship — Thunderstore, IP-safe logo pass, photosensitivity note if applicable.

Not yet broken into tickets.

### Clean Slate

#### M0 — Foundation ✅

Minimal MelonLoader Il2Cpp plugin proving build → deploy → MelonLoader load → the middleware
interface reaching into the game, end to end. Confirmed in-game via a real notification sent
through `Middleware/Notifications.cs`.

#### M1 — Delivery/dock spike ✅

Static feasibility check against the actual game assemblies: can we place a custom dock,
move product between storage locations programmatically, spawn/route a vehicle to a custom
point. All three came back feasible. See `docs/delivery-dock-spec.md`.

#### M2 — Storefront core (next up, the actual first build step)

From the spec's original "Storefront core" docket: the physical storefront building,
ownership/buy-unlock flow, on-site product storage. This is Clean Slate's real starting
point now that delivery is GRQD's job, not a separate mechanic Clean Slate builds itself.

- Delivery *into* the storefront's on-site storage is a GRQD concern (a route's finish can
  target the storefront, per the delivery-driver spec) — Clean Slate doesn't build its own
  delivery/dock logic. `docs/delivery-dock-spec.md`'s API findings (dock placement, storage
  transfer, vehicle spawn) live in DD2's build, not here.
- Not yet broken into tickets.

#### M3 — Dealer-as-counter loop

Dealer assigned to a storefront counter; customer walk-in → order → fetch from on-site
storage → deliver → collect cash; diverting a dealer's assigned customers from street deals
to the storefront. **Correction:** this is controlling/redirecting *existing* vanilla dealer
NPCs the player already hires — not creating new custom NPCs. No NPC-creation spike needed
here after all; the risky OTC-crashing capability (spawning brand-new custom NPCs) doesn't
apply to M3. Dealer-behavior modification specifically is proven working by an existing mod
(High Baller, referenced in the delivery-driver spec's prior-art note) — still needs its own
design pass for the storefront-specific behavior (redirect, walk-to-storage, counter
interaction), just not a from-scratch NPC-creation spike.

#### Rent/income — likely extracted to the third mod, not built as Clean Slate's own milestone

Was M4 ("Business economy"). No vanilla rent/passive-income system exists to hook into
(confirmed in the M1 spike) — new middleware either way. Currently leaning toward building
this as the separate QoL mod noted under Concept above, generic to any owned property
rather than storefront-specific, but not decided. If Clean Slate ships before that mod
exists, this reverts to a Clean Slate milestone.

## Status

Delivery Driver: DD0/DD1 done, DD2 in progress — van spawn/color and Delivery-app listing
written, not yet verified in-game; rest of the build order not started.
Clean Slate: M0/M1 done (M1 fed into GRQD, not independent CS work anymore), M2 (storefront
core) is next up, M3 needs a design pass (behavior on existing NPCs, not NPC creation),
rent/income direction undecided
(Clean Slate milestone vs. third mod).

## Open process question

Tickets should live somewhere tools can create/track them and tie directly to PRs. Not yet
resolved which system — flagged in conversation, not blocking the docs/spec work above.
