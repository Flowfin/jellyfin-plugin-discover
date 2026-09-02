# A block the mainline is already wrong about

This page exists so that the arm which refuses nothing can be watched refusing
nothing. The check is `tools/documented-commands/run.sh` and what it does is in
that file's header.

The block below names `origin/master` and agrees with neither of the two
commits it is read against.

    git cat-file -t origin/master
    tag

A branch meeting that is either behind a mainline somebody else made red or
standing on one that is red already, and in both cases the change being pushed
is not what made it wrong. Refusing here would refuse a branch cut before
somebody else moved a quoted line, which is the failure that gets a check turned
off, so the block is reported and passed over instead.

Nothing here is documentation of the plugin. This page is not read by the
ordinary run, which excludes this directory.
