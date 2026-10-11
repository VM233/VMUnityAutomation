# Roslyn compiler ownership

Project tools that need C# syntax or semantic analysis obtain the compiler assemblies from
`VmAutomationEditorCommands.TryGetRoslynAssemblies(out csharpAssembly, out coreAssembly)`.
The returned pair belongs to one Editor assembly domain. Consumers use the public Roslyn
contracts and own their finite source, metadata and analysis products.

Unity 6000.6+ uses the installed Editor's `Tools/BuildPipeline/Unity.Analyzers.Common`
.NET Standard compiler. The platform data root comes directly from `EditorApplication.applicationContentsPath`. Loading failures propagate before the pair is published.
The API Updater's .NET Core compiler and Pipeline's private renamed compiler are not
semantic-analysis dependencies. Older supported Editors retain their existing factory branch.

The compiler factory does not create another transport or parse sources through an alternate
implementation. Native code-policy review and project semantic reviews share this owner.
