# Player display settings

The typed `player/display-settings` tool reads or configures Unity's native
PlayerSettings display properties. Discover its current command and schema through
the bounded catalog. It owns default orientation, standalone reference dimensions
and the four autorotation flags; identity and build settings keep their existing
owners.

`State` reads the native values without writing. `Configure` requires at least one
field, a stable Editor outside Play Mode, positive dimensions and a defined native
orientation. All input validation finishes before assigning any property. Omitted
fields retain their current native values. The result reads PlayerSettings after
the native save, rather than echoing the request.

Default settings do not prove that a running Simulator or device adopted them.
Re-enter Play Mode and inspect the actual display and UI for runtime acceptance.

## Static Cost Ledger

There are no data-dependent loops, scans, caches or temporary objects. A request
reads seven scalar native settings, configures at most seven, saves once, and reads
seven scalar settings for the completion snapshot. Work is synchronous on the
Editor main thread with constant memory. Result: pass.
