using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationAnimationCommandUtility;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for managing Animator Controllers, Animation Clips, States, Transitions, and Parameters.
    /// </summary>
    public static class VmAutomationAnimationCommands
    {
        // ─── Animator Controller ───

        public static object CreateController(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
                return new { error = "path is required (e.g. 'Assets/Animations/PlayerController.controller')" };

            // Ensure directory exists
            string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller == null)
                return new { error = "Failed to create animator controller" };

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "name", controller.name },
                { "layers", controller.layers.Length },
                { "parameters", controller.parameters.Length },
            };
        }

        public static object GetControllerInfo(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            var layers = new List<Dictionary<string, object>>();
            for (int i = 0; i < controller.layers.Length; i++)
            {
                var layer = controller.layers[i];
                var states = new List<Dictionary<string, object>>();
                foreach (var state in layer.stateMachine.states)
                {
                    states.Add(new Dictionary<string, object>
                    {
                        { "name", state.state.name },
                        { "nameHash", state.state.nameHash },
                        { "speed", state.state.speed },
                        { "motion", state.state.motion != null ? state.state.motion.name : null },
                        { "position", new Dictionary<string, object> { { "x", state.position.x }, { "y", state.position.y } } },
                        { "isDefault", layer.stateMachine.defaultState == state.state },
                        { "transitionCount", state.state.transitions.Length },
                    });
                }

                var subStateMachines = new List<string>();
                foreach (var sub in layer.stateMachine.stateMachines)
                    subStateMachines.Add(sub.stateMachine.name);

                layers.Add(new Dictionary<string, object>
                {
                    { "name", layer.name },
                    { "index", i },
                    { "weight", layer.defaultWeight },
                    { "blendingMode", layer.blendingMode.ToString() },
                    { "states", states },
                    { "subStateMachines", subStateMachines },
                    { "defaultState", layer.stateMachine.defaultState != null ? layer.stateMachine.defaultState.name : null },
                    { "anyStateTransitionCount", layer.stateMachine.anyStateTransitions.Length },
                });
            }

            var parameters = new List<Dictionary<string, object>>();
            foreach (var param in controller.parameters)
            {
                var paramInfo = new Dictionary<string, object>
                {
                    { "name", param.name },
                    { "type", param.type.ToString() },
                };
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Float:
                        paramInfo["defaultValue"] = param.defaultFloat;
                        break;
                    case AnimatorControllerParameterType.Int:
                        paramInfo["defaultValue"] = param.defaultInt;
                        break;
                    case AnimatorControllerParameterType.Bool:
                        paramInfo["defaultValue"] = param.defaultBool;
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        paramInfo["defaultValue"] = false;
                        break;
                }
                parameters.Add(paramInfo);
            }

            return new Dictionary<string, object>
            {
                { "name", controller.name },
                { "path", path },
                { "layerCount", controller.layers.Length },
                { "parameterCount", controller.parameters.Length },
                { "layers", layers },
                { "parameters", parameters },
            };
        }

        public static object ValidateController(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            if (!TryGetLayerStateMachine(controller, layerIndex, out var stateMachine, out var error))
                return new { error };

            var issues = new List<Dictionary<string, object>>();
            ValidateParameters(controller, args, issues);
            ValidateStates(stateMachine, args, issues);
            ValidateTransitions(stateMachine, args, issues);
            ValidateStateMesh(stateMachine, args, issues);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "valid", issues.Count == 0 },
                { "controllerPath", path },
                { "layerIndex", layerIndex },
                { "issueCount", issues.Count },
                { "issues", issues },
            };
        }

        // ─── Parameters ───

        public static object AddParameter(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string paramName = args.ContainsKey("parameterName") ? args["parameterName"].ToString() : "";
            string paramType = args.ContainsKey("parameterType") ? args["parameterType"].ToString() : "Float";

            if (string.IsNullOrEmpty(paramName))
                return new { error = "parameterName is required" };

            AnimatorControllerParameterType type;
            if (!Enum.TryParse(paramType, true, out type))
                return new { error = $"Invalid parameter type: {paramType}. Use Float, Int, Bool, or Trigger." };

            controller.AddParameter(paramName, type);

            // Set default value if provided
            if (args.ContainsKey("defaultValue"))
            {
                var parameters = controller.parameters;
                var param = parameters[parameters.Length - 1];
                switch (type)
                {
                    case AnimatorControllerParameterType.Float:
                        param.defaultFloat = Convert.ToSingle(args["defaultValue"]);
                        break;
                    case AnimatorControllerParameterType.Int:
                        param.defaultInt = Convert.ToInt32(args["defaultValue"]);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        param.defaultBool = Convert.ToBoolean(args["defaultValue"]);
                        break;
                }
                controller.parameters = parameters;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new { success = true, controllerPath = path, parameterName = paramName, parameterType = paramType };
        }

        public static object RemoveParameter(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string paramName = args.ContainsKey("parameterName") ? args["parameterName"].ToString() : "";
            if (string.IsNullOrEmpty(paramName))
                return new { error = "parameterName is required" };

            var parameters = controller.parameters.ToList();
            int index = parameters.FindIndex(p => p.name == paramName);
            if (index < 0)
                return new { error = $"Parameter '{paramName}' not found" };

            controller.RemoveParameter(index);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new { success = true, removed = paramName };
        }

        // ─── States ───

        public static object AddState(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string stateName = args.ContainsKey("stateName") ? args["stateName"].ToString() : "";
            if (string.IsNullOrEmpty(stateName))
                return new { error = "stateName is required" };

            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            if (layerIndex >= controller.layers.Length)
                return new { error = $"Layer index {layerIndex} out of range (count: {controller.layers.Length})" };

            var stateMachine = controller.layers[layerIndex].stateMachine;
            var state = stateMachine.AddState(stateName);

            // Set speed
            if (args.ContainsKey("speed"))
                state.speed = Convert.ToSingle(args["speed"]);

            // Assign animation clip if provided
            if (args.ContainsKey("clipPath"))
            {
                string clipPath = args["clipPath"].ToString();
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip != null) state.motion = clip;
            }

            // Set as default if requested
            if (args.ContainsKey("isDefault") && Convert.ToBoolean(args["isDefault"]))
                stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "stateName", state.name },
                { "nameHash", state.nameHash },
                { "layerIndex", layerIndex },
                { "isDefault", stateMachine.defaultState == state },
            };
        }

        public static object RemoveState(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string stateName = args.ContainsKey("stateName") ? args["stateName"].ToString() : "";
            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;

            var stateMachine = controller.layers[layerIndex].stateMachine;
            var stateEntry = stateMachine.states.FirstOrDefault(s => s.state.name == stateName);
            if (stateEntry.state == null)
                return new { error = $"State '{stateName}' not found in layer {layerIndex}" };

            stateMachine.RemoveState(stateEntry.state);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new { success = true, removed = stateName, layerIndex };
        }

        public static object UpdateState(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string stateName = args.ContainsKey("stateName") ? args["stateName"].ToString() : "";
            if (string.IsNullOrEmpty(stateName))
                return new { error = "stateName is required" };

            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            if (!TryGetLayerStateMachine(controller, layerIndex, out var stateMachine, out var error))
                return new { error };

            var states = stateMachine.states;
            int stateIndex = Array.FindIndex(states, s => s.state != null && s.state.name == stateName);
            if (stateIndex < 0)
                return new { error = $"State '{stateName}' not found in layer {layerIndex}" };

            var childState = states[stateIndex];
            var state = childState.state;

            if (args.ContainsKey("newStateName"))
                state.name = args["newStateName"].ToString();
            if (args.ContainsKey("speed"))
                state.speed = Convert.ToSingle(args["speed"]);
            if (args.ContainsKey("tag"))
                state.tag = args["tag"].ToString();
            if (args.ContainsKey("writeDefaultValues"))
                state.writeDefaultValues = Convert.ToBoolean(args["writeDefaultValues"]);
            if (args.ContainsKey("mirror"))
                state.mirror = Convert.ToBoolean(args["mirror"]);
            if (args.ContainsKey("iKOnFeet"))
                state.iKOnFeet = Convert.ToBoolean(args["iKOnFeet"]);
            if (args.ContainsKey("cycleOffset"))
                state.cycleOffset = Convert.ToSingle(args["cycleOffset"]);

            if (args.ContainsKey("position"))
            {
                childState.position = ParseVector2(args["position"]);
                states[stateIndex] = childState;
                stateMachine.states = states;
            }

            if (args.ContainsKey("clearMotion") && Convert.ToBoolean(args["clearMotion"]))
            {
                state.motion = null;
            }
            else
            {
                string motionPath = args.ContainsKey("motionPath")
                    ? args["motionPath"].ToString()
                    : "";

                if (!string.IsNullOrEmpty(motionPath))
                {
                    var motion = AssetDatabase.LoadAssetAtPath<Motion>(motionPath);
                    if (motion == null)
                        return new { error = $"Motion not found at '{motionPath}'" };

                    state.motion = motion;
                }
            }

            if (args.ContainsKey("isDefault") && Convert.ToBoolean(args["isDefault"]))
                stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return BuildStateInfo(stateMachine, state, childState.position, layerIndex);
        }

        // ─── Transitions ───

        public static object AddTransition(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string sourceName = args.ContainsKey("sourceState") ? args["sourceState"].ToString() : "";
            string destName = args.ContainsKey("destinationState") ? args["destinationState"].ToString() : "";
            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            bool fromAnyState = args.ContainsKey("fromAnyState") && Convert.ToBoolean(args["fromAnyState"]);

            var stateMachine = controller.layers[layerIndex].stateMachine;

            AnimatorState destState = null;
            if (!string.IsNullOrEmpty(destName))
            {
                var destEntry = stateMachine.states.FirstOrDefault(s => s.state.name == destName);
                destState = destEntry.state;
                if (destState == null)
                    return new { error = $"Destination state '{destName}' not found" };
            }

            AnimatorState sourceState = null;
            AnimatorStateTransition transition;

            if (fromAnyState)
            {
                transition = stateMachine.AddAnyStateTransition(destState);
            }
            else
            {
                if (string.IsNullOrEmpty(sourceName))
                    return new { error = "sourceState is required (or set fromAnyState to true)" };

                var sourceEntry = stateMachine.states.FirstOrDefault(s => s.state.name == sourceName);
                if (sourceEntry.state == null)
                    return new { error = $"Source state '{sourceName}' not found" };

                sourceState = sourceEntry.state;
                transition = sourceState.AddTransition(destState);
            }

            ApplyTransitionSettings(transition, args, true);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            int index = fromAnyState
                ? Array.IndexOf(stateMachine.anyStateTransitions, transition)
                : Array.IndexOf(sourceState.transitions, transition);

            return BuildTransitionInfo(transition, fromAnyState ? "AnyState" : sourceName,
                fromAnyState ? "AnyState" : "State", index);
        }

        public static object GetTransitionInfo(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            if (!TryGetLayerStateMachine(controller, layerIndex, out var stateMachine, out var error))
                return new { error };

            string sourceName = args.ContainsKey("sourceState") ? args["sourceState"].ToString() : "";
            string destName = args.ContainsKey("destinationState") ? args["destinationState"].ToString() : "";
            bool hasFromAnyState = args.ContainsKey("fromAnyState");
            bool fromAnyState = hasFromAnyState && Convert.ToBoolean(args["fromAnyState"]);
            int transitionIndex = args.ContainsKey("transitionIndex") ? Convert.ToInt32(args["transitionIndex"]) : -1;

            var transitions = new List<Dictionary<string, object>>();

            if (!hasFromAnyState || fromAnyState)
            {
                var anyTransitions = stateMachine.anyStateTransitions;
                for (int i = 0; i < anyTransitions.Length; i++)
                {
                    if (transitionIndex >= 0 && i != transitionIndex)
                        continue;
                    if (!string.IsNullOrEmpty(destName) && !TransitionDestinationMatches(anyTransitions[i], destName))
                        continue;

                    transitions.Add(BuildTransitionInfo(anyTransitions[i], "AnyState", "AnyState", i));
                }
            }

            if (!hasFromAnyState || !fromAnyState)
            {
                foreach (var childState in stateMachine.states)
                {
                    if (childState.state == null)
                        continue;
                    if (!string.IsNullOrEmpty(sourceName) && childState.state.name != sourceName)
                        continue;

                    var stateTransitions = childState.state.transitions;
                    for (int i = 0; i < stateTransitions.Length; i++)
                    {
                        if (transitionIndex >= 0 && i != transitionIndex)
                            continue;
                        if (!string.IsNullOrEmpty(destName) && !TransitionDestinationMatches(stateTransitions[i], destName))
                            continue;

                        transitions.Add(BuildTransitionInfo(stateTransitions[i], childState.state.name, "State", i));
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "controllerPath", path },
                { "layerIndex", layerIndex },
                { "count", transitions.Count },
                { "transitions", transitions },
            };
        }

        public static object UpdateTransition(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            string sourceName = args.ContainsKey("sourceState") ? args["sourceState"].ToString() : "";
            string destName = args.ContainsKey("destinationState") ? args["destinationState"].ToString() : "";
            bool fromAnyState = args.ContainsKey("fromAnyState") && Convert.ToBoolean(args["fromAnyState"]);
            int transitionIndex = args.ContainsKey("transitionIndex") ? Convert.ToInt32(args["transitionIndex"]) : -1;

            if (!TryFindTransition(controller, layerIndex, sourceName, destName, fromAnyState, transitionIndex,
                    out var transition, out var resolvedSource, out var resolvedSourceType, out var resolvedIndex,
                    out var error))
            {
                return new { error };
            }

            ApplyTransitionSettings(transition, args, false);
            ApplyTransitionConditionEdits(transition, args);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return BuildTransitionInfo(transition, resolvedSource, resolvedSourceType, resolvedIndex);
        }

        public static object ConnectStates(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            if (!TryGetLayerStateMachine(controller, layerIndex, out var stateMachine, out var error))
                return new { error };

            var stateNames = GetStringList(args, "stateNames");
            if (stateNames.Count < 2)
                return new { error = "stateNames must contain at least two states" };

            bool skipExisting = !args.ContainsKey("skipExisting") || Convert.ToBoolean(args["skipExisting"]);
            bool replaceExisting = args.ContainsKey("replaceExisting") && Convert.ToBoolean(args["replaceExisting"]);

            var states = new Dictionary<string, AnimatorState>();
            foreach (string stateName in stateNames)
            {
                var stateEntry = stateMachine.states.FirstOrDefault(s => s.state != null && s.state.name == stateName);
                if (stateEntry.state == null)
                    return new { error = $"State '{stateName}' not found in layer {layerIndex}" };

                states[stateName] = stateEntry.state;
            }

            var created = new List<Dictionary<string, object>>();
            var skipped = new List<Dictionary<string, object>>();

            foreach (string sourceName in stateNames)
            {
                foreach (string destName in stateNames)
                {
                    if (sourceName == destName)
                        continue;

                    var source = states[sourceName];
                    var destination = states[destName];
                    var existing = source.transitions
                        .Where(t => t.destinationState == destination)
                        .ToArray();

                    if (existing.Length > 0)
                    {
                        if (replaceExisting)
                        {
                            foreach (var transition in existing)
                                source.RemoveTransition(transition);
                        }
                        else if (skipExisting)
                        {
                            skipped.Add(new Dictionary<string, object>
                            {
                                { "source", sourceName },
                                { "destination", destName },
                                { "reason", "exists" },
                            });
                            continue;
                        }
                    }

                    var newTransition = source.AddTransition(destination);
                    ApplyTransitionSettings(newTransition, args, true);

                    created.Add(BuildTransitionInfo(newTransition, sourceName, "State",
                        source.transitions.Length - 1));
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "controllerPath", path },
                { "layerIndex", layerIndex },
                { "stateNames", stateNames },
                { "createdCount", created.Count },
                { "skippedCount", skipped.Count },
                { "created", created },
                { "skipped", skipped },
            };
        }

        // ─── Animation Clips ───

        public static object AddLayer(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string layerName = args.ContainsKey("layerName") ? args["layerName"].ToString() : "New Layer";

            controller.AddLayer(layerName);

            // Set weight if provided
            if (args.ContainsKey("weight"))
            {
                var layers = controller.layers;
                layers[layers.Length - 1].defaultWeight = Convert.ToSingle(args["weight"]);
                controller.layers = layers;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new { success = true, layerName, layerIndex = controller.layers.Length - 1 };
        }

        // ─── Assign Controller to GameObject ───

        public static object AssignController(Dictionary<string, object> args)
        {
            var go = VmAutomationGameObjectCommands.FindGameObject(args);
            if (go == null) return new { error = "GameObject not found" };

            string controllerPath = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            if (controller == null)
                return new { error = $"Animator controller not found at '{controllerPath}'" };

            var animator = go.GetComponent<Animator>();
            if (animator == null)
                animator = Undo.AddComponent<Animator>(go);

            Undo.RecordObject(animator, "Assign Animator Controller");
            animator.runtimeAnimatorController = controller;

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "controller", controller.name },
            };
        }

        // ─── Keyframe Detail Operations ───

        public static object RemoveTransition(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string sourceName = args.ContainsKey("sourceState") ? args["sourceState"].ToString() : "";
            string destName = args.ContainsKey("destinationState") ? args["destinationState"].ToString() : "";
            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            bool fromAnyState = args.ContainsKey("fromAnyState") && Convert.ToBoolean(args["fromAnyState"]);
            int transitionIndex = args.ContainsKey("transitionIndex") ? Convert.ToInt32(args["transitionIndex"]) : -1;

            var stateMachine = controller.layers[layerIndex].stateMachine;

            if (fromAnyState)
            {
                var transitions = stateMachine.anyStateTransitions;
                AnimatorStateTransition toRemove = null;

                if (transitionIndex >= 0 && transitionIndex < transitions.Length)
                {
                    toRemove = transitions[transitionIndex];
                }
                else if (!string.IsNullOrEmpty(destName))
                {
                    toRemove = transitions.FirstOrDefault(t => t.destinationState != null && t.destinationState.name == destName);
                }

                if (toRemove == null)
                    return new { error = "AnyState transition not found" };

                stateMachine.RemoveAnyStateTransition(toRemove);
            }
            else
            {
                if (string.IsNullOrEmpty(sourceName))
                    return new { error = "sourceState is required (or set fromAnyState to true)" };

                var sourceEntry = stateMachine.states.FirstOrDefault(s => s.state.name == sourceName);
                if (sourceEntry.state == null)
                    return new { error = $"Source state '{sourceName}' not found" };

                var transitions = sourceEntry.state.transitions;
                AnimatorStateTransition toRemove = null;

                if (transitionIndex >= 0 && transitionIndex < transitions.Length)
                {
                    toRemove = transitions[transitionIndex];
                }
                else if (!string.IsNullOrEmpty(destName))
                {
                    toRemove = transitions.FirstOrDefault(t => t.destinationState != null && t.destinationState.name == destName);
                }

                if (toRemove == null)
                    return new { error = "Transition not found" };

                sourceEntry.state.RemoveTransition(toRemove);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new { success = true, source = fromAnyState ? "AnyState" : sourceName, destination = destName };
        }

        // ─── Layer Management ───

        public static object RemoveLayer(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : -1;
            if (layerIndex < 0)
                return new { error = "layerIndex is required" };
            if (layerIndex == 0)
                return new { error = "Cannot remove the base layer (index 0)" };
            if (layerIndex >= controller.layers.Length)
                return new { error = $"Layer index {layerIndex} out of range (count: {controller.layers.Length})" };

            string removedName = controller.layers[layerIndex].name;
            controller.RemoveLayer(layerIndex);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new { success = true, removedLayer = removedName, remainingLayers = controller.layers.Length };
        }

        // ─── Blend Trees ───

        public static object CreateBlendTree(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string stateName = args.ContainsKey("stateName") ? args["stateName"].ToString() : "Blend Tree";
            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;
            string blendType = args.ContainsKey("blendType") ? args["blendType"].ToString() : "Simple1D";
            string blendParameter = args.ContainsKey("blendParameter") ? args["blendParameter"].ToString() : "Blend";

            if (layerIndex >= controller.layers.Length)
                return new { error = $"Layer index {layerIndex} out of range" };

            BlendTree tree;
            var state = controller.CreateBlendTreeInController(stateName, out tree, layerIndex);

            // Set blend type
            BlendTreeType btType;
            if (Enum.TryParse(blendType, true, out btType))
                tree.blendType = btType;

            tree.blendParameter = blendParameter;
            if (args.ContainsKey("blendParameterY"))
                tree.blendParameterY = args["blendParameterY"].ToString();

            // Add motions if provided
            if (args.ContainsKey("motions"))
            {
                var motions = args["motions"] as List<object>;
                if (motions != null)
                {
                    foreach (var motionObj in motions)
                    {
                        var m = motionObj as Dictionary<string, object>;
                        if (m == null) continue;

                        string clipPath = m.ContainsKey("clipPath") ? m["clipPath"].ToString() : "";
                        float threshold = m.ContainsKey("threshold") ? Convert.ToSingle(m["threshold"]) : 0f;
                        float timeScale = m.ContainsKey("timeScale") ? Convert.ToSingle(m["timeScale"]) : 1f;

                        Motion motion = null;
                        if (!string.IsNullOrEmpty(clipPath))
                            motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

                        tree.AddChild(motion, threshold);

                        // Set time scale on the last child
                        if (timeScale != 1f)
                        {
                            var children = tree.children;
                            var child = children[children.Length - 1];
                            child.timeScale = timeScale;
                            children[children.Length - 1] = child;
                            tree.children = children;
                        }
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "controllerPath", path },
                { "stateName", state.name },
                { "blendType", tree.blendType.ToString() },
                { "blendParameter", tree.blendParameter },
                { "childCount", tree.children.Length },
            };
        }

        public static object GetBlendTreeInfo(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("controllerPath") ? args["controllerPath"].ToString() : "";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                return new { error = $"Animator controller not found at '{path}'" };

            string stateName = args.ContainsKey("stateName") ? args["stateName"].ToString() : "";
            int layerIndex = args.ContainsKey("layerIndex") ? Convert.ToInt32(args["layerIndex"]) : 0;

            if (string.IsNullOrEmpty(stateName))
                return new { error = "stateName is required" };

            var stateMachine = controller.layers[layerIndex].stateMachine;
            var stateEntry = stateMachine.states.FirstOrDefault(s => s.state.name == stateName);
            if (stateEntry.state == null)
                return new { error = $"State '{stateName}' not found" };

            var blendTree = stateEntry.state.motion as BlendTree;
            if (blendTree == null)
                return new { error = $"State '{stateName}' does not contain a blend tree" };

            var children = new List<Dictionary<string, object>>();
            for (int i = 0; i < blendTree.children.Length; i++)
            {
                var child = blendTree.children[i];
                children.Add(new Dictionary<string, object>
                {
                    { "index", i },
                    { "motion", child.motion != null ? child.motion.name : null },
                    { "motionPath", child.motion != null ? AssetDatabase.GetAssetPath(child.motion) : null },
                    { "threshold", child.threshold },
                    { "position", new Dictionary<string, object> { { "x", child.position.x }, { "y", child.position.y } } },
                    { "timeScale", child.timeScale },
                    { "isBlendTree", child.motion is BlendTree },
                });
            }

            return new Dictionary<string, object>
            {
                { "stateName", stateName },
                { "blendType", blendTree.blendType.ToString() },
                { "blendParameter", blendTree.blendParameter },
                { "blendParameterY", blendTree.blendParameterY },
                { "minThreshold", blendTree.minThreshold },
                { "maxThreshold", blendTree.maxThreshold },
                { "childCount", blendTree.children.Length },
                { "children", children.ToArray() },
            };
        }

        private static bool TryGetLayerStateMachine(AnimatorController controller, int layerIndex,
            out AnimatorStateMachine stateMachine, out string error)
        {
            stateMachine = null;
            error = null;

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
            {
                error = $"Layer index {layerIndex} out of range (count: {controller.layers.Length})";
                return false;
            }

            stateMachine = controller.layers[layerIndex].stateMachine;
            return true;
        }

        private static void ValidateParameters(AnimatorController controller, Dictionary<string, object> args,
            List<Dictionary<string, object>> issues)
        {
            foreach (var parameterObj in GetObjectList(args, "requiredParameters"))
            {
                string parameterName;
                string expectedType = "";

                if (parameterObj is Dictionary<string, object> parameterArgs)
                {
                    parameterName = parameterArgs["name"].ToString();
                    expectedType = parameterArgs.ContainsKey("type")
                        ? parameterArgs["type"].ToString()
                        : "";
                }
                else
                {
                    parameterName = parameterObj?.ToString() ?? "";
                }

                var parameter = controller.parameters.FirstOrDefault(item => item.name == parameterName);
                if (parameter == null)
                {
                    AddIssue(issues, "missing-parameter", parameterName, $"Parameter '{parameterName}' is missing.");
                    continue;
                }

                if (!string.IsNullOrEmpty(expectedType) &&
                    !string.Equals(parameter.type.ToString(), expectedType, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue(issues, "parameter-type", parameterName,
                        $"Parameter '{parameterName}' is {parameter.type}, expected {expectedType}.");
                }
            }
        }

        private static void ValidateStates(AnimatorStateMachine stateMachine, Dictionary<string, object> args,
            List<Dictionary<string, object>> issues)
        {
            foreach (string stateName in GetStringList(args, "requiredStates"))
            {
                if (FindState(stateMachine, stateName) == null)
                    AddIssue(issues, "missing-state", stateName, $"State '{stateName}' is missing.");
            }

            if (args.ContainsKey("requireMotion") && Convert.ToBoolean(args["requireMotion"]))
            {
                foreach (var childState in stateMachine.states)
                {
                    if (childState.state != null && childState.state.motion == null)
                    {
                        AddIssue(issues, "missing-motion", childState.state.name,
                            $"State '{childState.state.name}' has no motion.");
                    }
                }
            }
        }

        private static void ValidateTransitions(AnimatorStateMachine stateMachine, Dictionary<string, object> args,
            List<Dictionary<string, object>> issues)
        {
            foreach (var transitionObj in GetObjectList(args, "requiredTransitions"))
            {
                if (transitionObj is not Dictionary<string, object> transitionArgs)
                    continue;

                string sourceName = transitionArgs["source"].ToString();
                string destinationName = transitionArgs["destination"].ToString();
                string conditionParameter = transitionArgs.ContainsKey("conditionParameter")
                    ? transitionArgs["conditionParameter"].ToString()
                    : "";

                var source = FindState(stateMachine, sourceName);
                if (source == null)
                {
                    AddIssue(issues, "missing-transition-source", sourceName,
                        $"Transition source state '{sourceName}' is missing.");
                    continue;
                }

                var transition = source.transitions.FirstOrDefault(item =>
                    TransitionDestinationMatches(item, destinationName));
                if (transition == null)
                {
                    AddIssue(issues, "missing-transition", $"{sourceName}->{destinationName}",
                        $"Transition '{sourceName}' -> '{destinationName}' is missing.");
                    continue;
                }

                if (!string.IsNullOrEmpty(conditionParameter) &&
                    transition.conditions.All(condition => condition.parameter != conditionParameter))
                {
                    AddIssue(issues, "missing-transition-condition", $"{sourceName}->{destinationName}",
                        $"Transition '{sourceName}' -> '{destinationName}' has no condition '{conditionParameter}'.");
                }
            }
        }

        private static void ValidateStateMesh(AnimatorStateMachine stateMachine, Dictionary<string, object> args,
            List<Dictionary<string, object>> issues)
        {
            bool requireFullMesh = args.ContainsKey("requireFullMesh") && Convert.ToBoolean(args["requireFullMesh"]);
            if (!requireFullMesh)
                requireFullMesh = args.ContainsKey("requireMutualTransitions") &&
                                  Convert.ToBoolean(args["requireMutualTransitions"]);
            if (!requireFullMesh)
                return;

            var stateNames = GetStringList(args, "stateNames");
            if (stateNames.Count == 0)
                stateNames = stateMachine.states
                    .Where(item => item.state != null)
                    .Select(item => item.state.name)
                    .ToList();

            foreach (string sourceName in stateNames)
            {
                var source = FindState(stateMachine, sourceName);
                if (source == null)
                    continue;

                foreach (string destinationName in stateNames)
                {
                    if (sourceName == destinationName)
                        continue;

                    if (source.transitions.Any(transition =>
                            TransitionDestinationMatches(transition, destinationName)) == false)
                    {
                        AddIssue(issues, "missing-mutual-transition", $"{sourceName}->{destinationName}",
                            $"Transition '{sourceName}' -> '{destinationName}' is missing.");
                    }
                }
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
                return null;

            return stateMachine.states
                .Select(item => item.state)
                .FirstOrDefault(state => state != null && state.name == stateName);
        }

        private static void AddIssue(List<Dictionary<string, object>> issues, string type, string target,
            string message)
        {
            issues.Add(new Dictionary<string, object>
            {
                { "type", type },
                { "target", target },
                { "message", message },
            });
        }

        private static Dictionary<string, object> BuildStateInfo(AnimatorStateMachine stateMachine,
            AnimatorState state, Vector3 position, int layerIndex)
        {
            return new Dictionary<string, object>
            {
                { "success", true },
                { "layerIndex", layerIndex },
                { "stateName", state.name },
                { "nameHash", state.nameHash },
                { "motion", state.motion != null ? state.motion.name : null },
                { "motionPath", state.motion != null ? AssetDatabase.GetAssetPath(state.motion) : null },
                { "speed", state.speed },
                { "tag", state.tag },
                { "writeDefaultValues", state.writeDefaultValues },
                { "mirror", state.mirror },
                { "iKOnFeet", state.iKOnFeet },
                { "cycleOffset", state.cycleOffset },
                { "position", new Dictionary<string, object> { { "x", position.x }, { "y", position.y } } },
                { "isDefault", stateMachine.defaultState == state },
            };
        }

        private static Vector2 ParseVector2(object value)
        {
            if (value is Dictionary<string, object> d)
            {
                return new Vector2(
                    d.ContainsKey("x") ? Convert.ToSingle(d["x"]) : 0f,
                    d.ContainsKey("y") ? Convert.ToSingle(d["y"]) : 0f
                );
            }

            return Vector2.zero;
        }

        private static void ApplyTransitionSettings(AnimatorStateTransition transition,
            Dictionary<string, object> args, bool includeConditions)
        {
            if (args.ContainsKey("hasExitTime"))
                transition.hasExitTime = Convert.ToBoolean(args["hasExitTime"]);
            if (args.ContainsKey("exitTime"))
                transition.exitTime = Convert.ToSingle(args["exitTime"]);
            if (args.ContainsKey("duration"))
                transition.duration = Convert.ToSingle(args["duration"]);
            if (args.ContainsKey("offset"))
                transition.offset = Convert.ToSingle(args["offset"]);
            if (args.ContainsKey("hasFixedDuration"))
                transition.hasFixedDuration = Convert.ToBoolean(args["hasFixedDuration"]);
            if (args.ContainsKey("interruptionSource"))
            {
                if (Enum.TryParse(args["interruptionSource"].ToString(), true,
                        out TransitionInterruptionSource interruptionSource))
                    transition.interruptionSource = interruptionSource;
            }
            if (args.ContainsKey("orderedInterruption"))
                transition.orderedInterruption = Convert.ToBoolean(args["orderedInterruption"]);
            if (args.ContainsKey("canTransitionToSelf"))
                transition.canTransitionToSelf = Convert.ToBoolean(args["canTransitionToSelf"]);
            if (args.ContainsKey("mute"))
                transition.mute = Convert.ToBoolean(args["mute"]);
            if (args.ContainsKey("solo"))
                transition.solo = Convert.ToBoolean(args["solo"]);

            if (includeConditions && args.ContainsKey("conditions"))
                ReplaceConditions(transition, ParseConditions(args["conditions"]));
        }

        private static bool TryFindTransition(AnimatorController controller, int layerIndex, string sourceName,
            string destName, bool fromAnyState, int transitionIndex, out AnimatorStateTransition transition,
            out string resolvedSource, out string resolvedSourceType, out int resolvedIndex, out string error)
        {
            transition = null;
            resolvedSource = null;
            resolvedSourceType = null;
            resolvedIndex = -1;
            error = null;

            if (!TryGetLayerStateMachine(controller, layerIndex, out var stateMachine, out error))
                return false;

            if (fromAnyState)
            {
                var transitions = stateMachine.anyStateTransitions;
                transition = FindTransitionInArray(transitions, destName, transitionIndex, out resolvedIndex);
                if (transition == null)
                {
                    error = "AnyState transition not found";
                    return false;
                }

                resolvedSource = "AnyState";
                resolvedSourceType = "AnyState";
                return true;
            }

            if (string.IsNullOrEmpty(sourceName))
            {
                error = "sourceState is required unless fromAnyState is true";
                return false;
            }

            var sourceEntry = stateMachine.states.FirstOrDefault(s => s.state != null && s.state.name == sourceName);
            if (sourceEntry.state == null)
            {
                error = $"Source state '{sourceName}' not found";
                return false;
            }

            transition = FindTransitionInArray(sourceEntry.state.transitions, destName, transitionIndex,
                out resolvedIndex);
            if (transition == null)
            {
                error = "Transition not found";
                return false;
            }

            resolvedSource = sourceName;
            resolvedSourceType = "State";
            return true;
        }

        private static AnimatorStateTransition FindTransitionInArray(AnimatorStateTransition[] transitions,
            string destName, int transitionIndex, out int resolvedIndex)
        {
            resolvedIndex = -1;

            if (transitionIndex >= 0)
            {
                if (transitionIndex < transitions.Length)
                {
                    resolvedIndex = transitionIndex;
                    return transitions[transitionIndex];
                }

                return null;
            }

            for (int i = 0; i < transitions.Length; i++)
            {
                if (string.IsNullOrEmpty(destName) || TransitionDestinationMatches(transitions[i], destName))
                {
                    resolvedIndex = i;
                    return transitions[i];
                }
            }

            return null;
        }

        private static bool TransitionDestinationMatches(AnimatorStateTransition transition, string destName)
        {
            return transition.destinationState != null && transition.destinationState.name == destName ||
                   transition.destinationStateMachine != null && transition.destinationStateMachine.name == destName ||
                   transition.isExit && string.Equals(destName, "Exit", StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, object> BuildTransitionInfo(AnimatorStateTransition transition,
            string source, string sourceType, int transitionIndex)
        {
            var conditions = new List<Dictionary<string, object>>();
            for (int i = 0; i < transition.conditions.Length; i++)
            {
                var condition = transition.conditions[i];
                conditions.Add(new Dictionary<string, object>
                {
                    { "index", i },
                    { "parameter", condition.parameter },
                    { "mode", condition.mode.ToString() },
                    { "threshold", condition.threshold },
                });
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "index", transitionIndex },
                { "name", transition.name },
                { "source", source },
                { "sourceType", sourceType },
                { "destinationState", transition.destinationState != null ? transition.destinationState.name : null },
                { "destinationStateMachine", transition.destinationStateMachine != null ? transition.destinationStateMachine.name : null },
                { "isExit", transition.isExit },
                { "mute", transition.mute },
                { "solo", transition.solo },
                { "hasExitTime", transition.hasExitTime },
                { "exitTime", transition.exitTime },
                { "duration", transition.duration },
                { "offset", transition.offset },
                { "hasFixedDuration", transition.hasFixedDuration },
                { "interruptionSource", transition.interruptionSource.ToString() },
                { "orderedInterruption", transition.orderedInterruption },
                { "canTransitionToSelf", transition.canTransitionToSelf },
                { "conditionCount", transition.conditions.Length },
                { "conditions", conditions },
            };
        }

        private static void ApplyTransitionConditionEdits(AnimatorStateTransition transition,
            Dictionary<string, object> args)
        {
            if (args.ContainsKey("conditions"))
            {
                ReplaceConditions(transition, ParseConditions(args["conditions"]));
                return;
            }

            bool changed = false;
            var conditions = transition.conditions.ToList();

            if (args.ContainsKey("removeConditionIndexes"))
            {
                var indexes = GetIntList(args, "removeConditionIndexes");
                indexes.Sort();
                indexes.Reverse();
                foreach (int index in indexes)
                {
                    if (index >= 0 && index < conditions.Count)
                    {
                        conditions.RemoveAt(index);
                        changed = true;
                    }
                }
            }

            if (args.ContainsKey("updateConditions"))
            {
                foreach (var conditionObj in GetObjectList(args, "updateConditions"))
                {
                    var conditionArgs = conditionObj as Dictionary<string, object>;
                    if (conditionArgs == null || !conditionArgs.ContainsKey("index"))
                        continue;

                    int index = Convert.ToInt32(conditionArgs["index"]);
                    if (index < 0 || index >= conditions.Count)
                        continue;

                    var condition = conditions[index];
                    if (conditionArgs.ContainsKey("parameter"))
                        condition.parameter = conditionArgs["parameter"].ToString();
                    if (conditionArgs.ContainsKey("mode") &&
                        Enum.TryParse(conditionArgs["mode"].ToString(), true, out AnimatorConditionMode mode))
                        condition.mode = mode;
                    if (conditionArgs.ContainsKey("threshold"))
                        condition.threshold = Convert.ToSingle(conditionArgs["threshold"]);

                    conditions[index] = condition;
                    changed = true;
                }
            }

            if (args.ContainsKey("addConditions"))
            {
                conditions.AddRange(ParseConditions(args["addConditions"]));
                changed = true;
            }

            if (changed)
                ReplaceConditions(transition, conditions);
        }

        private static List<AnimatorCondition> ParseConditions(object value)
        {
            var conditions = new List<AnimatorCondition>();
            var conditionObjects = value as List<object>;
            if (conditionObjects == null)
                return conditions;

            foreach (var conditionObj in conditionObjects)
            {
                var conditionArgs = conditionObj as Dictionary<string, object>;
                if (conditionArgs == null)
                    continue;

                var condition = new AnimatorCondition
                {
                    parameter = conditionArgs.ContainsKey("parameter") ? conditionArgs["parameter"].ToString() : "",
                    threshold = conditionArgs.ContainsKey("threshold")
                        ? Convert.ToSingle(conditionArgs["threshold"])
                        : 0f,
                    mode = AnimatorConditionMode.If,
                };

                if (conditionArgs.ContainsKey("mode") &&
                    Enum.TryParse(conditionArgs["mode"].ToString(), true, out AnimatorConditionMode mode))
                    condition.mode = mode;

                conditions.Add(condition);
            }

            return conditions;
        }

        private static void ReplaceConditions(AnimatorStateTransition transition,
            IEnumerable<AnimatorCondition> conditions)
        {
            foreach (var condition in transition.conditions.ToArray())
                transition.RemoveCondition(condition);

            foreach (var condition in conditions)
                transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }

    }
}
