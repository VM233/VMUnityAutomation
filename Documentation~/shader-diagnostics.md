# Shader compiler diagnostics

The typed request limits returned diagnostics to 100 by default, at most 200.
The result includes the native diagnostic count and explicit truncation.

Use the catalog's `shader/diagnostics` typed tool with an exact imported Shader
or ComputeShader asset path. It supports hand-written shaders, compiled Shader
Graph assets and compute kernels. The result identifies `assetType` as `shader`
or `computeShader`, and retains compiler message severity, source file, line
and target platform.

`Shader.isSupported` can remain true while a particular pass has compiler
errors. An empty C# compiler report or Console query does not validate a shader.
For Shader assets, the tool reads `ShaderUtil.GetShaderMessages`,
`ShaderHasError` and the native `Shader.isSupported` flag. For ComputeShader
assets, it reads `ShaderUtil.GetComputeShaderMessages`; `hasErrors` reflects
the error severities in that native product. ComputeShader exposes no native
asset-wide support property, so `isSupported` is null rather than an inferred
device or kernel capability. The tool does not trigger compilation or dispatch,
clear diagnostics or substitute the Console.
Import a changed shader through the normal asset owner, exercise its real
rendering consumer, then inspect the same shader again.
