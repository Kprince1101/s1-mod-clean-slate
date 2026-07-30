# AGENTS.md

Rules for anyone (human or AI) working in this repo. Read this before touching code.

## Non-negotiables

- No S1API dependency, forked or vendored, from either lineage (KaBooMa's original or the
  ifBars fork). We don't wrap a wrapper. This applies to every mod in this repo, no
  exceptions, no "just for comparison" builds that leave S1API in a real project's dependency
  tree.
- All game access goes through `LegionCore/` — one shared middleware project, not per-mod.
  It's our own thin layer directly over the auto-generated `Il2CppScheduleOne.*` interop
  stubs (regenerated locally by MelonLoader's Il2CppAssemblyGenerator every launch) plus
  Harmony, built from the real decompiled Mono-branch source (kept locally under
  `reference/`, gitignored, never committed). Facade (`LegionCore.Api`) + Factory pattern:
  each domain (`Vehicles`, `Delivery`, `Npcs`, `Configurables`, `Save`, `Notifications`) is
  an interface + implementation behind `Api`, and anything that *creates* a game object goes
  through a `*Factory` class so a game update only breaks one file, not every call site. Both
  mods depend on `LegionCore` via `ProjectReference` and never touch `Il2CppScheduleOne.*`
  directly. Grow it one domain at a time, for what a mod's features actually need — no
  per-mod `Middleware/` folders anymore. Every game update: run `tools/mw-update-check/`
  (see `docs/shared-middleware-architecture.md`), fix what it flags, then manually update
  both mods to match — no auto-migration. A mod still loads on an unexpected game version; it
  fires a non-blocking in-game alert instead (`LegionCore.Api.CheckVersion()`).
- Beta branch only. No stable-branch compatibility work, no hedging the build for both.
- No narrative/diary-style comments. A comment says what isn't obvious from the code, in one
  line, or it doesn't exist. No "why we did this historically," no restating the code in
  prose.
- No guessing. If a fact only the repo owner has (game version, in-game behavior, what a
  vanilla system does, which branch/design direction to take), ask. Don't assume and build on
  the assumption.
- Plan before large or ambiguous changes. State the plan, get a yes, then execute.

## Workflow

- Every change ties to a ticket. No ticket, no work — file one first if the task doesn't have
  one yet.
- All work happens on a branch and lands via PR. No direct pushes to `main`, including small
  fixes.
- Build target is `net6.0` (matches MelonLoader's host runtime). See
  `LocalPaths.targets.example` (repo root) for the local build setup — real builds and
  in-game verification happen on the dev's machine, not in CI/sandbox.

## Architecture reference

This is a monorepo of separate, independently-shippable mods — same pattern as
OTC/OTCLoader: each mod is its own project, its own DLL, its own Thunderstore listing, and
none of them require each other to function (optional integration between mods is fine, a
hard dependency is not).

- `<ModName>/Plugin.cs` — that mod's MelonMod entry point.
- `LegionCore/` — the shared middleware interface. See rule above. Both mods reference it;
  neither mod has its own `Middleware/` folder.
- `tools/mw-update-check/` — dev-only tool that diffs `LegionCore`'s watched game members
  across game versions after an update.
- `LocalPaths.targets.example` — shared local build config template (every mod builds
  against the same game install).
- `docs/PLAN.md` — feature roadmap and milestones, repo-wide.
- `docs/shared-middleware-architecture.md` — `LegionCore`'s design (locked).
- `docs/` — specs, one per mod/feature, written before build work starts on that feature.

Current mods: `CleanSlate/` (storefront/economy), `GRQD/` (van logistics, van-only, no driver
NPC).
