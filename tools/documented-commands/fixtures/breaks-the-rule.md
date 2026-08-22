# A block that breaks the rule

This page exists so that the check beside it can be watched refusing rather than
asserted to refuse. The check is `tools/documented-commands/run.sh` and what it
does is in that file's header.

The command below reads this page's own path and the output pasted under it is
the other fixture's, which is the shape a page takes on the day the thing it
quotes has moved and nobody re-ran the command.

    git ls-files -- tools/documented-commands/fixtures/breaks-the-rule.md
    tools/documented-commands/fixtures/holds-the-rule.md

The difference is one word rather than a missing block, because a page that
stops printing anything is the easy case and a page printing something adjacent
is the one that gets believed.

Nothing here is documentation of the plugin. Neither of these two pages is read
by the ordinary run, which excludes this directory.
