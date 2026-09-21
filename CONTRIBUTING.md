# Contributing

Quarry's product rule is **bounded sessions, not completeness**: it surfaces a small, capped list of
achievements you can finish where you are, not everything you haven't finished. Features that would grow
that list without bound — removing the cap, listing all incomplete achievements, adding an unfiltered
browse view where the point is to be exhaustive — will be declined, not because they're bad ideas in
general, but because they work against what this module is for.

Bug fixes, small guidance/UI improvements, and data-accuracy reports are all welcome.

## Before opening something

- **Found a bug?** Use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.yml).
- **Data looks wrong** (wrong objective, wrong location, wrong bit)? Use the
  [data error template](.github/ISSUE_TEMPLATE/data_error.yml) — most of Quarry's data comes from the
  Guild Wars 2 Wiki, so this helps separate wiki errors from Quarry bugs.
- **Have an idea or a question?** There's no dedicated template for this yet, and Discussions is off, so
  open an issue with whichever of the two templates is the closer fit and explain in the body that it's a
  proposal, not a bug or a data error — GitHub requires picking one since blank issues are disabled. See
  the product rule above before proposing anything that removes or grows past a cap.

## Pull requests

Keep PRs small and scoped to one change. Match the existing style (`this.`-prefixed members, services
behind interfaces, wired through the DI container). If a change touches data handling
(`AchievementService`, bit alignment, persistence), explain the reasoning in the PR description — those
paths have a few non-obvious constraints that aren't visible from the diff alone.
