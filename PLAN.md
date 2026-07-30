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

**Delivery Driver (Global Real Quick Delivery / GRQD, driver "Fry"):** a van service that
runs repeatable routes moving owned product between the player's properties on a schedule,
paid daily from a designated locker. v1 is van-only — no physical Fry NPC spawn, he texts
the player via an avatar instead (see spec for why that's deferred). Standalone — works with
vanilla, doesn't require Clean Slate — but optionally supplies Clean Slate's storefront
storage when both are installed. Full spec: `docs/delivery-driver-spec.md`.

Build order: **Delivery Driver first.** It de-risks custom dock placement, storage-to-storage
transfer, and vehicle spawn/navigation — capabilities Clean Slate's own delivery work (M2
below) will reuse directly (`StorageTransfer` is explicitly built to be shared). It does
**not** de-risk custom NPC creation — v1 has no physical NPC spawn, so that question is still
open and separate (relevant to M3, dealers, which do need a physical NPC at a counter).

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
5. Route UX — GRQD phone app page (teal branding, up to 5 routes, pay locker, status).
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

#### M2 — Delivery mechanic (build)

Likely builds on Delivery Driver's DD2 output (vehicle spawn/navigate, product movement)
rather than duplicating it — needs revisiting once DD2 exists, not fully speced as
independent work anymore.

- Custom dock component, storage-to-storage transfer, vehicle spawn+navigate — see
  `docs/delivery-dock-spec.md` for the API surface already identified.
- Decision needed: van-only vs. a driver NPC (may just be "reuse Fry").

#### M3 — Dealer storefronts

NPC dealer per zone, selling product on the player's behalf. Not yet speced — needs its own
design pass, especially NPC creation. Delivery Driver v1 does **not** de-risk this (it's
van-only, no physical NPC spawn) — custom NPC creation is still an open, unspiked question,
and it's the exact capability that crashed OTC. Spike this before designing dealer behavior
further, same treatment M1 gave the delivery mechanic.

#### M4 — Business economy

Owned businesses generate passive income, cost rent weekly. No vanilla rent/passive-income
system exists to hook into (confirmed in the M1 spike) — this is new middleware. Needs an
income-model decision (per-item-sold vs. flat) before build.

## Status

Delivery Driver: DD0/DD1 done, DD2 not started (6-step build order in the spec).
Clean Slate: M0/M1 done, M2 depends on DD2 landing first, M3 needs its own NPC-creation
spike before it can be speced further, M4 unspeced.

## Open process question

Tickets should live somewhere tools can create/track them and tie directly to PRs. Not yet
resolved which system — flagged in conversation, not blocking the docs/spec work above.
