# Runtime keyboard input

`input/runtime-keyboard` sends one explicit chord to the enabled project-wide
Input System action through a temporary Keyboard. It observes the real action's
performed callback; it never invokes the callback or changes a gameplay property.
Use the exact `map/action` path or action GUID and one to four Key enum names.
The normal gameplay and save effects of every enabled matching binding apply.

The synchronous owner releases all keys, removes its Keyboard and restores the
previous current Keyboard, input update/focus routing and Editor pause state in
`finally`. It never sends OS input or changes assets/bindings/action enable state.
The result reports actual callback count, game frame interval and restoration.
After the call, observe the product through its ordinary read contracts and
advance native frames when its own UI lifecycle requires them.

## Static cost ledger

Admission accepts 1..4 unique valid keys; parsing uses at most four enum parses
and six pair checks. Native dispatch is exactly three input updates: released,
chord, released. One actual action has one temporary observer, one Keyboard is
created and removed, and no game frame is requested. There is no asset scan,
cache, retry, subscription beyond the invocation or cross-call state. Native
Input System and game callbacks retain their own costs and normal semantics.
Temporary allocation is one four-key array, one Keyboard and one result. PASS
for the bounded chord domain. Input remains serialized on Unity's Editor thread.

Validation must cover invalid/duplicate keys without mutation, actual performed
callbacks, repeated toggles, released final controls and exact device/settings/
pause restoration. Typed unit cases do not replace official executor admission
and actual production-state readback.
