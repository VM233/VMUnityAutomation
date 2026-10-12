# USS declaration editing

`uitoolkit/edit-uss` uses the same rule and declaration parser as the authoring
audits. A declaration edit changes its value range, preserving adjacent comments,
declaration order, rule spacing and line endings. Removing a declaration removes
every occurrence of that property. Upserting a selector replaces its entire body;
comments before the selector remain outside the replacement.

Quoted delimiters and comment contents are lexical data, rather than rule or
declaration separators. Discover the exact operation schema through the catalog.

## Static cost ledger

The frozen integration input has at most 7,200 characters, 63 rules and 256
characters per rule header, with two operations. Comment masking, rule and
declaration scans and the line-number cursor are monotone; direct character
visits stay below 32 times the source length, or 460,800 across both operations.
Six existing header metadata regexes have a conservative header-squared bound,
below 49,545,216 comparisons. The integration budget is 60 million comparisons.
Twelve focused cases each use at most 4 KiB, three rules and three operations:
below 4,718,592 direct visits and 42,467,328 header comparisons, within a
50 million comparison budget. Parsing
retains source ranges and masks; the integration allocation budget is 4 MiB, with
no asset search, Cartesian product, background task, timer or retained cache.
Only a changed file invokes its existing native synchronous import. PASS for the
frozen integration and regression inputs; this ledger is not a new public file
size limit.
