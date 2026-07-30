# AGENTS.md

Rules for anyone (human or AI) working in this repo. Read this before touching code.

## Non-negotiables

- No S1API dependency, forked or vendored, from either lineage (KaBooMa's original or the
  ifBars fork). We don't wrap a wrapper. This applies to every mod in this repo, no
  exceptions, no "just for comparison" builds that leave S1API in a real project's dependency
  tree.
- All game access goes through that mod's own `Middleware/` folder — "the middleware
  interface." It's our own thin layer directly over the auto-generated
  `Il2CppScheduleOne.*` interop stubs (regenerated locally by MelonLoader's
  Il2CppAssemblyGenerator every launch) plus Harmony. Grow it one file at a time, only for
  what that mod's features actually need. Never add speculative surface area. Each mod owns
  its own middleware — don't reach into another mod's `Middleware/` folder; factor shared
  code out explicitly if duplication becomes a real problem, don't default to sharing.
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
- `<ModName>/Middleware/` — that mod's middleware interface. See rule above.
- `LocalPaths.targets.example` — shared local build config template (every mod builds
  against the same game install).
- `PLAN.md` — feature roadmap and milestones, repo-wide.
- `docs/` — specs, one per mod/feature, written before build work starts on that feature.

Current mods: `CleanSlate/` (storefront/economy), `DeliveryDriver/` (logistics NPC, "Fry").
