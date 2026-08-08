# What a metadata source's API offers

A page here records what a source's own API documentation says it will do: the
request budget, the parameters this plugin has to send, and the behaviour the
documentation declines to state. It is written so that an issue deciding
something against a source is deciding it against a reading somebody made on a
day rather than against a memory.

This is a different document from the one in [`docs/sources`](../sources), and
they are kept apart on purpose. A terms page turns a source's terms of use into
obligations on this plugin's behaviour, and `source-terms` refuses one that does
not say when its terms were read. An API reading is not a terms reading, so a
page put in that directory would carry a `Terms read:` line to satisfy a check
rather than because anybody read terms.

Nothing here is enforced. No check reads a page in this directory, no check
requires one to exist, and no check compares what a page says against what the
code sends. A page here is worth what its reader can re-derive from the URLs and
the date on it, which is why both are on every value.

## What a page carries

The day it was read, at the top, and the URL beside every value taken from a
page. A value with no URL next to it did not come from the source.

What was looked for and not found, written as not found. A short page that
records only what it found reads as a complete API, and the gaps are what the
issues waiting on the page most often need.

Nothing verified against a live response unless it says so. Documentation is a
claim a source makes about itself, and this project has no route to check one
today.
