using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for managing MPPM (Multiplayer PlayMode) scenarios via reflection.
    /// All MPPM scenario types are internal, so we use reflection to access them.
    /// </summary>
    public static class MCPScenarioCommands
    {
        // MPPM scenario types — historically lived in package assembly
        // `Unity.Multiplayer.PlayMode.Scenarios.Editor` under namespace
        // `Unity.Multiplayer.PlayMode.Scenarios.Editor.*` (Unity 2022/2023 + older).
        // In Unity 6 MPPM was absorbed into the built-in editor module
        // `UnityEditor.MultiplayerModule` under namespace
        // `Unity.Multiplayer.PlayMode.Editor.*`. Properties and method names are
        // mostly preserved across the rename, so the reflection helpers below work
        // for both once the types are resolved here.
        private static Type _scenarioConfigType;   // .ScenarioConfig (internal)
        private static Type _scenarioRunnerType;    // .ScenarioRunner (internal, static helpers)
        private static Type _scenarioStatusType;    // .ScenarioStatus (internal struct)
        private static Type _scenarioType;          // .Scenario / .GraphsFoundation.Scenario (internal)
        private static Type _instanceDescriptionType; // .InstanceDescription (internal)

        // Assembly: Unity.Multiplayer.Playmode (current-player tagging API, still a package)
        private static Type _currentPlayerType;     // Unity.Multiplayer.Playmode.CurrentPlayer (public)

        private static bool _initialized = false;
        private static bool _mppmAvailable = false;

        private static void InitializeReflection()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // Find assemblies by name since the types are internal
                Assembly scenariosAssembly = null;
                Assembly mppmAssembly = null;

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var asmName = asm.GetName().Name;
                    // Old (package-based) and Unity 6 (module-based) assemblies both host
                    // the MPPM scenario APIs. Accept whichever is present.
                    if (scenariosAssembly == null && (
                        asmName == "Unity.Multiplayer.PlayMode.Scenarios.Editor" ||
                        asmName == "UnityEditor.MultiplayerModule"))
                        scenariosAssembly = asm;
                    else if (asmName == "Unity.Multiplayer.Playmode")
                        mppmAssembly = asm;
                }

                if (scenariosAssembly != null)
                {
                    // The "scenario config" ScriptableObject: ScenarioConfig pre-Unity 6,
                    // renamed OrchestratedScenario in MPPM 2.0 (Unity 6).
                    _scenarioConfigType = FirstType(scenariosAssembly,
                        "Unity.Multiplayer.PlayMode.Scenarios.Editor.ScenarioConfig",     // old (pre-Unity 6)
                        "Unity.Multiplayer.PlayMode.Editor.OrchestratedScenario");        // MPPM 2.0 (Unity 6)
                    _scenarioRunnerType = FirstType(scenariosAssembly,
                        "Unity.Multiplayer.PlayMode.Scenarios.Editor.ScenarioRunner",
                        "Unity.Multiplayer.PlayMode.Editor.ScenarioRunner");
                    // The scenario status struct: ScenarioStatus pre-Unity 6,
                    // renamed ScenarioStatusData in MPPM 2.0.
                    _scenarioStatusType = FirstType(scenariosAssembly,
                        "Unity.Multiplayer.PlayMode.Scenarios.Editor.Api.ScenarioStatus", // old
                        "Unity.Multiplayer.PlayMode.Editor.ScenarioStatusData");          // MPPM 2.0
                    _scenarioType = FirstType(scenariosAssembly,
                        "Unity.Multiplayer.PlayMode.Scenarios.Editor.GraphsFoundation.Scenario",
                        "Unity.Multiplayer.PlayMode.Editor.Scenario");
                    _instanceDescriptionType = FirstType(scenariosAssembly,
                        "Unity.Multiplayer.PlayMode.Scenarios.Editor.InstanceDescription",
                        "Unity.Multiplayer.PlayMode.Editor.InstanceDescription");
                }

                if (mppmAssembly != null)
                {
                    _currentPlayerType = mppmAssembly.GetType("Unity.Multiplayer.Playmode.CurrentPlayer");
                }

                _mppmAvailable = _scenarioConfigType != null && _scenarioRunnerType != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VMUnityAutomation] MPPM reflection init failed: {ex.Message}");
                _mppmAvailable = false;
            }
        }

        private static Type FirstType(Assembly asm, params string[] candidates)
        {
            foreach (var n in candidates)
            {
                var t = asm.GetType(n);
                if (t != null) return t;
            }
            return null;
        }

        private static object WrapError(string message)
        {
            return new Dictionary<string, object>
            {
                { "error", message },
                { "mppmAvailable", _mppmAvailable }
            };
        }

        // ─── Virtual Player management (MPPM) ────────────────────────────────────
        // Resolves Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode + UnityPlayer
        // by reflection and exposes List/Activate/Deactivate. Players are addressed by
        // 1-based index (1 = main editor / PlayerOne, 2..4 = virtual players).

        private static Type ResolveMppmPlaymodeType()
        {
            return Type.GetType("Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode, UnityEditor.MultiplayerModule");
        }

        private static object GetPlayerByIndex(int index)
        {
            var t = ResolveMppmPlaymodeType();
            if (t == null) return null;
            string propName;
            switch (index)
            {
                case 1: propName = "PlayerOne"; break;
                case 2: propName = "PlayerTwo"; break;
                case 3: propName = "PlayerThree"; break;
                case 4: propName = "PlayerFour"; break;
                default: return null;
            }
            var prop = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Static);
            return prop?.GetValue(null);
        }

        private static Dictionary<string, object> DescribePlayer(object player, int index)
        {
            if (player == null)
                return new Dictionary<string, object> { { "index", index }, { "available", false } };

            var pt = player.GetType();
            string Name = pt.GetProperty("Name")?.GetValue(player)?.ToString() ?? "";
            string State = pt.GetProperty("PlayerState")?.GetValue(player)?.ToString() ?? "";
            string Type = pt.GetProperty("Type")?.GetValue(player)?.ToString() ?? "";
            string Role = pt.GetProperty("Role")?.GetValue(player)?.ToString() ?? "";
            return new Dictionary<string, object>
            {
                { "index", index },
                { "available", true },
                { "name", Name },
                { "state", State },     // NotLaunched, Launching, Launched, Communicative, etc.
                { "type", Type },        // Main, Virtual, Local, Remote
                { "role", Role }
            };
        }

        public static object MppmListPlayers(Dictionary<string, object> args)
        {
            try
            {
                var t = ResolveMppmPlaymodeType();
                if (t == null)
                    return WrapError("MultiplayerPlaymode type not found — Multiplayer Play Mode package not installed?");

                var list = new List<Dictionary<string, object>>();
                for (int i = 1; i <= 4; i++)
                    list.Add(DescribePlayer(GetPlayerByIndex(i), i));

                return new Dictionary<string, object>
                {
                    { "players", list }
                };
            }
            catch (Exception e)
            {
                return WrapError("MppmListPlayers exception: " + e.Message);
            }
        }

        public static object MppmActivatePlayer(Dictionary<string, object> args)
        {
            int index = args != null && args.ContainsKey("index")
                ? Convert.ToInt32(args["index"]) : 0;
            if (index < 2 || index > 4)
                return WrapError("index must be 2, 3, or 4 (Player 1 is the main editor and cannot be activated this way)");

            try
            {
                var player = GetPlayerByIndex(index);
                if (player == null)
                    return WrapError($"Player {index} not found");

                var pt = player.GetType();
                var stateBefore = pt.GetProperty("PlayerState")?.GetValue(player)?.ToString();

                var activate = pt.GetMethod("Activate");
                if (activate == null)
                    return WrapError("UnityPlayer.Activate method not found");

                var listType = activate.GetParameters()[1].ParameterType;
                var emptyList = Activator.CreateInstance(listType);
                var callArgs = new object[] { null, emptyList };
                bool ok = (bool)activate.Invoke(player, callArgs);

                var stateAfter = pt.GetProperty("PlayerState")?.GetValue(player)?.ToString();
                return new Dictionary<string, object>
                {
                    { "success", ok },
                    { "index", index },
                    { "name", pt.GetProperty("Name")?.GetValue(player)?.ToString() },
                    { "stateBefore", stateBefore },
                    { "stateAfter", stateAfter },
                    { "error", callArgs[0]?.ToString() }
                };
            }
            catch (Exception e)
            {
                return WrapError("MppmActivatePlayer exception: " + e.Message);
            }
        }

        public static object MppmDeactivatePlayer(Dictionary<string, object> args)
        {
            int index = args != null && args.ContainsKey("index")
                ? Convert.ToInt32(args["index"]) : 0;
            if (index < 2 || index > 4)
                return WrapError("index must be 2, 3, or 4");

            try
            {
                var player = GetPlayerByIndex(index);
                if (player == null)
                    return WrapError($"Player {index} not found");

                var pt = player.GetType();
                var stateBefore = pt.GetProperty("PlayerState")?.GetValue(player)?.ToString();

                var deactivate = pt.GetMethod("Deactivate");
                if (deactivate == null)
                    return WrapError("UnityPlayer.Deactivate method not found");

                var callArgs = new object[] { null };
                bool ok = (bool)deactivate.Invoke(player, callArgs);

                var stateAfter = pt.GetProperty("PlayerState")?.GetValue(player)?.ToString();
                return new Dictionary<string, object>
                {
                    { "success", ok },
                    { "index", index },
                    { "name", pt.GetProperty("Name")?.GetValue(player)?.ToString() },
                    { "stateBefore", stateBefore },
                    { "stateAfter", stateAfter },
                    { "error", callArgs[0]?.ToString() }
                };
            }
            catch (Exception e)
            {
                return WrapError("MppmDeactivatePlayer exception: " + e.Message);
            }
        }

        /// <summary>
        /// Whether this Editor is an MPPM Virtual Player (a clone launched by
        /// Multiplayer Play Mode), as opposed to the main Editor. Returns false
        /// when MPPM is not installed or the player role cannot be determined —
        /// callers should treat "unknown" as "main Editor" (do not block startup).
        /// </summary>
        public static bool IsVirtualPlayer()
        {
            // MPPM's CurrentPlayer type moved between Unity versions:
            //   Unity 6+    : Unity.Multiplayer.PlayMode.CurrentPlayer in UnityEngine.MultiplayerModule
            //   pre-Unity 6 : Unity.Multiplayer.Playmode.CurrentPlayer in Unity.Multiplayer.Playmode
            var currentPlayerType =
                Type.GetType("Unity.Multiplayer.PlayMode.CurrentPlayer, UnityEngine.MultiplayerModule")
                ?? Type.GetType("Unity.Multiplayer.Playmode.CurrentPlayer, Unity.Multiplayer.Playmode");
            if (currentPlayerType == null) return false;
            try
            {
                var isMainEditorProperty = currentPlayerType.GetProperty("IsMainEditor",
                    BindingFlags.Static | BindingFlags.Public);
                if (isMainEditorProperty == null) return false;
                return isMainEditorProperty.GetValue(null) is bool isMain && !isMain;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// List all ScenarioConfig assets in the project.
        /// Since GetAllInstances is an instance method, we find configs via AssetDatabase.
        /// </summary>
        public static object ListScenarios(Dictionary<string, object> args)
        {
            InitializeReflection();

            if (!_mppmAvailable)
                return WrapError("MPPM (Multiplayer PlayMode) is not installed in this project");

            try
            {
                var result = new Dictionary<string, object>();
                var scenarioList = new List<Dictionary<string, object>>();

                // Find all ScenarioConfig ScriptableObject assets via AssetDatabase
                var guids = AssetDatabase.FindAssets("t:ScriptableObject");

                // Get properties we need (using NonPublic since type is internal)
                var bindFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var scenarioProperty = _scenarioConfigType.GetProperty("Scenario", bindFlags);
                var descriptionProperty = _scenarioConfigType.GetProperty("Description", bindFlags);
                // Old ScenarioConfig exposed instances as properties; MPPM 2.0's
                // OrchestratedScenario stores them in m_* fields — read either.
                var editorInstanceProperty = _scenarioConfigType.GetProperty("EditorInstance", bindFlags);
                var virtualEditorInstancesProperty = _scenarioConfigType.GetProperty("VirtualEditorInstances", bindFlags);
                var localInstancesProperty = _scenarioConfigType.GetProperty("LocalInstances", bindFlags);
                var editorInstanceField = _scenarioConfigType.GetField("m_MainEditorInstance", bindFlags);
                var virtualEditorInstancesField = _scenarioConfigType.GetField("m_EditorInstances", bindFlags);
                var localInstancesField = _scenarioConfigType.GetField("m_LocalInstances", bindFlags);

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(path, _scenarioConfigType);
                    if (asset == null) continue;

                    try
                    {
                        var scenarioObj = scenarioProperty?.GetValue(asset);
                        var scenarioName = (scenarioObj as UnityEngine.Object)?.name ?? asset.name;
                        var description = descriptionProperty?.GetValue(asset) as string ?? "";

                        var editorInst = editorInstanceProperty?.GetValue(asset) ?? editorInstanceField?.GetValue(asset);
                        var virtualInsts = (virtualEditorInstancesProperty?.GetValue(asset) ?? virtualEditorInstancesField?.GetValue(asset)) as System.Collections.IEnumerable;
                        var localInsts = (localInstancesProperty?.GetValue(asset) ?? localInstancesField?.GetValue(asset)) as System.Collections.IEnumerable;

                        var scenarioInfo = new Dictionary<string, object>
                        {
                            { "name", scenarioName },
                            { "path", path },
                            { "description", description },
                            { "hasEditorInstance", editorInst != null },
                            { "virtualInstanceCount", virtualInsts?.Cast<object>().Count() ?? 0 },
                            { "localInstanceCount", localInsts?.Cast<object>().Count() ?? 0 }
                        };

                        // Add instance details if available
                        var instancesInfo = new List<Dictionary<string, object>>();

                        if (editorInst != null)
                        {
                            instancesInfo.Add(GetInstanceInfo(editorInst, "Editor"));
                        }

                        if (virtualInsts != null)
                        {
                            int idx = 0;
                            foreach (var inst in virtualInsts)
                            {
                                instancesInfo.Add(GetInstanceInfo(inst, $"VirtualEditor{idx}"));
                                idx++;
                            }
                        }

                        if (localInsts != null)
                        {
                            int idx = 0;
                            foreach (var inst in localInsts)
                            {
                                instancesInfo.Add(GetInstanceInfo(inst, $"LocalInstance{idx}"));
                                idx++;
                            }
                        }

                        scenarioInfo["instances"] = instancesInfo;
                        scenarioList.Add(scenarioInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[VMUnityAutomation] Error processing scenario config at {path}: {ex.Message}");
                    }
                }

                result["scenarios"] = scenarioList;
                result["count"] = scenarioList.Count;
                return result;
            }
            catch (Exception ex)
            {
                return WrapError($"Failed to list scenarios: {ex.Message}");
            }
        }

        private static Dictionary<string, object> GetInstanceInfo(object instance, string typeName)
        {
            var info = new Dictionary<string, object>
            {
                { "type", typeName }
            };

            try
            {
                if (instance == null) return info;

                var instanceType = instance.GetType();
                var bindFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var typeNameProperty = instanceType.GetProperty("InstanceTypeName", bindFlags);
                var runModeStateProperty = instanceType.GetProperty("RunModeState", bindFlags);

                if (typeNameProperty != null)
                    info["instanceTypeName"] = typeNameProperty.GetValue(instance) as string ?? "Unknown";

                if (runModeStateProperty != null)
                    info["runModeState"] = runModeStateProperty.GetValue(instance)?.ToString() ?? "Unknown";
            }
            catch (Exception ex)
            {
                info["error"] = ex.Message;
            }

            return info;
        }

        /// <summary>
        /// Get the current scenario runner status.
        /// </summary>
        public static object GetScenarioStatus(Dictionary<string, object> args)
        {
            InitializeReflection();

            if (!_mppmAvailable)
                return WrapError("MPPM is not installed");

            try
            {
                var result = new Dictionary<string, object>();

                var staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var instFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var getStatusMethod = _scenarioRunnerType.GetMethod("GetScenarioStatus", staticFlags);

                // On Unity 6, ScenarioRunner derives from ScriptableSingleton<ScenarioRunner>,
                // so IsRunning/ActiveScenario are INSTANCE properties accessed via the static
                // `instance` getter on the base class. The older package-based layout exposed
                // them statically — try that path first and fall back to the singleton.
                object isRunningValue = null;
                object activeScenarioValue = null;

                var isRunningStatic = _scenarioRunnerType.GetProperty("IsRunning", staticFlags);
                var activeStatic = _scenarioRunnerType.GetProperty("ActiveScenario", staticFlags);
                if (isRunningStatic != null) isRunningValue = isRunningStatic.GetValue(null);
                if (activeStatic != null) activeScenarioValue = activeStatic.GetValue(null);

                if (isRunningValue == null || activeScenarioValue == null)
                {
                    var baseType = _scenarioRunnerType.BaseType; // ScriptableSingleton<T>
                    var instanceProp = baseType?.GetProperty("instance", staticFlags);
                    var runnerInstance = instanceProp?.GetValue(null);
                    if (runnerInstance != null)
                    {
                        var isRunningInst = _scenarioRunnerType.GetProperty("IsRunning", instFlags);
                        var activeInst = _scenarioRunnerType.GetProperty("ActiveScenario", instFlags);
                        if (isRunningValue == null) isRunningValue = isRunningInst?.GetValue(runnerInstance);
                        if (activeScenarioValue == null) activeScenarioValue = activeInst?.GetValue(runnerInstance);
                    }
                }

                result["isRunning"] = isRunningValue ?? false;
                result["activeScenarioName"] = (activeScenarioValue as UnityEngine.Object)?.name ?? "None";

                // Get detailed status
                if (getStatusMethod != null)
                {
                    try
                    {
                        object scenarioStatus = getStatusMethod.Invoke(null, null);

                        if (scenarioStatus != null && _scenarioStatusType != null)
                        {
                            // ScenarioStatus is a struct with FIELDS, not properties
                            var instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                            var stateField = _scenarioStatusType.GetField("State", instanceFlags);
                            var currentStageField = _scenarioStatusType.GetField("CurrentStage", instanceFlags);
                            var totalProgressField = _scenarioStatusType.GetField("TotalProgress", instanceFlags);

                            if (stateField != null)
                                result["state"] = stateField.GetValue(scenarioStatus)?.ToString() ?? "Unknown";

                            if (currentStageField != null)
                                result["currentStage"] = currentStageField.GetValue(scenarioStatus)?.ToString() ?? "N/A";

                            if (totalProgressField != null)
                            {
                                var progress = totalProgressField.GetValue(scenarioStatus);
                                if (progress is float f)
                                    result["progress"] = f;
                                else if (progress != null)
                                    result["progress"] = progress.ToString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result["statusError"] = ex.Message;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return WrapError($"Failed to get scenario status: {ex.Message}");
            }
        }

        /// <summary>
        /// Load/activate a scenario by asset path.
        /// LoadScenario takes a Scenario object (not ScenarioConfig), so we extract it via the Scenario property.
        /// </summary>
        public static object ActivateScenario(Dictionary<string, object> args)
        {
            InitializeReflection();

            if (!_mppmAvailable)
                return WrapError("MPPM is not installed");

            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
                return WrapError("path parameter is required");

            try
            {
                // Load the ScenarioConfig asset
                var configAsset = AssetDatabase.LoadAssetAtPath(path, _scenarioConfigType);
                if (configAsset == null)
                    return WrapError($"Could not load ScenarioConfig at path: {path}");

                var instFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Prefer the instance method `CreateScenario()` on ScenarioConfig: it converts
                // the instance descriptions (Main + Virtual editors) into the runtime Scenario
                // graph. The `Scenario` property alone only surfaces a persisted sub-asset
                // which is HideFlags.DontSave and gets wiped on every domain reload.
                var createScenarioMethod = _scenarioConfigType.GetMethod("CreateScenario",
                    instFlags, null, Type.EmptyTypes, null);

                object scenarioObj = null;
                if (createScenarioMethod != null)
                {
                    scenarioObj = createScenarioMethod.Invoke(configAsset, null);
                }

                // Fallback: read the nested Scenario sub-asset directly for older MPPM builds.
                if (scenarioObj == null)
                {
                    var scenarioProperty = _scenarioConfigType.GetProperty("Scenario", instFlags);
                    scenarioObj = scenarioProperty?.GetValue(configAsset);
                }

                if (scenarioObj == null)
                    return WrapError("Could not build a Scenario from this ScenarioConfig");

                // Call ScenarioRunner.LoadScenario(Scenario scenario)
                var loadMethod = _scenarioRunnerType.GetMethod("LoadScenario",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (loadMethod == null)
                    return WrapError("Could not find ScenarioRunner.LoadScenario() method");

                loadMethod.Invoke(null, new[] { scenarioObj });

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "scenario", (scenarioObj as UnityEngine.Object)?.name ?? configAsset.name },
                    { "path", path }
                };
            }
            catch (Exception ex)
            {
                return WrapError($"Failed to activate scenario: {ex.Message}");
            }
        }

        /// <summary>
        /// Start the active scenario. Also flips the main editor into Play mode so MPPM's
        /// Play-mode hooks (OnPlayFromScenario + virtual-player launch) actually fire —
        /// calling only <c>ScenarioRunner.StartScenario()</c> leaves the runner marked
        /// IsRunning but the scene never enters Play on its own.
        /// </summary>
        public static object StartScenario(Dictionary<string, object> args)
        {
            InitializeReflection();

            if (!_mppmAvailable)
                return WrapError("MPPM is not installed");

            bool enterPlayMode = true;
            if (args != null && args.ContainsKey("enterPlayMode"))
                bool.TryParse(args["enterPlayMode"].ToString(), out enterPlayMode);

            try
            {
                var bindFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                var startMethod = _scenarioRunnerType.GetMethod("StartScenario", bindFlags,
                    null, Type.EmptyTypes, null);

                if (startMethod == null)
                    return WrapError("Could not find ScenarioRunner.StartScenario() method");

                startMethod.Invoke(null, null);

                bool playModeAlready = EditorApplication.isPlayingOrWillChangePlaymode;
                bool enteredPlayMode = false;
                if (enterPlayMode && !playModeAlready)
                {
                    EditorApplication.isPlaying = true;
                    enteredPlayMode = true;
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", enteredPlayMode
                        ? "Scenario started and Play mode entered"
                        : (playModeAlready ? "Scenario started (Play mode already active)"
                                           : "Scenario started (Play mode entry skipped)") },
                    { "enteredPlayMode", enteredPlayMode },
                    { "playModeAlreadyActive", playModeAlready }
                };
            }
            catch (Exception ex)
            {
                return WrapError($"Failed to start scenario: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the running scenario. Exits Play mode on the main editor as well so
        /// virtual-player processes shut down cleanly (they follow the main's Play state).
        /// </summary>
        public static object StopScenario(Dictionary<string, object> args)
        {
            InitializeReflection();

            if (!_mppmAvailable)
                return WrapError("MPPM is not installed");

            bool exitPlayMode = true;
            if (args != null && args.ContainsKey("exitPlayMode"))
                bool.TryParse(args["exitPlayMode"].ToString(), out exitPlayMode);

            try
            {
                var bindFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

                var stopMethod = _scenarioRunnerType.GetMethod("StopScenario", bindFlags,
                    null, Type.EmptyTypes, null);

                if (stopMethod == null)
                    return WrapError("Could not find ScenarioRunner.StopScenario() method");

                stopMethod.Invoke(null, null);

                bool exitedPlayMode = false;
                if (exitPlayMode && EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    exitedPlayMode = true;
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", exitedPlayMode
                        ? "Scenario stopped and Play mode exited"
                        : "Scenario stopped" },
                    { "exitedPlayMode", exitedPlayMode }
                };
            }
            catch (Exception ex)
            {
                return WrapError($"Failed to stop scenario: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a new MPPM ScenarioConfig asset programmatically. Supports the common
        /// host+clients layout: one MainEditor (defaults to ClientAndServer role) plus
        /// N VirtualEditor instances with configurable role. Only available on Unity 6
        /// where MPPM lives in <c>UnityEditor.MultiplayerModule</c>.
        /// </summary>
        /// <remarks>
        /// Accepted args:
        ///   name (string, required)
        ///   path (string, optional, default "Assets/MPPM/{name}.asset")
        ///   mainRole (string, optional: "Host" | "Client" | "Server", default "Host")
        ///   virtualEditors (int, optional, default 1) — number of virtual clones to add
        ///   virtualRole (string, optional: "Client" | "Server" | "Host", default "Client")
        ///   description (string, optional)
        /// </remarks>
        public static object CreateScenario(Dictionary<string, object> args)
        {
            InitializeReflection();

            if (!_mppmAvailable)
                return WrapError("MPPM is not installed");

            string name = args != null && args.ContainsKey("name") ? args["name"].ToString() : "";
            if (string.IsNullOrEmpty(name))
                return WrapError("name parameter is required");

            string path = args != null && args.ContainsKey("path") ? args["path"].ToString() : $"Assets/MPPM/{name}.asset";
            string mainRoleStr = args != null && args.ContainsKey("mainRole") ? args["mainRole"].ToString() : "Host";
            string virtualRoleStr = args != null && args.ContainsKey("virtualRole") ? args["virtualRole"].ToString() : "Client";
            int virtualEditors = 1;
            if (args != null && args.ContainsKey("virtualEditors"))
                int.TryParse(args["virtualEditors"].ToString(), out virtualEditors);

            try
            {
                // Resolve the types needed for this build of MPPM.
                Assembly scenariosAssembly = _scenarioConfigType.Assembly;
                Type mainEditorInstType = FirstType(scenariosAssembly,
                    "Unity.Multiplayer.PlayMode.Scenarios.Editor.MainEditorInstanceDescription",
                    "Unity.Multiplayer.PlayMode.Editor.MainEditorInstanceDescription");
                Type virtualEditorInstType = FirstType(scenariosAssembly,
                    "Unity.Multiplayer.PlayMode.Scenarios.Editor.VirtualEditorInstanceDescription",
                    "Unity.Multiplayer.PlayMode.Editor.VirtualEditorInstanceDescription");
                Type localInstType = FirstType(scenariosAssembly,
                    "Unity.Multiplayer.PlayMode.Scenarios.Editor.LocalInstanceDescription",
                    "Unity.Multiplayer.PlayMode.Editor.LocalInstanceDescription");
                // Remote instances were removed in MPPM 2.0 (Unity 6) — this type is optional.
                Type remoteInstType = FirstType(scenariosAssembly,
                    "Unity.Multiplayer.PlayMode.Scenarios.Editor.RemoteInstanceDescription",
                    "Unity.Multiplayer.PlayMode.Editor.RemoteInstanceDescription");

                // Role flags live in UnityEngine.MultiplayerModule (Client=1, Server=2, ClientAndServer=3).
                Type roleFlagsType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    if ((roleFlagsType = asm.GetType("UnityEngine.Multiplayer.Internal.MultiplayerRoleFlags")) != null) break;

                if (mainEditorInstType == null || virtualEditorInstType == null || localInstType == null
                    || roleFlagsType == null)
                    return WrapError("Required MPPM types not found; incompatible Unity version?");

                // ScenarioRunner.LoadScenario expects an actual Scenario object, so we create
                // one (a ScriptableObject) and nest it inside the config asset.
                var scenarioCreate = _scenarioType.GetMethod("Create",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (scenarioCreate == null)
                    return WrapError("Could not find Scenario.Create(string) factory");
                var scenarioInstance = scenarioCreate.Invoke(null, new object[] { name }) as UnityEngine.ScriptableObject;
                if (scenarioInstance == null)
                    return WrapError("Scenario.Create returned null");
                // Scenario.Create sets the internal m_Name but leaves UnityEngine.Object.name
                // empty; ListScenarios surfaces that .name in its output, and the MPPM UI
                // uses it for the scenario label.
                scenarioInstance.name = name;

                // Build the config ScriptableObject and wire up its fields.
                var config = UnityEngine.ScriptableObject.CreateInstance(_scenarioConfigType);
                config.name = name;

                var instFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _scenarioConfigType.GetField("m_Scenario", instFlags)?.SetValue(config, scenarioInstance);
                _scenarioConfigType.GetField("m_EnableEditors", instFlags)?.SetValue(config, true);

                int mainRole = ParseRoleFlag(mainRoleStr);
                int virtualRole = ParseRoleFlag(virtualRoleStr);

                // Main editor instance (runs inside this Unity).
                var main = Activator.CreateInstance(mainEditorInstType);
                mainEditorInstType.GetField("m_Role", instFlags)?.SetValue(main, Enum.ToObject(roleFlagsType, mainRole));
                mainEditorInstType.GetField("Name", instFlags)?.SetValue(main, "Main Editor");
                mainEditorInstType.GetField("PlayerInstanceIndex", instFlags)?.SetValue(main, 0);
                _scenarioConfigType.GetField("m_MainEditorInstance", instFlags)?.SetValue(config, main);

                // N virtual editor instances (cloned from the project, usually client roles).
                var listType = typeof(List<>).MakeGenericType(virtualEditorInstType);
                var editorList = Activator.CreateInstance(listType);
                var addMethod = listType.GetMethod("Add");
                for (int i = 0; i < virtualEditors; i++)
                {
                    var virt = Activator.CreateInstance(virtualEditorInstType);
                    virtualEditorInstType.GetField("m_Role", instFlags)?.SetValue(virt, Enum.ToObject(roleFlagsType, virtualRole));
                    virtualEditorInstType.GetField("Name", instFlags)?.SetValue(virt, $"Virtual Editor {i + 1}");
                    virtualEditorInstType.GetField("PlayerInstanceIndex", instFlags)?.SetValue(virt, i + 1);
                    addMethod.Invoke(editorList, new[] { virt });
                }
                _scenarioConfigType.GetField("m_EditorInstances", instFlags)?.SetValue(config, editorList);

                // Empty lists for local/remote so the MPPM UI doesn't NRE on null.
                _scenarioConfigType.GetField("m_LocalInstances", instFlags)
                    ?.SetValue(config, Activator.CreateInstance(typeof(List<>).MakeGenericType(localInstType)));
                if (remoteInstType != null)
                    _scenarioConfigType.GetField("m_RemoteInstances", instFlags)
                        ?.SetValue(config, Activator.CreateInstance(typeof(List<>).MakeGenericType(remoteInstType)));

                // Make sure the folder exists, then persist the config and the nested scenario.
                var folder = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !System.IO.Directory.Exists(folder))
                    System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.AddObjectToAsset(scenarioInstance, config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "path", path },
                    { "name", name },
                    { "mainRole", mainRoleStr },
                    { "virtualEditors", virtualEditors },
                    { "virtualRole", virtualRoleStr }
                };
            }
            catch (Exception ex)
            {
                return WrapError($"Failed to create scenario: {ex.Message}");
            }
        }

        private static int ParseRoleFlag(string role)
        {
            if (string.IsNullOrEmpty(role)) return 3;
            switch (role.Trim().ToLowerInvariant())
            {
                case "client": return 1;
                case "server": return 2;
                case "host":
                case "clientandserver":
                case "clientserver":
                    return 3;
                default: return 3;
            }
        }

        /// <summary>
        /// Get CurrentPlayer info and MPPM package version.
        /// </summary>
        public static object GetMultiplayerInfo(Dictionary<string, object> args)
        {
            InitializeReflection();

            var result = new Dictionary<string, object>();
            result["mppmAvailable"] = _mppmAvailable;

            // Get MPPM package version from package.json
            string mppmVersion = "unknown";
            try
            {
                var packageJsonPath = "Packages/com.unity.multiplayer.playmode/package.json";
                if (System.IO.File.Exists(packageJsonPath))
                {
                    var json = System.IO.File.ReadAllText(packageJsonPath);
                    var match = System.Text.RegularExpressions.Regex.Match(json, @"""version""\s*:\s*""([^""]+)""");
                    if (match.Success)
                        mppmVersion = match.Groups[1].Value;
                }
                else
                {
                    mppmVersion = "not installed";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VMUnityAutomation] Could not read MPPM version: {ex.Message}");
            }

            result["mppmVersion"] = mppmVersion;

            // Get CurrentPlayer info (this type is public, so Public binding flag works)
            if (_currentPlayerType != null)
            {
                try
                {
                    var isMainEditorProperty = _currentPlayerType.GetProperty("IsMainEditor",
                        BindingFlags.Static | BindingFlags.Public);

                    var readOnlyTagsMethod = _currentPlayerType.GetMethod("ReadOnlyTags",
                        BindingFlags.Static | BindingFlags.Public);

                    if (isMainEditorProperty != null)
                        result["isMainEditor"] = isMainEditorProperty.GetValue(null) ?? false;

                    if (readOnlyTagsMethod != null)
                    {
                        var tags = readOnlyTagsMethod.Invoke(null, null);
                        if (tags is System.Collections.IEnumerable enumerable)
                        {
                            result["tags"] = enumerable.Cast<object>().Select(t => t.ToString()).ToList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    result["currentPlayerError"] = ex.Message;
                }
            }

            return result;
        }
    }
}
