# A block this check refuses to run

This page exists so that the refusal to run a command carrying a shell
redirection can be watched rather than asserted. The check is
`tools/documented-commands/run.sh` and what it does is in that file's header.

The block below would print nothing if it were run, because its output goes to
the bit bucket, so a check that ran it would report a difference against the
line pasted under it. A check that refuses it prints the reason and stays
silent, which is what the leg beside this fixture asserts.

    git ls-files -- tools/documented-commands/fixtures/refuses-to-run.md > /dev/null
    tools/documented-commands/fixtures/refuses-to-run.md

The redirection here is harmless on purpose. What it stands in for is the one
that is not: a block writing a file somewhere, which is a shape this corpus
already carries and which no page author writes expecting a check to run it.

Nothing here is documentation of the plugin. The pages in this directory are not
read by the ordinary run, which excludes it.
