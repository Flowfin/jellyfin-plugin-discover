# Security policy

## Reporting

Report privately, through GitHub's private vulnerability reporting on this
repository, at
https://github.com/Flowfin/jellyfin-plugin-discover/security/advisories/new.

It is switched on, which is a property of the repository rather than of this
file, so it is read rather than asserted:

    gh api repos/Flowfin/jellyfin-plugin-discover/private-vulnerability-reporting
    {"enabled":true}

Do not open a public issue for something that lets one user reach another user's
data, reveals a credential, or reaches a network the server operator did not
intend. Everything else belongs in the tracker where more people can see it.

If that route is unavailable to you, say so in a public issue with no detail
beyond the fact that you have something to report, and a private route will be
arranged.

## What to expect

This is one person's project rather than a staffed product, so the honest numbers
are short and unambitious. Expect an acknowledgement that the report was read
within seven days. Expect a first assessment, meaning whether it is accepted and
roughly what it affects, within thirty days.

No fix time is promised, because a promise nobody is on call to keep is worse
than an absent one. What is promised is that you will be told which of the three
happened: fixed, accepted and not being fixed, or not accepted, with the reason.

You will be credited in the advisory unless you ask not to be.

## What is in scope

The plugin in this repository. That includes what it puts in the library
database, what it stores on the server, what it sends to a metadata source, and
anything a server operator's credential touches.

Not in scope, and worth stating so a report is not spent on them:

- The Jellyfin server itself. Report those to the Jellyfin project.
- A metadata source's own service.
- Findings from a scanner with no path to an effect on a server. Those are
  welcome as ordinary issues.

## Which versions are supported

No policy exists, and there is now something for one to be about.

THIS SECTION ANSWERED "None", on the ground that nothing had been published, so
there was no released version to fix and no user to protect. `0.1.0.0-stable` was
published on 2026-09-04 and that ground is gone. Read on 2026-09-05:

    gh api repos/Flowfin/jellyfin-plugin-discover/releases --jq 'length'
    1

One released version exists, and anybody who installed it is somebody this page
is addressed to. What is supported and for how long is
[#125](https://github.com/Flowfin/jellyfin-plugin-discover/issues/125), and the
release-readiness pass is
[#123](https://github.com/Flowfin/jellyfin-plugin-discover/issues/123). Until one
of those lands there is a released version and no commitment about it, and
reading this section as a promise to fix `0.1.0.0` would be reading a policy
nobody has written.

## What already runs against every change

The checks in `.github/workflows/` include a code scanner, a workflow audit, a
dependency review against the advisory database and a supply-chain self-audit,
and the restore fails on a known-vulnerable package rather than warning. The set
is not listed here because a list drifts against the directory that decides it:

    ls .github/workflows

None of that is a substitute for a report. A scanner finds the shapes somebody
has already written a rule for.
