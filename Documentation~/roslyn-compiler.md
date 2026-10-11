# Roslyn compiler ownership

Project tools that need C# syntax or semantic analysis obtain the compiler assemblies from
`VmAutomationEditorCommands.TryGetRoslynAssemblies(out csharpAssembly, out coreAssembly)`.
The returned pair belongs to one Editor assembly domain. Consumers use the public Roslyn
contracts and own their finite source, metadata and analysis products.

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
The focused metadata fixture admits one core-library reference, no source trees and one
assembly-symbol query. Project semantic consumers declare their separate finite ledgers.
