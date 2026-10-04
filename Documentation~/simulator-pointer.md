# Device Simulator pointer input

`simulator/pointer` sends a pooled native UI Toolkit mouse event to the Device
Simulator's hit-tested DeviceView. Unity's own TouchEventManipulator then owns
screen transforms, cutout rejection and touch publication. This tests the Simulator
input path rather than invoking a game callback or injecting a desktop mouse device.
The hit-tested element is assigned to the event target before panel dispatch.

Read the exact contract from the catalog. Supply the existing Simulator window's
`instanceId` from `uitoolkit/windows` or `uitoolkit/query`, a Down, Move or Up phase,
and x/y in UI Toolkit window points with the origin at the top left. Down and Up
are separate invocations so the running UI consumes distinct touch phases.

The Editor must be playing and unpaused. The window must be the native Device
Simulator and the hit target must be its DeviceView. Invalid points, other windows
and inactive or changing Editor states fail before dispatch. The result identifies
the native target and dispatched phase, together with Unity's own transformed
touch position, screen admission and active touch state, application focus and
frame number. These observations distinguish dispatch from native consumption;
inspect the game's actual state or UI to prove the interaction.

## Static Cost Ledger

The required window identity uses one native object lookup, with no window or Asset
scan. One native panel Pick owns hit testing against the current Editor UI tree;
there is no duplicated traversal. Each invocation pools, sends and disposes one
mouse event from one Unity Event value, on the Editor main thread. No cache,
retained event, callback or per-frame work is added. Native observation reads the
existing Simulator main and touch owner once and three fixed touch members; it does not
scan a hierarchy or publish an input event itself. The result contains eleven
scalar fields, under 1 KiB. Input size is four scalar fields. Result: pass.

## Input System state

`input/runtime-state` observes the installed Input System's current state buffer,
device admission, game focus and selected asset actions. It is available with
Unity 6 and Input System 1.11 or later. Application focus and Input System game
focus are distinct observations. The response names the current update buffer;
Editor-buffer control values do not prove a player update consumed an event.

### Static Cost Ledger

The request selects one action asset and at most sixteen action names. The native
device count is limited to thirty-two; a larger inventory is rejected before
traversal rather than returning a partial observation. Each device contributes
seven metadata fields and at most six primary-touch values. Each selected action
uses one native indexed lookup and contributes six scalar observations. Native
focus observation requires two exact reflection members, without type scans.
At most thirty-two devices and sixteen actions are visited on the Editor main
thread, with one result array each and no callbacks, state mutation, cache or
retained input. Output stays below 16 KiB. Result: pass.
