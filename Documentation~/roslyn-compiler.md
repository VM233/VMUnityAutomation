# Roslyn compiler ownership

Project tools that need C# syntax or semantic analysis obtain the compiler assemblies from
`VmAutomationEditorCommands.TryGetRoslynAssemblies(out csharpAssembly, out coreAssembly)`.
The returned pair belongs to one Editor assembly domain. Consumers use the public Roslyn
contracts and own their finite source, metadata and analysis products.

Policy review and dynamic compilation share parse-option construction in this
compiler owner. It selects the published compiler's declared Preview mode where
available, otherwise its declared Latest mode. Some supported older Editor
compilers predate the Preview enum member. Selection happens before the native
API call; there is no failed parse, catch-and-retry or replacement compiler.
The selection adds one enum metadata lookup and one parse-options object per
parse, with no source scan, retained state or additional assembly load.

Both consumers default to the active Editor compilation context, including Unity's
version/platform symbols and project scripting symbols, directly from
`EditorUserBuildSettings.activeScriptCompilationDefines`. Conditional code that
is inactive in this context remains inactive; this is not a claim to inspect
every platform or supported Unity version in one pass. In particular, an
`#if UNITY_EDITOR` body must participate in structural policy review.

Before implementation: admit at most2048 native symbols and65536 combined
characters per parse, rejecting excess before constructing Roslyn options. The
4096-file policy limit therefore adds at most8,388,608 symbol-length visits and
268,435,456 symbol characters to the existing bounded parse workload, with no
additional source read. One native symbol array and one immutable options value
are live per parse, at most128 KiB character payload plus2048 references; there
is no symbol cache or global settings mutation. Three focused cases use the
existing single small fixture file: Editor-active structure, a native version
symbol, and an inactive Player branch. Shared-option readback must match the
native symbol sequence. PASS for these bounded additions.

Cooperative main-thread consumers use `PublishRoslynAssemblies()` and advance its
enumerator once per native compiler stage. Each yielded value is the actual core,
then C# assembly. Exhaust the enumerator before reading the committed pair through
`TryGetRoslynAssemblies`. A cold Unity 6000.6+ publication performs one native
assembly load per advance, followed by a managed commit; disposal before completion
does not publish a partial compiler pair. Repeated publications yield the same two
domain-owned assemblies. Synchronous consumers exhaust this same producer.
`TryGetPublishedRoslynAssemblies` observes only the committed pair and never loads
an assembly; a canceled core-only publication remains unpublished.

Unity 6000.6+ uses the installed Editor's `Tools/BuildPipeline/Unity.Analyzers.Common`
.NET Standard compiler. The platform data root comes directly from `EditorApplication.applicationContentsPath`.
Both assemblies load through `UnityEngine.Assemblies.CurrentAssemblies.LoadFromPath`.
Unity owns their dependency binding and native BCL retargeting. The factory does not load
standalone System.Memory, Immutable, Unsafe or Metadata copies. Assembly-version equality
alone cannot establish Span signature compatibility with Unity's loaded BCL.
Loading failures propagate before the compiler pair is published.
The API Updater's .NET Core compiler and Pipeline's private renamed compiler are not
semantic-analysis dependencies. Older supported Editors retain their existing factory branch.

The compiler factory does not create another transport or parse sources through an alternate
implementation. Native code-policy review and project semantic reviews share this owner.

Unexpected project-tool errors retain the root cause in `exceptionType` and `stackTrace`,
and the complete nested context in `exceptionContext`. Expected typed project-tool rejections
retain their own stable error code and details.

The compiler factory performs two native assembly loads once per domain. The installed
4.10 pair contains 12,896,280 bytes; no source or metadata product is retained by the factory.
Incremental publication adds two yielded assembly references and a constant-size
iterator, with no file search or source scan on Unity 6000.6+. Unity's native loader
remains on the main thread. Older supported Editors use their existing compiler
discovery branch before yielding the pair; their compilation contract is unchanged.
The focused metadata fixture admits one core-library reference, no source trees and one
assembly-symbol query. Project semantic consumers declare their separate finite ledgers.


## Explicit policy compilation context

code/policy-review additionally accepts an explicit preprocessorSymbols array
from an actual compilation product, so a Player-only body can be inspected
without changing Editor or Player Settings. It publishes the exact symbol set
and its editor/explicit source. A pass covers that declared context; other
conditional variants need their own actual context.

Before executable changes: at most2048 symbols/65536 combined characters, and
1..128 characters per explicitly supplied symbol. The policy constructs one
immutable options value per review, not per source file; the existing4096-file
and4MiB-per-file parse domains stay unchanged. Explicit input adds one bounded
array copy and one immutable options value, below256KiB scratch; output contains
that same bounded symbol array. There is no additional syntax pass, file scan,
assembly load, cache or global setting mutation. Four focused context/input
controls use one task-owned GUID file under Temp and delete it in teardown;
fixture sources never enter Unity import/compilation. PASS.
