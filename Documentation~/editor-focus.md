# Editor focus

`editor/clear-focus` removes keyboard focus from the calling Windows Editor
thread using native `SetFocus(NULL)`. It does not activate another application,
select a tab, open or close a window, or modify Unity's private focus state.
The result reports the previous/current native keyboard window and Unity's
actual `EditorWindow.focusedWindow` identity. Success requires both current
identities to be empty. A remaining identity is `focus_clear_unconfirmed`;
unsupported platforms return `platform_not_supported`.

Use this mutation only when the intended state has no focused EditorWindow,
for example restoring an observed no-focus checkpoint after an authorized
interaction. It is separate from screenshot capture. Native `view` capture
reads an already selected host view without selecting or focusing it.

## Static Cost Ledger before implementation

The operation performs two native keyboard-focus reads, one native focus
mutation, and two Unity focused-window reads on the Editor main thread. It has
no loop, traversal, polling, retained state, asset access or window creation.
At most four identity strings and one result dictionary with at most eight entries are
allocated (under 4 KiB); work and space are O(1). The native focus transition
is synchronous and its actual result is checked once, with no retry. PASS.

Contract tests inspect one fixed empty input schema, one fixed result schema
and the route's declared effect. They neither create windows nor change focus.
Native acceptance uses the existing Editor and compares both focus owners,
the existing window inventory and the user's view/preference checkpoint.
