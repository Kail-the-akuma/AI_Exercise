# Engineering baseline (applies to all projects)

Generic engineering practice — portable to any repo, any language, any
collaborator. Not a personal style choice; see `personal-preferences.md`
for one person's own taste (language, formatting, etc.) layered on top of
this, if they choose to install it.

## Git & GitHub hygiene

- **Protected branches (`main`/`master`) are PR-gated.** Open a PR and stop
  there — leave it for a human to review and merge themselves. Never merge
  or push directly on someone's behalf, no matter how the request is
  phrased ("merge this", "let's merge with main" describe the desired end
  state, not permission to skip the review window). Non-protected working
  branches stay normal push targets, no PR required.
- **Issue/ticket hygiene**: use the tracker's native type and priority
  fields when it has them, and native parent-child/sub-issue links instead
  of a prose cross-reference. Close an issue only after the fix is
  verified (by the user, or by something objectively confirming it, like a
  clean test run), with a comment linking the commit — tell the user
  before/as doing it, never silently.

## Review & audit epistemic discipline

Four concrete failure modes to guard against whenever synthesizing a code
review, audit, or any multi-step research task:

1. **Don't assert a domain convention or fact without checking an
   authoritative source first.** Reasoning from pattern-matching over the
   code/data alone, instead of checking the actual rule/spec/doc that
   governs it, produces confident-sounding wrong answers.
2. **When multiple passes or sub-agents touch the same underlying fact,
   actively adopt whichever one actually resolved it** — never default to
   the earlier, more cautious-sounding, or first-written claim just
   because it sounds safer. A hedge ("must verify elsewhere") is a pointer
   to go check the other passes' output, not a final answer to preserve if
   a later pass already resolved it.
3. **Re-verify citations every time a finding is restated or
   reformatted** — line numbers and references drift, and confidence
   should only ever go down or stay the same as a finding moves further
   from the original source, never up.
4. **A negative search result is not proof of absence.** "I searched and
   found nothing" is weaker evidence than it feels like — corroborate a
   second way (broader scope, different phrasing, re-reading the file
   directly) before writing a confident "X doesn't exist" claim into any
   persisted artifact.

## Report / finding-writing discipline

- Never split findings into a "fix first" tier vs. an appendix/lesser
  tier — present one flat, complete list (grouped by scope/theme if
  useful, never by manufactured importance).
- Confidence is a required, explicit rating (e.g. High/Medium/Low)
  alongside Severity, not just hedging language buried in prose.
- Never show a hardcoded secret/credential value in full in any report or
  persisted artifact — mask it, showing only the last 3 characters.
- Never drop a finding just because it "works in context" or seems
  explainable — keep it, with lower confidence/severity, flagged for human
  review. Only exclude something entirely if it turns out to be a genuine
  tooling/rendering artifact, not because it seems contextually justified.

## Verification discipline

Never trust a single automated validation/linting/build-check tool's
self-reported success as sufficient evidence a change works. Always also
run the real build/test/run step, and where practical, actually exercise
the resulting code path — a clean type-check or a green "Success: true"
is not the same as verified behavior.

## Engineering judgment

- Minimize third-party dependencies; prefer what the platform/framework
  already provides.
- When porting or refactoring existing logic, prefer faithful behavior
  preservation over unrequested "cleanup" — apparent quirks may have been
  hardened against real-world data through prior iteration. If you do
  deliberately deviate (e.g. removing genuinely dead code), document the
  reasoning inline at the deviation site.
- Comments explain *why*, not *what* — a hidden constraint, a workaround,
  a non-obvious invariant. Skip comments that just restate what
  well-named code already shows.

## Roadmap / milestone hygiene

Only list real, scheduled, actionable work as a milestone/roadmap item.
Passive or environment-dependent preconditions ("this resolves itself once
infra changes") and low-priority open curiosities belong in context/risk
notes, not as work items — they dilute a roadmap otherwise.
