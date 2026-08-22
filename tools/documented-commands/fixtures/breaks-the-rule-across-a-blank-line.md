# A block that stops at the blank line in its own output

This page exists so that the check beside it can be watched refusing an output
that was pasted only as far as its first blank line. That is the near miss for
the fixture beside it: the command prints three lines, the page carries the
first, and the two that follow the blank are gone. The check is
`tools/documented-commands/run.sh` and what it does is in that file's header.

    git show HEAD:tools/documented-commands/fixtures/breaks-the-rule-across-a-blank-line.md | sed -n '1,3p'
    # A block that stops at the blank line in its own output

What is dropped here is exactly what a page loses when whatever reads it treats
a blank line as the end of an output rather than as part of one, so a page that
is right and a page that is short have to be told apart.

Nothing here is documentation of the plugin. The pages in this directory are not
read by the ordinary run, which excludes it.
