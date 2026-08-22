# A block that holds the rule

This page exists so that the check beside it can be watched staying silent on a
block that is right, next to the one it is watched refusing. The check is
`tools/documented-commands/run.sh` and what it does is in that file's header.

The command below reads this page's own path, so what it prints does not move
when anything else in the tree does.

    git ls-files -- tools/documented-commands/fixtures/holds-the-rule.md
    tools/documented-commands/fixtures/holds-the-rule.md

Nothing here is documentation of the plugin. Neither of these two pages is read
by the ordinary run, which excludes this directory, because a fixture that
breaks the rule on purpose would otherwise redden every run.
