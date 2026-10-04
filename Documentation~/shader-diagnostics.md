# Shader compiler diagnostics

The typed request limits returned diagnostics to 100 by default, at most 200.
The result includes the native diagnostic count and explicit truncation.

Use the catalog's `shader/diagnostics` typed tool with an exact imported Shader
asset path. It supports hand-written shaders and compiled Shader Graph assets.
The result carries Unity's native support and error state plus compiler message
severity, source file, line and target platform.

`Shader.isSupported` can remain true while a particular pass has compiler
errors. An empty C# compiler report or Console query does not validate a shader.
The tool reads `ShaderUtil.GetShaderMessages` and `ShaderHasError`; it does
not trigger compilation, clear diagnostics or substitute the Console.
Import a changed shader through the normal asset owner, exercise its real
rendering consumer, then inspect the same shader again.
