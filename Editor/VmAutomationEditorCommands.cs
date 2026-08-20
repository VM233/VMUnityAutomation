using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationEditorCommands
    {
        public static object GetEditorState()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorIdleSnapshot idleSnapshot = GetEditorIdleSnapshot();
            return new Dictionary<string, object>
            {
                { "isIdle", idleSnapshot.IsIdle },
                { "isPlaying", EditorApplication.isPlaying },
                { "isPaused", EditorApplication.isPaused },
                { "isCompiling", idleSnapshot.IsCompiling },
                { "isUpdating", idleSnapshot.IsUpdating },
                { "isChangingPlayMode", idleSnapshot.IsChangingPlayMode },
                { "isPlayingOrWillChangePlaymode", idleSnapshot.IsPlayingOrWillChangePlaymode },
                { "activeScene", scene.name },
                { "activeScenePath", scene.path },
                { "sceneDirty", scene.isDirty },
                { "unityVersion", Application.unityVersion },
                { "platform", EditorUserBuildSettings.activeBuildTarget.ToString() },
                { "projectPath", Application.dataPath.Replace("/Assets", "") },
            };
        }

        public static void WaitForIdle(Dictionary<string, object> args, Action<object> resolve)
        {
            var normalizedArgs = NormalizeWaitForIdleArguments(args);
            int timeoutMs = (int)normalizedArgs["timeoutMs"];
            int stableFrames = (int)normalizedArgs["stableFrames"];
            int stableMs = (int)normalizedArgs["stableMs"];
            int resumeCount = (int)normalizedArgs["resumeCount"];
            bool hasDeadline = TryGetUtcDateTime(args, "_deadlineUtc", out DateTime deadlineUtc);
            double startTime = EditorApplication.timeSinceStartup;
            double stableStartTime = -1;
            int currentStableFrames = 0;
            List<string> lastBusyReasons = new List<string>();
            bool resolved = false;

            void Resolve(object result)
            {
                if (resolved)
                    return;

                resolved = true;
                resolve(result);
            }

            void Tick()
            {
                var snapshot = GetEditorIdleSnapshot();
                if (snapshot.IsIdle)
                {
                    if (currentStableFrames == 0)
                    {
                        stableStartTime = EditorApplication.timeSinceStartup;
                    }

                    currentStableFrames++;
                }
                else
                {
                    currentStableFrames = 0;
                    stableStartTime = -1;
                    lastBusyReasons = snapshot.BusyReasons;
                }

                double stableDurationMs = stableStartTime >= 0
                    ? (EditorApplication.timeSinceStartup - stableStartTime) * 1000d
                    : 0;
                double elapsedMs = (EditorApplication.timeSinceStartup - startTime) * 1000d;

                if (elapsedMs >= timeoutMs || hasDeadline && DateTime.UtcNow >= deadlineUtc)
                {
                    EditorApplication.update -= Tick;
                    Resolve(BuildIdleResult(false, true, timeoutMs, stableFrames, stableMs,
                        currentStableFrames, stableDurationMs, startTime, snapshot, lastBusyReasons, resumeCount));
                    return;
                }

                if (currentStableFrames >= stableFrames && stableDurationMs >= stableMs)
                {
                    EditorApplication.update -= Tick;
                    Resolve(BuildIdleResult(true, false, timeoutMs, stableFrames, stableMs,
                        currentStableFrames, stableDurationMs, startTime, snapshot, lastBusyReasons, resumeCount));
                    return;
                }
            }

            Tick();
            if (!resolved)
            {
                EditorApplication.update += Tick;
            }
        }

        internal static Dictionary<string, object> NormalizeWaitForIdleArguments(Dictionary<string, object> args)
        {
            return new Dictionary<string, object>
            {
                { "timeoutMs", Math.Max(1, GetInt(args, "timeoutMs", 30000)) },
                { "stableFrames", Math.Max(1, GetInt(args, "stableFrames", 3)) },
                { "stableMs", Math.Max(0, GetInt(args, "stableMs", 500)) },
                { "resumeCount", Math.Max(0, GetInt(args, "_resumeCount", 0)) },
            };
        }

        internal static string BuildWaitForIdleRequestKey(Dictionary<string, object> args)
        {
            var normalized = NormalizeWaitForIdleArguments(args);
            return $"wait/editor-idle|{normalized["timeoutMs"]}|{normalized["stableFrames"]}|{normalized["stableMs"]}";
        }

        public static void SetPlayMode(Dictionary<string, object> args, Action<object> resolve)
        {
            string requestedAction = args != null &&
                                     args.TryGetValue("action", out object requestedActionValue) &&
                                     requestedActionValue != null
                ? requestedActionValue.ToString().Trim().ToLowerInvariant()
                : "play";
            if (requestedAction == "step")
            {
                StepPlayModeFrame(args, resolve);
                return;
            }

            if (!TryResolvePlayModeTarget(args, out string action, out bool targetPlaying,
                    out bool targetPaused, out string validationError))
            {
                resolve(VmAutomationResponse.Error(validationError, "invalid_play_mode_action"));
                return;
            }

            bool initiallyPlaying = EditorApplication.isPlaying;
            bool initiallyPaused = EditorApplication.isPaused;
            if ((action == "pause" || action == "resume") &&
                !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                resolve(VmAutomationResponse.Error($"Cannot {action} because Unity is not in Play Mode.",
                    "play_mode_required"));
                return;
            }

            int timeoutMs = Math.Max(100, GetInt(args, "timeoutMs", 10000));
            int stableFrames = Math.Max(1, GetInt(args, "stableFrames", 2));
            double startedAt = EditorApplication.timeSinceStartup;
            int confirmedFrames = 0;
            bool resolved = false;

            void ApplyTarget()
            {
                if (!targetPlaying)
                {
                    if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                        EditorApplication.isPlaying = false;
                    return;
                }

                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
                if (EditorApplication.isPlaying && EditorApplication.isPaused != targetPaused)
                    EditorApplication.isPaused = targetPaused;
            }

            bool TargetReached()
            {
                bool isChangingPlayMode = EditorApplication.isPlayingOrWillChangePlaymode !=
                                          EditorApplication.isPlaying;
                return targetPlaying
                    ? EditorApplication.isPlaying && !isChangingPlayMode &&
                      EditorApplication.isPaused == targetPaused
                    : !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
            }

            void Complete(object result)
            {
                if (resolved)
                    return;
                resolved = true;
                resolve(result);
            }

            void Tick()
            {
                ApplyTarget();
                if (TargetReached())
                    confirmedFrames++;
                else
                    confirmedFrames = 0;

                double elapsedMs = (EditorApplication.timeSinceStartup - startedAt) * 1000d;
                if (confirmedFrames >= stableFrames)
                {
                    EditorApplication.update -= Tick;
                    Complete(new Dictionary<string, object>
                    {
                        { "success", true },
                        { "action", action },
                        { "stateConfirmed", true },
                        { "isPlaying", EditorApplication.isPlaying },
                        { "isPaused", EditorApplication.isPaused },
                        { "changed", initiallyPlaying != EditorApplication.isPlaying ||
                                     initiallyPaused != EditorApplication.isPaused },
                        { "stableFrames", confirmedFrames },
                        { "elapsedMs", Math.Round(elapsedMs, 1) },
                    });
                    return;
                }

                if (elapsedMs < timeoutMs)
                    return;

                EditorApplication.update -= Tick;
                Complete(VmAutomationResponse.Error(
                    $"Unity did not reach the requested Play Mode state for '{action}' within {timeoutMs} ms.",
                    "play_mode_state_timeout", true, new Dictionary<string, object>
                    {
                        { "action", action },
                        { "targetPlaying", targetPlaying },
                        { "targetPaused", targetPaused },
                        { "isPlaying", EditorApplication.isPlaying },
                        { "isPaused", EditorApplication.isPaused },
                        { "isPlayingOrWillChangePlaymode", EditorApplication.isPlayingOrWillChangePlaymode },
                    }));
            }

            Tick();
            if (!resolved)
                EditorApplication.update += Tick;
        }

        private static void StepPlayModeFrame(Dictionary<string, object> args, Action<object> resolve)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode !=
                EditorApplication.isPlaying)
            {
                resolve(VmAutomationResponse.Error("Cannot step because Unity is not in stable Play Mode.",
                    "play_mode_required"));
                return;
            }

            int timeoutMs = Math.Max(100, GetInt(args, "timeoutMs", 10000));
            int stableFrames = Math.Max(1, GetInt(args, "stableFrames", 1));
            int frameBefore = Time.frameCount;
            bool wasPaused = EditorApplication.isPaused;
            double startedAt = EditorApplication.timeSinceStartup;
            int confirmedFrames = 0;
            bool resolved = false;

            EditorApplication.isPaused = true;
            EditorApplication.Step();

            void Complete(object result)
            {
                if (resolved)
                    return;

                resolved = true;
                resolve(result);
            }

            void Tick()
            {
                bool isChangingPlayMode = EditorApplication.isPlayingOrWillChangePlaymode !=
                                          EditorApplication.isPlaying;
                bool frameAdvanced = Time.frameCount > frameBefore;
                bool targetReached = EditorApplication.isPlaying && !isChangingPlayMode &&
                                     EditorApplication.isPaused && frameAdvanced;
                confirmedFrames = targetReached ? confirmedFrames + 1 : 0;
                double elapsedMs = (EditorApplication.timeSinceStartup - startedAt) * 1000d;

                if (confirmedFrames >= stableFrames)
                {
                    EditorApplication.update -= Tick;
                    Complete(new Dictionary<string, object>
                    {
                        { "success", true },
                        { "action", "step" },
                        { "stateConfirmed", true },
                        { "isPlaying", EditorApplication.isPlaying },
                        { "isPaused", EditorApplication.isPaused },
                        { "wasPaused", wasPaused },
                        { "frameBefore", frameBefore },
                        { "frameAfter", Time.frameCount },
                        { "stableFrames", confirmedFrames },
                        { "elapsedMs", Math.Round(elapsedMs, 1) },
                    });
                    return;
                }

                if (elapsedMs < timeoutMs)
                    return;

                EditorApplication.update -= Tick;
                Complete(VmAutomationResponse.Error(
                    $"Unity did not complete one Play Mode frame step within {timeoutMs} ms.",
                    "play_mode_step_timeout", true, new Dictionary<string, object>
                    {
                        { "action", "step" },
                        { "isPlaying", EditorApplication.isPlaying },
                        { "isPaused", EditorApplication.isPaused },
                        { "frameBefore", frameBefore },
                        { "frameAfter", Time.frameCount },
                    }));
            }

            EditorApplication.update += Tick;
        }

        internal static bool TryResolvePlayModeTarget(Dictionary<string, object> args,
            out string action, out bool targetPlaying, out bool targetPaused, out string error)
        {
            action = args != null && args.TryGetValue("action", out object rawAction) && rawAction != null
                ? rawAction.ToString().Trim().ToLowerInvariant()
                : "play";
            error = null;
            switch (action)
            {
                case "play":
                case "resume":
                    targetPlaying = true;
                    targetPaused = false;
                    return true;
                case "pause":
                    targetPlaying = true;
                    targetPaused = true;
                    return true;
                case "stop":
                    targetPlaying = false;
                    targetPaused = false;
                    return true;
                default:
                    targetPlaying = false;
                    targetPaused = false;
                    error = $"Unknown action: {action}. Use 'play', 'pause', 'resume', 'step', or 'stop'.";
                    return false;
            }
        }

        public static object ExecuteMenuItem(Dictionary<string, object> args)
        {
            string menuPath = args.ContainsKey("menuPath") ? args["menuPath"].ToString() : "";
            if (string.IsNullOrEmpty(menuPath))
                return new { error = "menuPath is required" };

            bool result = EditorApplication.ExecuteMenuItem(menuPath);
            return new { success = result, menuPath };
        }

        // Short temp directory to avoid Windows 260-char path limit
        private static readonly string _shortTempDir = Path.Combine(Path.GetTempPath(), "vma");

        private static string GetShortTempDir()
        {
            if (!Directory.Exists(_shortTempDir))
                Directory.CreateDirectory(_shortTempDir);
            return _shortTempDir;
        }

        // ─── Roslyn via Reflection ───
        // Roslyn types are accessed purely through reflection so that the plugin compiles
        // even when the Microsoft.CodeAnalysis assemblies aren't directly referenced
        // (e.g. Unity 6000.3+ changed how editor assemblies are exposed).

        private static Assembly _roslynCSharpAsm;
        private static Assembly _roslynCoreAsm;
        private static bool _roslynProbed;

        /// <summary>
        /// Try to locate the Roslyn assemblies from the currently loaded AppDomain.
        /// Returns true if both Microsoft.CodeAnalysis.CSharp and Microsoft.CodeAnalysis are found.
        /// </summary>
        private static bool TryLoadRoslyn()
        {
            if (_roslynProbed) return _roslynCSharpAsm != null && _roslynCoreAsm != null;
            _roslynProbed = true;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name;
                if (name == "Microsoft.CodeAnalysis.CSharp") _roslynCSharpAsm = asm;
                else if (name == "Microsoft.CodeAnalysis") _roslynCoreAsm = asm;
            }

            // If not already loaded, try to find and load them from the Unity editor directory
            if (_roslynCSharpAsm == null || _roslynCoreAsm == null)
            {
                // Resolve the editor's "Data" directory across platforms.
                // Windows: <EditorDir>/Data/...
                // macOS:   EditorApplication.applicationPath ends in "Unity.app"; Data lives at Unity.app/Contents
                // Linux:   <EditorDir>/Data/... (same as Windows)
                string appPath = EditorApplication.applicationPath;
                string editorDir = Path.GetDirectoryName(appPath);
                var dataRoots = new List<string>();
                // Windows / Linux layout
                if (!string.IsNullOrEmpty(editorDir))
                    dataRoots.Add(Path.Combine(editorDir, "Data"));
                // macOS layout: Unity.app/Contents
                if (!string.IsNullOrEmpty(appPath) && appPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    dataRoots.Add(Path.Combine(appPath, "Contents"));

                var searchDirs = new List<string>();
                foreach (var data in dataRoots)
                {
                    searchDirs.Add(Path.Combine(data, "Managed"));
                    searchDirs.Add(Path.Combine(data, "Tools", "Roslyn"));
                    // Mono-compatible Roslyn assemblies (preferred for Unity's Mono runtime)
                    searchDirs.Add(Path.Combine(data, "MonoBleedingEdge", "lib", "mono", "4.5"));
                    searchDirs.Add(Path.Combine(data, "MonoBleedingEdge", "lib", "mono", "msbuild", "Current", "bin", "Roslyn"));
                    // ApiUpdater has full Roslyn assemblies
                    searchDirs.Add(Path.Combine(data, "Tools", "BuildPipeline", "Compilation", "ApiUpdater"));
                    // ScriptUpdater also ships Roslyn (Mono-compatible)
                    searchDirs.Add(Path.Combine(data, "Tools", "ScriptUpdater"));
                    // DotNetSdkRoslyn contains .NET Core assemblies — may fail on Mono, tried last
                    searchDirs.Add(Path.Combine(data, "DotNetSdkRoslyn"));
                }
                if (!string.IsNullOrEmpty(editorDir))
                    searchDirs.Add(editorDir);

                foreach (var searchDir in searchDirs)
                {
                    if (!Directory.Exists(searchDir)) continue;
                    if (_roslynCoreAsm == null)
                    {
                        string corePath = Path.Combine(searchDir, "Microsoft.CodeAnalysis.dll");
                        if (File.Exists(corePath))
                        {
                            try { _roslynCoreAsm = Assembly.LoadFrom(corePath); }
                            catch { /* .NET Core assemblies fail on Mono — skip */ }
                        }
                    }
                    if (_roslynCSharpAsm == null)
                    {
                        string csharpPath = Path.Combine(searchDir, "Microsoft.CodeAnalysis.CSharp.dll");
                        if (File.Exists(csharpPath))
                        {
                            try { _roslynCSharpAsm = Assembly.LoadFrom(csharpPath); }
                            catch { /* .NET Core assemblies fail on Mono — skip */ }
                        }
                    }
                }
            }

            return _roslynCSharpAsm != null && _roslynCoreAsm != null;
        }

        /// <summary>
        /// Collect MetadataReference objects for Roslyn from all loaded assemblies (via reflection).
        /// </summary>
        private static object GetMetadataReferencesReflection()
        {
            // MetadataReference.CreateFromFile(string) is in Microsoft.CodeAnalysis
            var metadataRefType = _roslynCoreAsm.GetType("Microsoft.CodeAnalysis.MetadataReference");
            var createFromFile = metadataRefType.GetMethod("CreateFromFile",
                BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(string) }, null);

            // Fallback: find CreateFromFile with string as first param (may have optional params)
            if (createFromFile == null)
            {
                foreach (var m in metadataRefType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "CreateFromFile") continue;
                    var pars = m.GetParameters();
                    if (pars.Length >= 1 && pars[0].ParameterType == typeof(string))
                    {
                        createFromFile = m;
                        break;
                    }
                }
            }

            // We need to find the base type for the list — use the abstract PortableExecutableReference or MetadataReference
            var listType = typeof(List<>).MakeGenericType(metadataRefType);
            var refs = (System.Collections.IList)Activator.CreateInstance(listType);
            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                        continue;
                    if (addedPaths.Contains(assembly.Location))
                        continue;
                    string asmName = assembly.GetName().Name;
                    if (asmName.Contains(".Tests") || asmName.Contains("NUnit") || asmName.Contains("Moq"))
                        continue;

                    addedPaths.Add(assembly.Location);
                    var cfPars = createFromFile.GetParameters();
                    var cfArgs = new object[cfPars.Length];
                    cfArgs[0] = assembly.Location;
                    for (int i = 1; i < cfPars.Length; i++)
                        cfArgs[i] = cfPars[i].HasDefaultValue ? cfPars[i].DefaultValue : null;
                    var metaRef = createFromFile.Invoke(null, cfArgs);
                    refs.Add(metaRef);
                }
                catch { }
            }
            return refs;
        }

        public static object ExecuteCode(Dictionary<string, object> args)
        {
            return VmAutomationPersistentJobRunner.StartExecuteCode(args);
        }

        internal static object ExecuteCodeInline(Dictionary<string, object> args)
        {
            string code = args != null && args.ContainsKey("code") && args["code"] != null
                ? args["code"].ToString()
                : "";
            if (string.IsNullOrEmpty(code))
            {
                return VmAutomationResponse.Error("code is required", "invalid_arguments", false,
                    new Dictionary<string, object>
                    {
                        { "userCodeExecuted", false },
                    });
            }

            if (!TryLoadRoslyn())
            {
                return VmAutomationResponse.Error(
                    "Roslyn (Microsoft.CodeAnalysis) is not available in this Unity version. ExecuteCode requires Roslyn for dynamic compilation.",
                    "execute_code_compiler_unavailable", false, new Dictionary<string, object>
                    {
                        { "userCodeExecuted", false },
                    });
            }

            try
            {
                string fullCode = BuildExecuteCodeSource(args, code, out int generatedPrefixLineCount,
                    out int userCodeLineCount, out string usingError);
                if (usingError.Length > 0)
                {
                    return VmAutomationResponse.Error(usingError, "invalid_arguments", false,
                        new Dictionary<string, object>
                        {
                            { "userCodeExecuted", false },
                        });
                }

                // --- Roslyn-based compilation (via reflection) ---
                // All Roslyn types accessed through reflection to avoid compile-time dependency.
                // Unity 6000+ uses CoreCLR where CodeDom/mcs can't handle netstandard facades.
                // Roslyn resolves type forwarding correctly.

                // Parse with the preview language version supported by Unity's bundled Roslyn.
                // CSharpSyntaxTree.ParseText defaults to an older language version in some
                // Unity releases, and Unity 6.4 still classifies C# 9 pattern syntax such as
                // "is not" as Preview even though the loaded parser supports it.
                var syntaxTreeType = _roslynCSharpAsm.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree");
                var parseOptionsType = _roslynCSharpAsm.GetType("Microsoft.CodeAnalysis.CSharp.CSharpParseOptions");
                var languageVersionType = _roslynCSharpAsm.GetType("Microsoft.CodeAnalysis.CSharp.LanguageVersion");
                object parseOptions = null;
                if (parseOptionsType != null && languageVersionType != null)
                {
                    var defaultOptions = parseOptionsType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)
                        ?.GetValue(null);
                    var withLanguageVersion = parseOptionsType.GetMethod("WithLanguageVersion",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { languageVersionType }, null);
                    if (defaultOptions != null && withLanguageVersion != null)
                    {
                        var previewLanguageVersion = Enum.Parse(languageVersionType, "Preview");
                        parseOptions = withLanguageVersion.Invoke(defaultOptions, new[] { previewLanguageVersion });
                    }
                }

                MethodInfo parseText = null;
                foreach (var method in syntaxTreeType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name != "ParseText")
                        continue;

                    var parameters = method.GetParameters();
                    if (parameters.Length < 1 || parameters[0].ParameterType != typeof(string))
                        continue;

                    if (parseOptions != null && parameters.Any(parameter => parameter.ParameterType == parseOptionsType))
                    {
                        parseText = method;
                        break;
                    }

                    if (parseText == null)
                        parseText = method;
                }

                if (parseText == null)
                    throw new MissingMethodException(syntaxTreeType.FullName, "ParseText");

                // Build argument array matching ParseText signature (fill optional params with defaults)
                object syntaxTree;
                {
                    var pars = parseText.GetParameters();
                    var invokeArgs = new object[pars.Length];
                    invokeArgs[0] = fullCode;
                    for (int i = 1; i < pars.Length; i++)
                    {
                        invokeArgs[i] = parseOptions != null && pars[i].ParameterType == parseOptionsType
                            ? parseOptions
                            : pars[i].HasDefaultValue ? pars[i].DefaultValue : null;
                    }
                    syntaxTree = parseText.Invoke(null, invokeArgs);
                }

                var references = GetMetadataReferencesReflection();

                string tempDir = GetShortTempDir();
                string outputPath = Path.Combine(tempDir, $"vm_automation_dynamic_{Guid.NewGuid():N}.dll");

                // OutputKind.DynamicallyLinkedLibrary
                var outputKindType = _roslynCoreAsm.GetType("Microsoft.CodeAnalysis.OutputKind");
                var dllOutputKind = Enum.Parse(outputKindType, "DynamicallyLinkedLibrary");

                // CSharpCompilationOptions(OutputKind, ...)
                var compilationOptionsType = _roslynCSharpAsm.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions");
                object compilationOptions;
                {
                    // Find constructor: CSharpCompilationOptions(OutputKind outputKind, ...)
                    ConstructorInfo optionsCtor = null;
                    foreach (var ctor in compilationOptionsType.GetConstructors())
                    {
                        var pars = ctor.GetParameters();
                        if (pars.Length >= 1 && pars[0].ParameterType == outputKindType)
                        {
                            optionsCtor = ctor;
                            break;
                        }
                    }
                    var ctorPars = optionsCtor.GetParameters();
                    var ctorArgs = new object[ctorPars.Length];
                    ctorArgs[0] = dllOutputKind;
                    for (int i = 1; i < ctorPars.Length; i++)
                        ctorArgs[i] = ctorPars[i].HasDefaultValue ? ctorPars[i].DefaultValue : null;
                    // Set allowUnsafe if there's such a parameter
                    for (int i = 1; i < ctorPars.Length; i++)
                    {
                        if (ctorPars[i].Name == "allowUnsafe")
                            ctorArgs[i] = true;
                    }
                    compilationOptions = optionsCtor.Invoke(ctorArgs);
                }

                // CSharpCompilation.Create(string, IEnumerable<SyntaxTree>, IEnumerable<MetadataReference>, CSharpCompilationOptions)
                var compilationType = _roslynCSharpAsm.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation");
                MethodInfo createMethod = null;
                foreach (var m in compilationType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "Create") continue;
                    var pars = m.GetParameters();
                    if (pars.Length == 4 && pars[0].ParameterType == typeof(string))
                    {
                        createMethod = m;
                        break;
                    }
                }

                // Wrap syntaxTree in array
                var syntaxTreeBaseType = _roslynCoreAsm.GetType("Microsoft.CodeAnalysis.SyntaxTree");
                var syntaxTreeArray = Array.CreateInstance(syntaxTreeBaseType, 1);
                syntaxTreeArray.SetValue(syntaxTree, 0);

                var compilation = createMethod.Invoke(null, new object[]
                {
                    Path.GetFileNameWithoutExtension(outputPath),
                    syntaxTreeArray,
                    references,
                    compilationOptions
                });

                // compilation.Emit(string outputPath)
                // Use the stream overload: Emit(Stream)
                object emitResult;
                using (var stream = new FileStream(outputPath, FileMode.Create))
                {
                    var emitMethod = compilation.GetType().GetMethod("Emit",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(Stream) }, null);

                    // Fallback: find Emit with Stream as first param
                    if (emitMethod == null)
                    {
                        foreach (var m in compilation.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (m.Name != "Emit") continue;
                            var pars = m.GetParameters();
                            if (pars.Length >= 1 && pars[0].ParameterType == typeof(Stream))
                            {
                                emitMethod = m;
                                break;
                            }
                        }
                    }

                    var emitArgs = new object[emitMethod.GetParameters().Length];
                    emitArgs[0] = stream;
                    for (int i = 1; i < emitArgs.Length; i++)
                    {
                        var p = emitMethod.GetParameters()[i];
                        emitArgs[i] = p.HasDefaultValue ? p.DefaultValue : null;
                    }
                    emitResult = emitMethod.Invoke(compilation, emitArgs);
                }

                // Check emitResult.Success
                bool success = (bool)emitResult.GetType().GetProperty("Success").GetValue(emitResult);

                if (!success)
                {
                    // Get Diagnostics
                    var diagnostics = (System.Collections.IEnumerable)emitResult.GetType()
                        .GetProperty("Diagnostics").GetValue(emitResult);

                    var diagnosticSeverityType = _roslynCoreAsm.GetType("Microsoft.CodeAnalysis.DiagnosticSeverity");
                    var errorSeverity = Enum.Parse(diagnosticSeverityType, "Error");

                    var errors = new List<string>();
                    foreach (var diag in diagnostics)
                    {
                        var severity = diag.GetType().GetProperty("Severity").GetValue(diag);
                        if (!severity.Equals(errorSeverity)) continue;

                        var location = diag.GetType().GetProperty("Location").GetValue(diag);
                        var lineSpan = location.GetType().GetMethod("GetMappedLineSpan").Invoke(location, null);
                        var startPos = lineSpan.GetType().GetProperty("StartLinePosition").GetValue(lineSpan);
                        int line = (int)startPos.GetType().GetProperty("Line").GetValue(startPos);
                        string message;
                        try
                        {
                            // GetMessage has signature GetMessage(IFormatProvider = null)
                            var getMsg = diag.GetType().GetMethod("GetMessage");
                            message = getMsg != null
                                ? (string)getMsg.Invoke(diag, new object[] { null })
                                : diag.ToString();
                        }
                        catch { message = diag.ToString(); }

                        int generatedLine = line + 1;
                        int userLine = generatedLine - generatedPrefixLineCount;
                        string lineLabel = userLine >= 1 && userLine <= userCodeLineCount
                            ? $"Line {userLine}"
                            : $"Generated line {generatedLine}";
                        errors.Add($"{lineLabel}: {message}");
                    }

                    return VmAutomationResponse.Error("Compilation failed",
                        "execute_code_compilation_failed", false, new Dictionary<string, object>
                    {
                        { "errors", errors },
                        { "codePreview", code.Length <= 2000 ? code : code.Substring(0, 2000) },
                        { "codeTruncated", code.Length > 2000 },
                        { "userCodeExecuted", false },
                    });
                }

                object loadContext = null;
                bool collectibleAssemblyContext = false;
                try
                {
                    if (TryExecuteInIsolatedAppDomain(outputPath, args, out var isolatedResponse,
                            out string appDomainMessage, out bool requiresDefaultDomain))
                    {
                        isolatedResponse["collectibleAssemblyContext"] = false;
                        isolatedResponse["assemblyIsolation"] = "app-domain";
                        return isolatedResponse;
                    }

                    var compiledAssembly = LoadDynamicAssembly(outputPath, out loadContext,
                        out collectibleAssemblyContext);
                    var compiledType = compiledAssembly.GetType("VmAutomationDynamicCode");
                    var method = compiledType.GetMethod("Execute");
                    var result = method.Invoke(null, null);
                    var response = SerializeResult(result, args);
                    response["collectibleAssemblyContext"] = collectibleAssemblyContext;
                    response["assemblyIsolation"] = collectibleAssemblyContext
                        ? "collectible-load-context"
                        : "default-domain";
                    if (appDomainMessage.Length > 0)
                    {
                        response[requiresDefaultDomain ? "assemblyIsolationReason" : "assemblyIsolationWarning"] =
                            appDomainMessage;
                    }
                    return response;
                }
                finally
                {
                    TryUnloadAssemblyContext(loadContext);
                    try
                    {
                        File.Delete(outputPath);
                    }
                    catch
                    {
                    }
                }
            }
            catch (TargetInvocationException ex)
            {
                return BuildExecuteCodeError(ex.InnerException ?? ex, args);
            }
            catch (Exception ex)
            {
                return BuildExecuteCodeError(ex, args);
            }
        }

        internal static bool CanUseIsolatedAppDomain(IEnumerable<string> assemblyNames, out string reason)
        {
            if (assemblyNames == null)
            {
                reason = "Skipped isolated AppDomain because the dynamic-code assembly references could not be inspected safely.";
                return false;
            }

            foreach (string assemblyName in assemblyNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                if (IsFrameworkAssembly(assemblyName))
                    continue;

                reason = $"Skipped isolated AppDomain because dynamic code references '{assemblyName}', which must use Unity's loaded assembly context.";
                return false;
            }

            reason = "";
            return true;
        }

        private static bool IsFrameworkAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return true;

            return string.Equals(assemblyName, "mscorlib", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(assemblyName, "netstandard", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(assemblyName, "System", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(assemblyName, "Microsoft.CSharp", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildExecuteCodeSource(Dictionary<string, object> args, string code,
            out int generatedPrefixLineCount, out int userCodeLineCount, out string error)
        {
            var namespaces = new List<string>
            {
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "UnityEngine",
                "UnityEngine.UIElements",
                "UnityEditor",
                "UnityEditor.SceneManagement",
            };

            if (!TryAddUsingNamespaces(
                    namespaces,
                    VmAutomationSettings.GetExecuteCodeAdditionalNamespaces(),
                    "Project Settings > VM Unity Automation > Execute Code",
                    out error))
            {
                generatedPrefixLineCount = 0;
                userCodeLineCount = 0;
                return "";
            }

            if (!TryAddUsingNamespaces(
                    namespaces,
                    GetRequestUsingNamespaces(args),
                    "usings",
                    out error))
            {
                generatedPrefixLineCount = 0;
                userCodeLineCount = 0;
                return "";
            }

            var prefix = new StringBuilder();
            foreach (string namespaceName in namespaces)
                prefix.Append("using ").Append(namespaceName).AppendLine(";");
            prefix.AppendLine();
            prefix.AppendLine("public static class VmAutomationDynamicCode");
            prefix.AppendLine("{");
            prefix.AppendLine("    public static object Execute()");
            prefix.AppendLine("    {");

            generatedPrefixLineCount = prefix.ToString().Count(character => character == '\n');
            userCodeLineCount = Math.Max(1, code.Count(character => character == '\n') + 1);
            error = "";
            return prefix.ToString() + code + Environment.NewLine + "        return null;" + Environment.NewLine + "    }" +
                   Environment.NewLine + "}";
        }

        private static bool TryAddUsingNamespaces(
            List<string> namespaces,
            IEnumerable<string> additionalNamespaces,
            string source,
            out string error)
        {
            foreach (string namespaceName in additionalNamespaces)
            {
                if (!IsValidNamespace(namespaceName))
                {
                    error = $"Invalid namespace in {source}: '{namespaceName}'.";
                    return false;
                }

                if (!namespaces.Contains(namespaceName, StringComparer.Ordinal))
                    namespaces.Add(namespaceName);
            }

            error = "";
            return true;
        }

        private static IEnumerable<string> GetRequestUsingNamespaces(Dictionary<string, object> args)
        {
            if (args == null)
                yield break;

            if (!args.TryGetValue("usings", out object value) || value == null)
                yield break;

            if (value is string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text.Trim();
                yield break;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    string namespaceName = item?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(namespaceName))
                        yield return namespaceName;
                }
            }
        }

        internal static bool IsValidNamespace(string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
                return false;

            foreach (string part in namespaceName.Split('.'))
            {
                if (part.Length == 0 || char.IsLetter(part[0]) == false && part[0] != '_')
                    return false;

                for (int i = 1; i < part.Length; i++)
                {
                    if (char.IsLetterOrDigit(part[i]) == false && part[i] != '_')
                        return false;
                }
            }

            return true;
        }

        private static Assembly LoadDynamicAssembly(string path, out object loadContext,
            out bool collectibleAssemblyContext)
        {
            loadContext = null;
            collectibleAssemblyContext = false;
            var loadContextType = Type.GetType(
                "System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader", false) ??
                                  AppDomain.CurrentDomain.GetAssemblies()
                                      .Select(assembly => assembly.GetType(
                                          "System.Runtime.Loader.AssemblyLoadContext", false))
                                      .FirstOrDefault(type => type != null);
            if (loadContextType != null)
            {
                try
                {
                    var constructor = loadContextType.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                        new[] { typeof(string), typeof(bool) }, null);
                    var loadFromStream = loadContextType.GetMethod("LoadFromStream", new[] { typeof(Stream) });
                    var unload = loadContextType.GetMethod("Unload", Type.EmptyTypes);
                    if (constructor != null && loadFromStream != null && unload != null)
                    {
                        loadContext = constructor.Invoke(new object[]
                        {
                            "VMUnityAutomationDynamicCode_" + Guid.NewGuid().ToString("N"),
                            true,
                        });
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var assembly = loadFromStream.Invoke(loadContext, new object[] { stream }) as Assembly;
                            if (assembly != null)
                            {
                                collectibleAssemblyContext = true;
                                return assembly;
                            }
                        }
                    }
                }
                catch
                {
                    TryUnloadAssemblyContext(loadContext);
                    loadContext = null;
                }
            }

            return Assembly.Load(File.ReadAllBytes(path));
        }

        private static bool TryExecuteInIsolatedAppDomain(string path, Dictionary<string, object> args,
            out Dictionary<string, object> response, out string error, out bool requiresDefaultDomain)
        {
            response = null;
            error = "";
            requiresDefaultDomain = false;
            AppDomain domain = null;
            try
            {
                var createDomain = typeof(AppDomain).GetMethod("CreateDomain",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                var unloadDomain = typeof(AppDomain).GetMethod("Unload",
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(AppDomain) }, null);
                if (createDomain == null || unloadDomain == null)
                    return false;

                domain = createDomain.Invoke(null,
                    new object[] { "VMUnityAutomationDynamicCode_" + Guid.NewGuid().ToString("N") }) as AppDomain;
                if (domain == null)
                    return false;

                var createExecutor = domain.GetType().GetMethod("CreateInstanceFromAndUnwrap",
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(string), typeof(string) }, null);
                var executor = createExecutor?.Invoke(domain, new object[]
                {
                    typeof(VmAutomationDynamicCodeDomainExecutor).Assembly.Location,
                    typeof(VmAutomationDynamicCodeDomainExecutor).FullName,
                }) as VmAutomationDynamicCodeDomainExecutor;
                if (executor == null)
                {
                    error = "Could not create the isolated dynamic-code executor.";
                    return false;
                }

                response = executor.Execute(File.ReadAllBytes(path), args);
                if (response != null && response.TryGetValue("requiresDefaultDomain", out object fallbackValue) &&
                    Convert.ToBoolean(fallbackValue))
                {
                    requiresDefaultDomain = true;
                    error = response.TryGetValue("assemblyIsolationReason", out object reasonValue)
                        ? reasonValue?.ToString() ?? ""
                        : "Dynamic code requires Unity's loaded assembly context.";
                    response = null;
                    return false;
                }
                return response != null;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
            finally
            {
                if (domain != null)
                {
                    try
                    {
                        AppDomain.Unload(domain);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void TryUnloadAssemblyContext(object loadContext)
        {
            if (loadContext == null)
                return;

            try
            {
                loadContext.GetType().GetMethod("Unload", Type.EmptyTypes)?.Invoke(loadContext, null);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Serialize the result of ExecuteCode into a JSON-friendly structure.
        /// Handles primitives, dictionaries, anonymous objects, lists, arrays,
        /// and Unity types (Vector3, Color, etc.)
        /// </summary>
        internal static Dictionary<string, object> SerializeResult(object result, Dictionary<string, object> args)
        {
            var budget = new ResultSerializationBudget(
                Math.Max(1, Math.Min(GetInt(args, "maxResultItems", 200), 2000)),
                Math.Max(1, Math.Min(GetInt(args, "maxResultDepth", 8), 16)),
                Math.Max(100, Math.Min(GetInt(args, "maxResultStringLength", 20000), 200000)),
                string.Equals(GetString(args, "unityStructFormat", "compact"),
                    "structured", StringComparison.OrdinalIgnoreCase));
            object serialized = SerializeResultValue(result, 0,
                new HashSet<object>(ReferenceEqualityComparer.Instance), budget);
            var response = new Dictionary<string, object>
            {
                { "success", true },
                { "result", serialized },
                { "truncated", budget.Truncated },
                { "serializedItems", budget.SerializedItems },
                { "maxResultItems", budget.MaxItems },
                { "maxResultDepth", budget.MaxDepth },
                { "maxResultStringLength", budget.MaxStringLength },
            };
            if (result is System.Collections.ICollection collection)
                response["count"] = collection.Count;
            return response;
        }

        private static object SerializeResultValue(object value, int depth, HashSet<object> visiting,
            ResultSerializationBudget budget)
        {
            if (value == null)
                return null;
            if (depth > budget.MaxDepth)
            {
                budget.Truncated = true;
                return "<max-depth>";
            }

            Type type = value.GetType();
            if (value is string text)
            {
                if (text.Length <= budget.MaxStringLength)
                    return text;
                budget.Truncated = true;
                return text.Substring(0, budget.MaxStringLength) + "<truncated>";
            }
            if (value is bool || value is byte || value is sbyte || value is short ||
                value is ushort || value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal)
                return value;
            if (value is char character)
                return character.ToString();
            if (type.IsEnum)
                return value.ToString();
            if (value is DateTime dateTime)
                return dateTime.ToString("O");
            if (value is DateTimeOffset dateTimeOffset)
                return dateTimeOffset.ToString("O");
            if (value is Guid guid)
                return guid.ToString();
            if (value is Type reflectedType)
                return reflectedType.FullName;

            // Execute-code eagerly serializes arbitrary values before VmAutomationResponse reaches the
            // transport boundary. Route every supported Unity value struct through the shared
            // formatter here as well, otherwise structs such as Rect are reflected into all of
            // their redundant aliases (x/position/min/left/etc.) before transport compaction.
            if (budget.StructuredUnityValues)
            {
                if (VmAutomationUnityValueFormatter.TryStructureUnityValue(value, out object structuredUnityValue))
                    return structuredUnityValue;
            }
            else if (VmAutomationUnityValueFormatter.TryFormatUnityValue(value, out string compactUnityValue))
            {
                return compactUnityValue;
            }

            if (value is UnityEngine.Object unityObject)
            {
                // Unity keeps a CLR wrapper after the native object is destroyed. Such a wrapper
                // passes the C# type check but compares equal to null through Unity's overloaded
                // equality operator, and reading name/instance ID can throw from native bindings.
                if (unityObject == null)
                    return null;

                try
                {
                    string assetPath = AssetDatabase.GetAssetPath(unityObject);
                    var unityResult = new Dictionary<string, object>
                    {
                        { "name", unityObject.name },
                        { "type", unityObject.GetType().FullName },
                        { "instanceId", VmObjectId.Get(unityObject) },
                    };
                    if (!string.IsNullOrEmpty(assetPath))
                        unityResult["assetPath"] = assetPath;
                    return unityResult;
                }
                catch (MissingReferenceException)
                {
                    return null;
                }
                catch (NullReferenceException)
                {
                    return null;
                }
            }

            bool trackReference = !type.IsValueType;
            if (trackReference && !visiting.Add(value))
                return "<cycle>";

            try
            {
                if (value is System.Collections.IDictionary dictionary)
                {
                    var result = new Dictionary<string, object>();
                    foreach (System.Collections.DictionaryEntry entry in dictionary)
                    {
                        if (!budget.TryConsume())
                        {
                            result["$truncated"] = true;
                            break;
                        }
                        string key = entry.Key?.ToString() ?? "null";
                        result[key] = SerializeResultValue(entry.Value, depth + 1, visiting, budget);
                    }
                    return result;
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    var result = new List<object>();
                    foreach (var item in enumerable)
                    {
                        if (!budget.TryConsume())
                        {
                            result.Add("<truncated>");
                            break;
                        }
                        result.Add(SerializeResultValue(item, depth + 1, visiting, budget));
                    }
                    return result;
                }

                var objectResult = new Dictionary<string, object>();
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length > 0)
                        continue;
                    if (!budget.TryConsume())
                    {
                        objectResult["$truncated"] = true;
                        break;
                    }
                    try
                    {
                        objectResult[property.Name] = SerializeResultValue(property.GetValue(value), depth + 1,
                            visiting, budget);
                    }
                    catch (Exception ex)
                    {
                        objectResult[property.Name] = $"<error: {ex.GetBaseException().Message}>";
                    }
                }

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!objectResult.ContainsKey(field.Name))
                    {
                        if (!budget.TryConsume())
                        {
                            objectResult["$truncated"] = true;
                            break;
                        }
                        objectResult[field.Name] = SerializeResultValue(field.GetValue(value), depth + 1,
                            visiting, budget);
                    }
                }

                return objectResult.Count > 0 ? (object)objectResult : value.ToString();
            }
            finally
            {
                if (trackReference)
                    visiting.Remove(value);
            }
        }

        private sealed class ResultSerializationBudget
        {
            private int _remainingItems;

            public readonly int MaxItems;
            public readonly int MaxDepth;
            public readonly int MaxStringLength;
            public readonly bool StructuredUnityValues;
            public int SerializedItems { get; private set; }
            public bool Truncated { get; set; }

            public ResultSerializationBudget(int maxItems, int maxDepth, int maxStringLength,
                bool structuredUnityValues)
            {
                MaxItems = maxItems;
                MaxDepth = maxDepth;
                MaxStringLength = maxStringLength;
                StructuredUnityValues = structuredUnityValues;
                _remainingItems = maxItems;
            }

            public bool TryConsume()
            {
                if (_remainingItems <= 0)
                {
                    Truncated = true;
                    return false;
                }

                _remainingItems--;
                SerializedItems++;
                return true;
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static bool IsEditorIdle()
        {
            return GetEditorIdleSnapshot().IsIdle;
        }

        private static Dictionary<string, object> BuildIdleResult(bool success, bool timedOut, int timeoutMs,
            int stableFrames, int stableMs, int currentStableFrames, double stableDurationMs, double startTime,
            EditorIdleSnapshot snapshot, List<string> lastBusyReasons, int resumeCount)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var result = new Dictionary<string, object>
            {
                { "success", success },
                { "timedOut", timedOut },
                { "isIdle", snapshot.IsIdle },
                { "busyReasons", snapshot.BusyReasons },
                { "lastBusyReasons", lastBusyReasons },
                { "isCompiling", snapshot.IsCompiling },
                { "isUpdating", snapshot.IsUpdating },
                { "isPlaying", EditorApplication.isPlaying },
                { "isChangingPlayMode", snapshot.IsChangingPlayMode },
                { "isPlayingOrWillChangePlaymode", snapshot.IsPlayingOrWillChangePlaymode },
                { "activeScene", scene.name },
                { "activeScenePath", scene.path },
                { "timeoutMs", timeoutMs },
                { "stableFrames", stableFrames },
                { "stableMs", stableMs },
                { "currentStableFrames", currentStableFrames },
                { "stableDurationMs", (long)stableDurationMs },
                { "elapsedMs", (long)((EditorApplication.timeSinceStartup - startTime) * 1000d) },
                { "resumedAfterReload", resumeCount > 0 },
                { "resumeCount", resumeCount },
                { "compilationDiagnostics", VmAutomationConsoleCommands.GetCompilationDiagnosticsSummary() },
            };
            if (timedOut)
            {
                const string message = "Timed out waiting for the Unity Editor to become idle.";
                result["error"] = message;
                result["message"] = message;
                result["errorCode"] = "editor_idle_timeout";
                result["retryable"] = true;
            }
            return result;
        }

        private static EditorIdleSnapshot GetEditorIdleSnapshot()
        {
            var busyReasons = new List<string>();
            bool isCompiling = EditorApplication.isCompiling;
            bool isUpdating = EditorApplication.isUpdating;
            bool isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode;
            bool isChangingPlayMode = isPlayingOrWillChangePlaymode != EditorApplication.isPlaying;

            if (isCompiling)
                busyReasons.Add("compiling");
            if (isUpdating)
                busyReasons.Add("asset_database_updating");
            if (isChangingPlayMode)
                busyReasons.Add("play_mode_changing");

            return new EditorIdleSnapshot
            {
                IsIdle = busyReasons.Count == 0,
                IsCompiling = isCompiling,
                IsUpdating = isUpdating,
                IsPlayingOrWillChangePlaymode = isPlayingOrWillChangePlaymode,
                IsChangingPlayMode = isChangingPlayMode,
                BusyReasons = busyReasons
            };
        }

        private sealed class EditorIdleSnapshot
        {
            public bool IsIdle;
            public bool IsCompiling;
            public bool IsUpdating;
            public bool IsPlayingOrWillChangePlaymode;
            public bool IsChangingPlayMode;
            public List<string> BusyReasons;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || args.ContainsKey(key) == false || args[key] == null)
                return defaultValue;

            return int.TryParse(args[key].ToString(), out int value) ? value : defaultValue;
        }

        private static string GetString(Dictionary<string, object> args, string key, string defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : defaultValue;
        }

        internal static Dictionary<string, object> BuildExecuteCodeError(
            Exception exception,
            Dictionary<string, object> args)
        {
            Debug.LogException(exception);
            Dictionary<string, object> extra = null;
            if (args != null &&
                args.TryGetValue("includeStackTrace", out object includeStackTrace) &&
                includeStackTrace != null &&
                bool.TryParse(includeStackTrace.ToString(), out bool include) &&
                include)
            {
                extra = new Dictionary<string, object>
                {
                    { "stackTrace", exception.StackTrace ?? "" },
                };
            }

            return VmAutomationResponse.Error(exception.Message, "execute_code_exception", false, extra);
        }

        private static bool TryGetUtcDateTime(Dictionary<string, object> args, string key, out DateTime value)
        {
            value = default;
            if (args == null || !args.TryGetValue(key, out object raw) || raw == null ||
                !DateTime.TryParse(raw.ToString(), out value))
                return false;

            value = value.ToUniversalTime();
            return true;
        }
    }

}
