# Runtime UI Toolkit touch

`uitoolkit/runtime-touch` publishes one or two native touch samples during one
Editor invocation to an exact attached runtime UIDocument. Use panel coordinates
from runtime-query and native finger slots 0 through 9. Each sample declares Down,
Move, Up or Cancel. The command validates the complete sample domain before any
event, then rechecks the exact document and native capture/hit ownership for each
dispatch. Captured continuation may leave the root. Unity's dispatcher owns the
event target, pointer capture and normal event propagation.

This stimulates UI Toolkit without injecting an Input System device, invoking
callbacks or assigning product values. It does not establish phone-device input
acceptance. Read product scale/orbit/selection and inspect actual frames separately.
The caller pairs admitted Down with Up/Cancel, or observes the production root's
retirement releasing capture. The tool retains no contacts or resources. A later
sample can fail after earlier native callbacks changed the UI; this is an immediate
sequence with no rollback, and the error identifies its sample index.

## Static Cost Ledger

Before executable writes: at most two samples are validated and dispatched, with
one object resolution, at most two panel Picks, two capture reads and four root
containment checks per sample. Final observation reads ten native capture slots.
One pooled event is alive at a time; no arrays beyond the two-sample input and
ten-integer result, retained events, tasks, subscriptions or frame pumps. The
native fixture uses two fixed frames and two root elements, with at most eight
events and one rejected foreign/outside sample per case. No Cartesian product,
Asset scan or timing loop is introduced. Scratch and response remain below 4 KiB.
Unity 2021.3 pointer capture and pooled Touch APIs are the supported surface. Pass.
