# A page that names this repository under the account it left

This file exists to be refused. It is the one place in the tree where the
superseded address is written on purpose, and the runner excludes the fixture
tree from the leg that reads the tracked tree, which is why it can sit here
without reddening the run it proves.

The plain link, which is what fifty nine of the sixty eight occurrences were:

    https://github.com/iderex/jellyfin-plugin-discover/issues/227

A recorded command, which is the shape that needs re-running before it moves
rather than rewriting in place:

    gh api repos/iderex/jellyfin-plugin-discover --jq .default_branch

A bare repository name with no scheme in front of it, because a rule anchored on
`github.com` would miss this one:

    gh repo view iderex/jellyfin-plugin-discover --json defaultBranchRef

`tools/invariants/rules/no-old-owner-for-this-repository.rule` says what the
refusal is for and what it cannot see.
