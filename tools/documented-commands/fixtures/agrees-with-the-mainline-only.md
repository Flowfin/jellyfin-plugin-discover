# A block that is right about the mainline and wrong about this checkout

This page exists so that the two arms the check takes off the mainline can be
watched rather than asserted. The check is `tools/documented-commands/run.sh`
and what it does is in that file's header.

The block below names `origin/master`, so off the mainline it is read twice:
once against the commit the page describes and once against the tree being
pushed. Which of the two answers it still agrees with is the whole of the
decision, and this page agrees with a commit and not with a tree.

    git cat-file -t origin/master
    commit

The two commits the check reads it against are handed in rather than fetched,
because the run that judges the mainline has one commit checked out and no
second tree to differ against. What it is handed here is this checkout's own
head and that head's tree, which every checkout carries, name different objects
and answer this command differently. Read the other way round, the same page is
a change that repairs a block the mainline is already wrong about, which is the
other arm and the one that keeps the check from refusing honest work.

Nothing here is documentation of the plugin. This page is not read by the
ordinary run, which excludes this directory.
