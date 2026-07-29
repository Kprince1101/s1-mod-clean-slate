# AGENTS.md

Rules for anyone (human or AI) working in this repo. Read this before touching code.

## Non-negotiables

- No S1API dependency, forked or vendored, from either lineage (KaBooMa's original or the
  ifBars fork). We don't wrap a wrapper.
- All game access goes through `CleanSlate/Middleware/` — "the middleware interface." It's
  our own thin layer directly over the auto-generated `Il2CppScheduleOne.*` interop stubs
  (regenerated locally by MelonLoader's Il2CppAssemblyGenerator every launch) plus Harmony.
  Grow it one file at a time, only for what a feature actually needs. Never add speculative
  surface area.
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
  `CleanSlate/LocalPaths.targets.example` for the local build setup — real builds and
  in-game verification happen on the dev's machine, not in CI/sandbox.

## Architecture reference

- `CleanSlate/Plugin.cs` — MelonMod entry point.
- `CleanSlate/Middleware/` — the middleware interface. See rule above.
- `PLAN.md` — feature roadmap and milestones.
- `docs/` — per-feature specs, written before build work starts on that feature.
