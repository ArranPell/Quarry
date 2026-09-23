# Contributing

Quarry's product rule is **bounded sessions, not completeness**. It shows a small, capped list of
achievements you can finish where you are, rather than everything you haven't finished. I'll decline
features that would grow that list without bound: removing the cap, listing all incomplete achievements,
or adding a browse view whose point is to be exhaustive. They may be good ideas elsewhere; here they work
against what the module is for.

Bug fixes, small guidance and UI improvements, and data-accuracy reports are all welcome.

## Before opening something

- **Found a bug?** Use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.yml).
- **Data looks wrong** (wrong objective, wrong location, or the API ticks the wrong one)? Use the
  [data error template](.github/ISSUE_TEMPLATE/data_error.yml). Most of Quarry's data comes from the
  Guild Wars 2 Wiki, and the template helps separate wiki errors from Quarry bugs.
- **Have an idea or a question?** Post it in
  [Discussions → Suggestions](https://github.com/ArranPell/Quarry/discussions/categories/suggestions).
  Issues here are for bugs and data errors. If your idea removes a cap or grows a list past one, read
  the product rule above first.

## Pull requests

Keep PRs small, one change each. Match the existing style: `this.`-prefixed members, services behind
interfaces, wired through the DI container. If a change touches data handling (`AchievementService`, bit
alignment, persistence), explain your reasoning in the PR description. The wiki and the API list some
achievements' objectives in different orders, and bit alignment and a small override table correct for
it. `persistanceStorage.json` keeps its misspelling so existing installs still load.
