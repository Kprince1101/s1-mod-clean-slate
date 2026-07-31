# Plan

Read `AGENTS.md` first — it has the non-negotiables (no S1API, middleware-interface-only
game access, no narrative comments, ticket-per-change, PR workflow, beta-only). This doc is
the repo-wide roadmap; per-mod/per-feature specs live in `docs/`.

This repo is a monorepo of independently-shippable mods — see `AGENTS.md` for the
OTC/OTCLoader-style structure. Two mods so far: `CleanSlate/` and `GRQD/`.

## Concept

**Clean Slate:** a storefront system. Dealers work a zone (roughly one per zone) to legally
sell product. Businesses the player owns generate income and cost rent weekly. Full spec:
`docs/clean-slate-spec.md`.

**GRQD (Global Real Quick Delivery):** a van service with up to 5 independently-scheduled
routes, moving owned product from a source locker/shelf to a vanilla dock (or the storefront,
with Clean Slate), pay collected at each route's first stop. Van-only — no driver NPC, the
van moves on its own. Fully standalone — no dependency on Clean Slate. Full spec:
`docs/grqd-spec.md`.

Build order: **GRQD first, and it's not just about de-risking shared
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

### GRQD

#### GRQD0 — Foundation ✅

Minimal MelonLoader Il2Cpp plugin scaffold in `GRQD/`, same pattern as Clean
Slate's M0. Proves the second mod builds and loads independently in the monorepo.

#### GRQD1 — Own-layer vs. S1API decision ✅

Decided, not just recommended: no bake-off. OTC's crash chain traced into `PatchS1APIBugs` —
the *currently maintained* S1API fork ships buggy custom-NPC code that needed runtime IL
patches, and the patcher itself crashed inside Mono.Cecil. Own layer only. See "Foundation
Decision" in `docs/grqd-spec.md`.

#### GRQD2 — Build (per the spec's build order)

0. Van spawn/color (`LegionCore.Api.Vehicles.SpawnVan`, real color persistence via
   `SendOwnedColor`) and the GRQD Delivery-app tile (`LegionCore.Api.Delivery.RegisterShopTile`,
   fixed to inject into `_shopElements`, not just `deliveryShops`) — ✅ moved into `LegionCore`,
   both bugs from the original `Middleware/` draft fixed using the real decompiled source.
   **Not yet re-verified in-game post-move.** Ordering (`CanOrder`/`SubmitOrder`) is still
   vanilla vendor-purchase logic, blocked via a Harmony prefix until step 5 redirects it.
1. Docks — custom dock via `ClassInjector`, fixed test position.
2. Storage transfer — locker-to-locker transfer helper.
3. Navigate wrapper over `VehicleAgent.Navigate`, added to `LegionCore`'s vehicle domain.
4. Integration — dock → transfer → van pickup → transfer as one tested loop, in-game on
   Vortex.
5. Route UX — up to 5 routes, pay-locker assignment, status, and redirecting GRQD's cloned
   shop's `CanOrder`/`SubmitOrder` into its own route logic (needs a synthetic `ShopInterface`
   — see `docs/shared-middleware-architecture.md` §6). `IConfigurable`'s enum is confirmed
   closed (15 vanilla values, no mod slot) — default plan is to bypass `ManagementInterface`
   for this, not patch the enum.
6. Ship — Thunderstore, IP-safe logo pass, photosensitivity note if applicable.

Not yet broken into tickets.

### Clean Slate

#### M0 — Foundation ✅

Minimal MelonLoader Il2Cpp plugin proving build → deploy → MelonLoader load → the middleware
interface reaching into the game, end to end. Confirmed in-game via a real notification sent
through the middleware (now `LegionCore`, moved from Clean Slate's own `Middleware/` folder
once the shared-middleware architecture landed — see `docs/shared-middleware-architecture.md`).

#### M1 — Delivery/dock spike ✅

Static feasibility check against the actual game assemblies: can we place a custom dock,
move product between storage locations programmatically, spawn/route a vehicle to a custom
point. All three came back feasible. See `docs/delivery-dock-spec.md`.

#### M2 — Storefront core (in progress, first build step underway)

From the spec's original "Storefront core" docket: the physical storefront building,
ownership/buy-unlock flow, on-site product storage. This is Clean Slate's real starting
point now that delivery is GRQD's job, not a separate mechanic Clean Slate builds itself.

- Building construction: primitive-based, own code in `LegionCore/Buildings/` — no S1API
  (`AGENTS.md`'s existing rule; see `docs/clean-slate-spec.md`'s "Storefront Site" section for
  how this got confirmed via checking how OTC's Big Dispensary is actually built). Shell
  (`StorefrontFactory`: foundation, walls with front + east door gaps, tinted-glass window
  overlays, flat roof), terrain site prep (`TerrainSitePrep`: tree clear + flatten, west/left
  side untouched per the sewer entrance there), and a cosmetic parking pad
  (`ParkingPadFactory`) on the east/right side are all wired into `CleanSlate/Plugin.cs`,
  spawning at the candidate lot's real corner readings.
- First report back was "there is no building" despite a clean success log — prime suspect was
  a shader-stripping issue `PrimitiveBuilder.cs` fixed (forces `Shader.Find("Sprites/
  Default")`, same fix VanLivery already needed for the van decals). **Confirmed visible
  in-game** by a follow-up screenshot, but that same shader renders the shell translucent/
  "ghostly" (it's a transparent-blend shader, wrong fit for solid architecture) — real fix
  needs a genuinely opaque shader name pulled from `ApiSurfaceDump`'s output, not chosen
  in-repo, per `AGENTS.md`'s "no guessing."
- Two more real bugs found and fixed from that same report ("mesh isnt aligned... still too
  high" + a log showing `ApiSurfaceDump` crashing before writing its file): (1) the shell's
  spawn origin was using an unzeroed, sloped-ground Y reading instead of street level — fixed
  in `CleanSlate/Plugin.cs` (reuse `flatLeft` for the spawn position, not raw
  `StorefrontLotLeft`); (2) `ApiSurfaceDump.cs` now wraps every individual reflected member
  access in its own try/catch so one bad `TerrainData` property (a `TypeLoadException` on its
  `DetailPrototype[]` property) can't wipe the whole report before the file gets written.
  Neither re-verified in-game yet — needs a rebuild + relaunch.
- **Still open, not guessed at:** the alignment complaint itself. See
  `docs/clean-slate-spec.md`'s "Storefront Site" section — the `Left`/`Right` corner readings
  used for facing rotation don't line up well with the spec's separate "front, middle-ish"
  reading (~7-8m off in Z, over a meter off in Y), so those readings may not actually sit on
  the sidewalk-facing edge the rotation math assumes. Needs confirmation or a fresh pair of
  readings taken at that edge before trusting the facing rotation.
- Also open: `Terrain.terrainData` showed up `null` once even after the game reported ready —
  treated as a timing issue, not an API question; `Plugin.OnUpdate` now waits up to ~300
  frames for it to become non-null before spawning. Not yet re-verified.
- Deferred to later M2 passes: a functional door object, on-site product storage,
  ownership/buy-unlock flow.
- Delivery *into* the storefront's on-site storage is a GRQD concern (a route's finish can
  target the storefront, per the GRQD spec) — Clean Slate doesn't build its own
  delivery/dock logic. `docs/delivery-dock-spec.md`'s API findings (dock placement, storage
  transfer, vehicle spawn) live in GRQD2's build, not here.
- Not yet broken into tickets.

#### M3 — Dealer-as-counter loop

Dealer assigned to a storefront counter; customer walk-in → order → fetch from on-site
storage → deliver → collect cash; diverting a dealer's assigned customers from street deals
to the storefront. **Correction:** this is controlling/redirecting *existing* vanilla dealer
NPCs the player already hires — not creating new custom NPCs. No NPC-creation spike needed
here after all; the risky OTC-crashing capability (spawning brand-new custom NPCs) doesn't
apply to M3. Dealer-behavior modification specifically is proven working by an existing mod
(High Baller, referenced in the GRQD spec's prior-art note) — still needs its own
design pass for the storefront-specific behavior (redirect, walk-to-storage, counter
interaction), just not a from-scratch NPC-creation spike.

#### Rent/income — likely extracted to the third mod, not built as Clean Slate's own milestone

Was M4 ("Business economy"). No vanilla rent/passive-income system exists to hook into
(confirmed in the M1 spike) — new middleware either way. Currently leaning toward building
this as the separate QoL mod noted under Concept above, generic to any owned property
rather than storefront-specific, but not decided. If Clean Slate ships before that mod
exists, this reverts to a Clean Slate milestone.

## Status

GRQD: GRQD0/GRQD1 done, GRQD2 in progress — van spawn/color and Delivery-app listing
moved into `LegionCore` with both known bugs fixed, not yet re-verified in-game; rest of the
build order not started.
Clean Slate: M0/M1 done (M1 fed into GRQD, not independent CS work anymore), M2 (storefront
core) in progress — first primitive-built shell wired in, not yet verified in-game — M3 needs
a design pass (behavior on existing NPCs, not NPC creation), rent/income direction undecided
(Clean Slate milestone vs. third mod).

## Open process question

Tickets should live somewhere tools can create/track them and tie directly to PRs. Not yet
resolved which system — flagged in conversation, not blocking the docs/spec work above.
