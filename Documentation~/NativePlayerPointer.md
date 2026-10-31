# Native Player pointer

`player/focus-window` requests foreground activation of that same verified Player
before a visible input test. It reports previous/current native foreground
windows and the actual readback. A Windows rejection fails explicitly; it does
not send Alt keys, attach input threads, change topmost state or activate another
application. Focus and pointer tools share the process lease/identity owner.

Use the typed `player/window-pointer` project tool for Windows desktop pointer
inspection, movement and a complete left click on one exact running Player.
Supply the executable path, PID and round-trip UTC OS creation time returned by
`player/launch`. Actions are `Inspect`, `Move` and `Click`; coordinates are desktop
pixels with a top-left origin, including negative monitor coordinates.

Move inserts one native absolute SendInput motion event across the virtual desktop,
targeting the requested pixel center. This reaches raw mouse-input consumers as
well as moving the desktop cursor; SetCursorPos alone does not establish that input.
The desktop axis must contain 1..65536 pixels and the target must be inside it.
Move reports one inserted event and the actual cursor readback. Allow the Player to consume that movement before
Inspect or Click. Inspect returns the actual Win32 window/process at the queried
point, the Player's extended window style and the actual cursor position. Click
requires the cursor already at that point, an unheld left button, an opaque native
Player window at the point and the verified Player as the actual receiver. It
sends one down/up pair; it does not click another application or focus a window.
No command opens a window, changes a save or retains a held input across calls.
Use the game's runtime CLI observations to establish what the received click did.

The shared process-identity owner rejects a mismatched executable or OS creation
time before native interaction. Player quit uses the same owner and now reports
`player_identity_mismatch` for that shared rejection. Existing shutdown admission,
process lease and durable exit observation are unchanged.

Static Cost Ledger (before executable write): each request owns one Process lease,
one main-window identity check, one Win32 point query and at most one single-event
motion or two-event click SendInput call. Motion adds four native virtual-desktop
metric reads and constant integer coordinate conversion. A partially inserted down event owns one explicit
release attempt before reporting the native failure and release count. No
enumeration, loop, asynchronous work, cache,
Editor-window creation or process creation. INPUT[2] plus scalar native structs
and typed result fit under 32 KiB managed transient budget; all handles are scoped
to Execute. CPU budget is constant native calls on the existing main thread,
with no sleep or blocking observation. Pass. Native acceptance must compare the
real Player's UI state and window receiver after allowing its normal frame update;
an injected UI Toolkit event alone does not establish desktop routing.

Focus adds one foreground-window read before and after one SetForegroundWindow
call, with the same single process lease and main-window identity. The shared
identity request removes duplicate path/start-time validation from consumers.
No loop, delay, cache or retained resource; <32 KiB constant transient budget.
No additional Player build or scene is created by a focus request. Pass.

## Complete drag and double click

Actions also include DoubleClick and Drag. Both retain the same verified opaque receiver and unheld-button admission. DoubleClick inserts two complete down/up pairs inside one call. Drag requires both endX and endY, validates destination before pressing, then sends20 interpolated absolute motions at20ms intervals and releases in finally. Neither returns a held-button lease. Final cursor coordinates are read again; receiver fields describe the admitted start, and actual UI effects require later Player observation. A rejected native event fails explicitly.

Extension cost: at most22 single-event sends per Drag,20 fixed sleeps totaling400ms, four metric reads and constant scalar/INPUT[1] temporary allocation per step (<32KiB overall). No unbounded loops, background tasks or retained handles. DoubleClick admits at most4 inserted events. The existing Editor main-thread invocation is blocked for the bounded400ms while the separate Player consumes native events. Native execution remains required qualification.
