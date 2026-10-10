# Native UI Toolkit input

`uitoolkit/runtime-pointer` publishes a native pointer phase or one short Click to
an attached runtime UIDocument. Use its exact `instanceId` from
`uitoolkit/runtime-documents`, Down, Move, Up or Click, and panel coordinates from
runtime-query world bounds. Click sends Down followed by Up within one invocation;
Up is paired in `finally` through the original panel. Events have no explicit
target: Unity's dispatcher owns capture routing and the current panel hit test.
Separate Down and Up requests remain appropriate for an intentional hold. Unity
owns Clickable and Slider behavior. Inspect the product's state after dispatch to
verify its effect. The Editor may be paused.

This entry stimulates the UI Toolkit layer. Device Simulator touch publication and
Input System player-buffer consumption remain separately observable through their
own commands. It does not invoke callbacks or assign control values.

`uitoolkit/runtime-keyboard` requires the exact `documentInstanceId` and an `events`
array of 1 through 64 entries. Every entry requires `phase` (Down or Up), `keyCode`
(a named Unity KeyCode), `character` (empty or one UTF-16 code unit), and `modifiers`
(a unique array of named non-None EventModifiers; an empty array means None).
All keys and modifiers come from the current Unity API enums. The input contract
validates the complete batch before any event. Each event then goes through the
selected root's panel with no explicit target. Unity's dispatcher selects the
current leaf focus. The public focus can be a retargeted composite control, so it
is used only for exact-document admission and response observations. The command
does not focus an element or assign a field value. A preceding native pointer
Click can focus the intended integer input.

For integer entry, send an A Down/Up pair with `["Control"]`, digit Down/Up pairs
with their characters and no modifiers, and Return Down/Up. These are native
KeyDownEvent/KeyUpEvent publications; TextField and SliderInt own selection, editing
and commit. Unicode code units can reach native text editing, but this route does
not model an IME composition session.

Focus and capture can change as legitimate native responses. Every subsequent
event rechecks attachment, public focus and native mouse capture. Missing or
foreign focus, or capture owned outside the exact document, fails at that event
with `eventIndex` and `dispatchedEvents`; already dispatched effects remain.
This immediate input sequence is not an atomic or rollback transaction. Success
reports the event count, initial and final focus and current frame. Product state
and ChangeEvent effects still require separate observations.

When changing a built-in route, run `Documentation~/generate-route-contracts.py
--write` and review its contract and audited fingerprint changes together. The
runtime provider rejects a route manifest whose fingerprint was not regenerated.

Pointer admission keeps the root bounds and initial Pick requirement, then rejects
capture owned outside the exact root with `ui_pointer_target_mismatch`. A capture
inside the selected root can receive Move and Up over a peer element; the initial
Pick in the response remains a coordinate observation, not a dispatch target.
Keyboard capture outside the exact root reports `ui_keyboard_capture_mismatch`.
The remaining attachment, bounds and focus errors are published in the catalog.
Builder preview similarly reports when its native Fit control is absent.

## Static Cost Ledger

Before executable writes for 0.6.197: pointer resolves one native object identity,
performs one initial panel Pick and reads mouse capture once. Admission performs
at most two root-containment checks. Click pools, sends and disposes two events
on the Editor main thread; other phases send one event. Native dispatch owns any
additional capture and hit-test work, with no tool-side target compensation. No
Asset scan, retained event, subscription or frame pump is introduced. Result:
eight scalars below 1 KiB; pass.

Keyboard freezes a maximum of 64 events. The verified Unity 6000.6.4f1 API has
339 KeyCode names and seven non-None modifier names. Each schema build consumes
the native enum definitions; the invocation
parses at most 64 x 7 = 448 modifier entries and owns one immutable native-input
array, below 8 KiB. Dispatch performs at most 64 focus reads, 64 capture reads,
128 root-containment checks and 64 pooled key events, with one event alive at a
time. No keys, held state, focus cache, background job or additional rendering
frames persist. Native
control callbacks own any game effects. Configuration of the 64-event bound or
the native enum set invalidates this ledger. Result: pass.

Focused fixtures admit fourteen schema batches, each capped by the shared
65,536-unit validator budget (917,504 total worst-case units across the fixture
set). Seven attached-panel fixtures yield a fixed maximum of nineteen Editor
frames; they publish at most eighteen pointer events (including four Click pairs)
and fourteen keyboard events. The two UIDocument fixtures create three documents
in isolated preview scenes and verify preservation of the user's active scene,
dirty state and scene count. The foreign-capture fixture asserts nonoverlapping
roots and matching initial Picks before four phase rejections and one keyboard
rejection. A separate fixture checks capture changing after the first key and
reports the partial boundary. Same-root capture is exercised by Move and Up over
a peer; the capture owner receives both and releases while the peer is untouched.
The existing pointer fixture remains the control for separate phases and drag.
The SliderInt fixture observes native text-input key and ChangeEvent publication,
then the SliderInt ChangeEvent and final value. No fixture delays native editing.
No timed frame polling, retained input or additional transport is introduced.
Result: pass for these frozen input bounds. Native execution remains a separate
acceptance gate. The APIs retain the declared Unity 2021.3 surface.

UI Builder preview activates its native Fit viewport button with one
NavigationSubmitEvent and verifies the resulting document bounds against the
visible viewport. Runtime pointer dispatch remains a separate input contract.
One named native query, one pooled event and eight scalar comparisons occur once
per preview. No zoom reflection, repeated fitting or alternative document is
introduced.
