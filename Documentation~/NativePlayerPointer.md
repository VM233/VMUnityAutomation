# Native Player pointer

Use the typed `player/window-pointer` project tool for Windows desktop pointer
inspection, movement and a complete left click on one exact running Player.
Supply the executable path, PID and round-trip UTC OS creation time returned by
`player/launch`. Actions are `Inspect`, `Move` and `Click`; coordinates are desktop
pixels with a top-left origin, including negative monitor coordinates.

Move changes the desktop cursor. Allow the Player to consume that movement before
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
one main-window identity check, one Win32 point query and at most one cursor move
or one two-event SendInput call. A partially inserted down event owns one explicit
release attempt before reporting the native failure and release count. No
enumeration, loop, asynchronous work, cache,
Editor-window creation or process creation. INPUT[2] plus scalar native structs
and typed result fit under 32 KiB managed transient budget; all handles are scoped
to Execute. CPU budget is constant native calls on the existing main thread,
with no sleep or blocking observation. Pass. Native acceptance must compare the
real Player's UI state and window receiver after allowing its normal frame update;
an injected UI Toolkit event alone does not establish desktop routing.
