using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPrefabComponentTypeAdmission
    {
        internal static bool TryValidate(
            string assetPath, IReadOnlyList<string> componentTypes, out object error)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                error = VmAutomationResponse.Error(
                    "Prefab editing requires an idle Editor with imported component types.",
                    "editor_not_stable", false,
                    new Dictionary<string, object>
                    {
                        { "assetPath", assetPath },
                        { "isCompiling", EditorApplication.isCompiling },
                        { "isUpdating", EditorApplication.isUpdating }
                    });
                return false;
            }

            foreach (string name in componentTypes)
            {
                Type type = VmAutomationComponentCommands.FindType(name);
                if (type == null || !typeof(Component).IsAssignableFrom(type))
                {
                    error = VmAutomationResponse.Error(
                        $"Component type '{name}' is not available in the current Editor domain. " +
                        "Import and compile its source explicitly before editing the prefab.",
                        "component_type_not_found", false,
                        new Dictionary<string, object>
                        {
                            { "assetPath", assetPath },
                            { "componentType", name },
                            { "stage", "component-type-admission" }
                        });
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
