# Native UI Toolkit input

`uitoolkit/editor-pointer` targets one exact existing EditorWindow identity from
`uitoolkit/windows`. It publishes Down, Move, Up or Click at native panel
coordinates from `uitoolkit/query`. It does not create, focus or replace a window,
invoke a callback or assign a control value. Native controls own any resulting
Editor or asset changes. Read the actual resulting state and pixels separately.
Initial Down/Click requires a hit inside that exact root; owned capture admits
Move/Up outside its bounds. Foreign capture rejects admission. A short Click
shares runtime-pointer's pooled native Down/fresh-Pick Up owner. Intentional
holds use separate phases and must end with Up or native root retirement.

Static Cost Ledger before implementation: one exact object lookup, one capture
read, one native Pick and at most two native containment checks admit the root.
Click owns at most two pooled events, other phases one. One post-dispatch capture
read and root attachment observation qualify retirement. No window search,
subscription, retained event, timer, Asset scan or native focus request is added.
The output has at most eight bounded scalar observations plus native type/name.
Three native fixtures create at most one32-element Editor window each and yield
at most two layout frames per fixture. They cover real Button/Slider input,
captured outside Move/Up and wrong-object/outside admission. Schema controls
exercise the exact typed descriptor before input. Existing runtime pointer
fixtures remain the shared dispatch controls; no clock or assertion changes.
Require native effect/capture results and the actual authoring-window request
after publication/adoption. A leaked hold, wrong-window input or missing native
effect rejects this method and stops it for renewed investigation.

The first exact native capture fixture is rejected before Down because its empty
EditorWindow root is not a pickable surface. Native authoring readback also shows
EditorWindow roots use PickingMode.Ignore; the tool must retain that rejection.
Unity documents Ignore as preventing pointer picking:
https://docs.unity3d.com/kr/6000.0/ScriptReference/UIElements.PickingMode.Ignore.html
The existing native Button/Slider fixture is the positive pickable-child control.
Correct the capture fixture by adding one real Position-pickable child and assert
its exact native Pick before Down. Keep root bubbling/capture and the outside
Move/Up assertions. Reset only pre-stimulus diagnostic counters after the same
two layout frames. No production dispatch, admission or budget changes. The
fixture adds one element, one Pick and at most three events, with no polling.
Require one Down, one captured outside Move, one Up and no retained capture; any
missed native event or leaked capture stops this correction for investigation.

## Editor keyboard admission

The current formal catalog exposes native runtime keyboard input only; authoring
TextField entry cannot target an EditorWindow. Add editor-keyboard beside the
existing runtime operation, reusing its exact bounded key-event schema, parser
and native focus/capture dispatcher. Unity routes keyboard events to current
focus, so the operation must never focus a field or assign its value:
https://docs.unity3d.com/cn/2023.2/Manual/UIE-Events-Dispatching.html
Admit one exact existing attached EditorWindow identity, then validate the whole
1..64-event batch before input. Recheck exact-root focus and capture for each
event. Native controls own Editor/asset effects; no window, event, subscription
or held key persists. A native pointer Click supplies focus separately.

Static Cost Ledger: reuse the runtime64-event/448-modifier maximum and existing
validation budget. One object lookup, at most64 attachment/focus/capture reads,
128 root-containment checks and64 pooled key events occur synchronously. No
source/Asset scan, control search, extra layout frame or retry is added. A native
TextField fixture owns one window/two fields and two initial layout frames. It
uses native Click followed by A/CtrlA/B key pairs, observes ChangeEvent and final
text, and verifies wrong-object/no-focus rejection before event publication.
Schema controls reject65 events, multi-character input and invalid keys before
input. Require the real graph filter/local-review entry after adoption. Any
wrong-root input, absent native change or retained capture rejects the method.

Native multi-contact input uses [runtime-touch](runtime-touch.md), with one or
two finger samples per frame and exact document/capture ownership.

`uitoolkit/runtime-pointer` publishes a native pointer phase or one short Click to
an attached runtime UIDocument. Use its exact `instanceId` from
`uitoolkit/runtime-documents`, Down, Move, Up or Click, and panel coordinates from
runtime-query world bounds. Click sends Down followed by Up within one invocation;
Up is paired in `finally` through a fresh native Pick on the original panel,
matching a separate Up request after any Down-driven hierarchy change. Events have no explicit
target: Unity's dispatcher owns capture routing and the current panel hit test.
Separate Down and Up requests remain appropriate for an intentional hold. Unity
owns Clickable and Slider behavior. Inspect the product's state after dispatch to
verify its effect. The Editor may be paused.

The compound Click has the same release receiver and local coordinates as the
separate event control for custom capture owners that stop Down propagation.
It also releases through the current attached target when Down removes its source.

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

Pointer admission rejects capture owned outside the exact root with
`ui_pointer_target_mismatch`. Down/Click, and Move/Up without capture, require an
inside point whose native Pick belongs to that exact root. When the selected root
owns native capture, Move/Up may cross its bounds or pass over another document.
Unity's dispatcher delivers those events to the capture owner and permits its
normal release. The response Pick remains a coordinate observation; its name and
type are empty when there is no native hit. It is never an assigned event target.
Keyboard capture outside the exact root reports `ui_keyboard_capture_mismatch`.
The remaining attachment, bounds and focus errors are published in the catalog.
Builder preview similarly reports when its native Fit control is absent.

## Static Cost Ledger

Before executable writes for 0.6.251: pointer admission reads native capture once,
performs one panel Pick and at most two exact-root containment checks. Owned
capture admits Move/Up across root bounds and peer documents; Down/Click retain
the original hit admission. Dispatch still publishes one pooled event per phase
and two for Click, with no explicit target, retained state or frame pump. The
additional native fixture uses two documents in one isolated preview scene,
two fixed Editor frames and five pointer requests (four native events): one rejected initial Down,
one admitted Down, two captured Moves and one captured Up. It checks a peer hit
and an outside-root point, then retires all fixture resources. No input-dependent
loop or Asset scan is added; result and allocation bounds remain unchanged. Pass.

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
