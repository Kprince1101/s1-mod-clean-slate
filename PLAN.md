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

#### DD2 — Build (six steps, per the spec's build order)

1. `Middleware/Docks.cs` — custom dock via `ClassInjector`, fixed test position.
2. `Middleware/StorageTransfer.cs` — locker-to-locker transfer helper.
3. `Middleware/DeliveryVehicles.cs` — spawn + navigate wrapper.
4. Integration — dock → transfer → van pickup → transfer as one tested loop, in-game on
   Vortex.
5. Route UX — GRQD phone app page (teal branding, up to 5 routes, pay-locker assignment,
   status). Likely mechanism found: `ManagementClipboard` + `IConfigurable` (equip tool,
   point-select a world object, config UI opens) — the same pattern for both route
   assignment and pay-locker assignment. Not fully confirmed (interop metadata didn't expose
   the full `IConfigurable` implementor list) — verify in-game before building.
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
to the storefront. Needs its own design pass, especially NPC creation — Delivery Driver v1
does **not** de-risk this (it's van-only, no physical NPC spawn), so custom NPC creation is
still open, unspiked, and it's the exact capability that crashed OTC. Spike before designing
further, same treatment M1 gave the delivery mechanic.

#### Rent/income — likely extracted to the third mod, not built as Clean Slate's own milestone

Was M4 ("Business economy"). No vanilla rent/passive-income system exists to hook into
(confirmed in the M1 spike) — new middleware either way. Currently leaning toward building
this as the separate QoL mod noted under Concept above, generic to any owned property
rather than storefront-specific, but not decided. If Clean Slate ships before that mod
exists, this reverts to a Clean Slate milestone.

## Status

Delivery Driver: DD0/DD1 done, DD2 not started (6-step build order in the spec).
Clean Slate: M0/M1 done (M1 fed into GRQD, not independent CS work anymore), M2 (storefront
core) is next up, M3 needs its own NPC-creation spike, rent/income direction undecided
(Clean Slate milestone vs. third mod).

## Open process question

Tickets should live somewhere tools can create/track them and tie directly to PRs. Not yet
resolved which system — flagged in conversation, not blocking the docs/spec work above.
