# A block whose pasted output carries a blank line

This page exists so that the check beside it can be watched staying silent on a
block whose output has a blank line in the middle of it, which is what most real
command output looks like. The check is `tools/documented-commands/run.sh` and
what it does is in that file's header.

The command below reads this page's own first three lines, so what it prints
does not move when anything else in the tree does, and the second of the three
is empty:

    git show HEAD:tools/documented-commands/fixtures/holds-the-rule-across-a-blank-line.md | sed -n '1,3p'
    # A block whose pasted output carries a blank line

    This page exists so that the check beside it can be watched staying silent on a

Two blocks a blank line apart are two blocks and not one, and the pair below is
here because a reader that carries blank lines through has to stop somewhere. If
it stopped at the wrong place the second command would be read as the first
one's output, and a block reported as transcribing a session is a block nobody
compared:

    git ls-files -- tools/documented-commands/fixtures/breaks-the-rule.md
    tools/documented-commands/fixtures/breaks-the-rule.md

    git ls-files -- tools/documented-commands/fixtures/holds-the-rule.md
    tools/documented-commands/fixtures/holds-the-rule.md

Nothing here is documentation of the plugin. The pages in this directory are not
read by the ordinary run, which excludes it.
