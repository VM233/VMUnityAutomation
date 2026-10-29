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
An empty diagnostic array before a device-specific kernel has been requested
does not prove that the kernel can load or execute. Focused compute fixtures
request native kernel support before comparing compiler messages; the
diagnostics entry itself remains read-only.

Use the typed `shader/compute-kernel-support` tool to request one exact imported
compute asset and authored kernel on the current graphics device. It reports
the native `ComputeShader.IsSupported` value and, for supported programs, native
thread-group dimensions. Unsupported dimensions are absent. This call may
compile a device-specific program and emit or populate compiler diagnostics;
it does not dispatch, reimport, write Assets or clear messages. Read the same
asset with `shader/diagnostics` afterwards. Native support and clean compiler
messages still do not prove that a real rendering consumer executes correctly.

Use `shader/variant-layout` to compile one exact shader pass, stage, platform,
build target and keyword set through the public `ShaderData` API (Unity 2021.3+).
This is a durable operation in stable Edit Mode. Native compilation is synchronous;
cancellation cannot interrupt its native call. The result reports native success,
program byte count, all-buffer byte sum and bounded top-level constant-buffer
fields. No source, bytecode or recursive struct layout is returned. Total counts
and truncation remain explicit. One request accepts at most 64 distinct keywords
of at most 128 characters and returns at most 128 buffers, 256 fields per buffer
and 200 diagnostics. It never enumerates the shader's variant space, creates a
material, renders a frame or changes the selected build target.

Constant-buffer sizes describe compiled declarations. They do not measure Ring
Buffer allocations, prove that the active draw selected those keywords, or replace
native rendering and GPU timing evidence. The compiled program/cache cost is owned
by Unity; returned metadata is bounded independently of native compiler memory.
