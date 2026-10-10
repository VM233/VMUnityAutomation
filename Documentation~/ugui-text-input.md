# Focused UGUI text input

`ui/type-text` types through the active InputField's native `ProcessEvent` key
handling, rather than changing its display Text. First enter Play Mode and click
the InputField with `simulate_pointer` down/up. Pass the full hierarchy `path`
and a `text` string (up to 4096 UTF-16 characters) to `vm_auto_ui_type_text`.
CRLF and CR are normalized to LF. The field owns multiline policy, character
limit, validation, selection replacement and onValueChanged events.

The command requires an active, interactable, focused UGUI InputField. It returns
the resulting text, focus state and count of processed input characters. A field
can reject characters according to its own policy. This command does not emulate
an operating system IME or send Input System text events, which the legacy UGUI
InputField does not consume. Focus behavior and synthetic device setup in a
headless test remain the test environment's responsibility.
