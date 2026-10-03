# Device Simulator pointer input

`simulator/pointer` sends a pooled native UI Toolkit mouse event to the Device
Simulator's hit-tested DeviceView. Unity's own TouchEventManipulator then owns
screen transforms, cutout rejection and touch publication. This tests the Simulator
input path rather than invoking a game callback or injecting a desktop mouse device.

Read the exact contract from the catalog. Supply the existing Simulator window's
`instanceId` from `uitoolkit/windows` or `uitoolkit/query`, a Down, Move or Up phase,
and x/y in UI Toolkit window points with the origin at the top left. Down and Up
are separate invocations so the running UI consumes distinct touch phases.

The Editor must be playing and unpaused. The window must be the native Device
Simulator and the hit target must be its DeviceView. Invalid points, other windows
and inactive or changing Editor states fail before dispatch. The result identifies
the native target and dispatched phase; inspect the game's actual state or UI to
prove the resulting interaction.

## Static Cost Ledger

The required window identity uses one native object lookup, with no window or Asset
scan. One native panel Pick owns hit testing against the current Editor UI tree;
there is no duplicated traversal. Each invocation pools, sends and disposes one
mouse event from one Unity Event value, on the Editor main thread. No cache, retained event, callback or
per-frame work is added. Input size is four scalar fields. Result: pass.
