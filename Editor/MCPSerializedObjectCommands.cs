using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class MCPSerializedObjectCommands
    {
        public static object Get(Dictionary<string, object> args)
        {
            if (!TryResolveTarget(args, out var target, out string error))
                return new { error };

            string propertyPath = GetString(args, "propertyPath");
            int maxProperties = Math.Max(1, Math.Min(GetInt(args, "maxProperties", 50), 500));
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int maxDepth = Math.Max(1, Math.Min(GetInt(args, "maxDepth", 3), 8));
            int maxArrayElements = Math.Max(1, Math.Min(GetInt(args, "maxArrayElements", 50), 500));
            bool includeChildren = GetBool(args, "includeChildren", false);

            var serialized = new SerializedObject(target);
            var result = BuildTargetResult(target);

            if (!string.IsNullOrEmpty(propertyPath))
            {
                var property = serialized.FindProperty(propertyPath);
                if (property == null)
                    return new { error = $"Property '{propertyPath}' was not found on '{target.GetType().Name}'" };

                result["property"] = BuildPropertyInfo(property, maxDepth, maxArrayElements);
                return result;
            }

            var properties = new List<Dictionary<string, object>>();
            int totalProperties = 0;
            var iterator = serialized.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (totalProperties >= offset && properties.Count < maxProperties)
                        properties.Add(BuildPropertyInfo(iterator, maxDepth, maxArrayElements));
                    totalProperties++;
                } while (iterator.NextVisible(includeChildren));
            }

            result["propertyCount"] = properties.Count;
            result["totalProperties"] = totalProperties;
            result["offset"] = offset;
            result["limit"] = maxProperties;
            int nextOffset = offset + properties.Count;
            result["truncated"] = nextOffset < totalProperties;
            result["hasMore"] = nextOffset < totalProperties;
            result["nextOffset"] = nextOffset < totalProperties ? (object)nextOffset : null;
            result["maxDepth"] = maxDepth;
            result["maxArrayElements"] = maxArrayElements;
            result["properties"] = properties;
            return result;
        }

        public static object Set(Dictionary<string, object> args)
        {
            return Set(args, saveOnlyTarget: false);
        }

        internal static object SetForTransaction(Dictionary<string, object> args)
        {
            return Set(args, saveOnlyTarget: true);
        }

        private static object Set(Dictionary<string, object> args, bool saveOnlyTarget)
        {
            if (!TryResolveTarget(args, out var target, out string error))
                return new { error };

            string propertyPath = GetString(args, "propertyPath");
            if (string.IsNullOrEmpty(propertyPath))
                return new { error = "propertyPath is required" };
            if (args == null || !args.ContainsKey("value"))
                return new { error = "value is required" };

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyPath);
            if (property == null)
                return new { error = $"Property '{propertyPath}' was not found on '{target.GetType().Name}'" };

            int maxDepth = Math.Max(1, Math.Min(GetInt(args, "maxDepth", 3), 8));
            int maxArrayElements = Math.Max(1, Math.Min(GetInt(args, "maxArrayElements", 50), 500));
            var beforeValue = MCPComponentCommands.GetSerializedValue(property, maxDepth, maxArrayElements);
            try
            {
                MCPComponentCommands.SetSerializedValue(property, args["value"]);
            }
            catch (Exception exception)
            {
                serialized.Update();
                return MCPResponse.Error(exception.Message, "serialized_object_set_failed", false,
                    new Dictionary<string, object> { { "propertyPath", propertyPath } });
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            if (saveOnlyTarget)
                AssetDatabase.SaveAssetIfDirty(target);
            else
                AssetDatabase.SaveAssets();

            serialized.Update();
            property = serialized.FindProperty(propertyPath);

            var result = BuildTargetResult(target);
            result["success"] = true;
            result["propertyPath"] = propertyPath;
            result["beforeValue"] = beforeValue;
            result["afterValue"] = property == null
                ? null
                : MCPComponentCommands.GetSerializedValue(property, maxDepth, maxArrayElements);
            result["property"] = property == null ? null : BuildPropertyInfo(property, maxDepth, maxArrayElements);
            return result;
        }

        private static bool TryResolveTarget(Dictionary<string, object> args, out UnityEngine.Object target,
            out string error)
        {
            target = null;
            error = "";

            if (TryGetObjectId(args, "instanceId", out object instanceId))
            {
                target = VmObjectId.ToObject(instanceId);
                if (target != null)
                    return true;

                error = $"Object instanceId '{instanceId}' was not found";
                return false;
            }

            string gameObjectPath = GetString(args, "gameObjectPath");
            if (string.IsNullOrEmpty(gameObjectPath) == false)
            {
                var gameObject = GameObject.Find(gameObjectPath);
                if (gameObject == null)
                {
                    error = $"GameObject '{gameObjectPath}' was not found";
                    return false;
                }

                target = ResolveComponentOrGameObject(gameObject, args, out error);
                return target != null;
            }

            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath) == false)
            {
                target = LoadAssetTarget(assetPath, args, out error);
                return target != null;
            }

            error = "Provide instanceId, gameObjectPath, or assetPath";
            return false;
        }

        private static UnityEngine.Object LoadAssetTarget(string assetPath, Dictionary<string, object> args,
            out string error)
        {
            error = "";
            Type assetType = ResolveType(GetString(args, "assetType"));
            var asset = assetType == null
                ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)
                : AssetDatabase.LoadAssetAtPath(assetPath, assetType);

            if (asset == null)
            {
                error = $"Asset '{assetPath}' was not found";
                return null;
            }

            if (asset is GameObject gameObject)
                return ResolveComponentOrGameObject(gameObject, args, out error);

            return asset;
        }

        private static UnityEngine.Object ResolveComponentOrGameObject(GameObject gameObject,
            Dictionary<string, object> args, out string error)
        {
            error = "";
            string componentTypeName = GetString(args, "componentType");
            if (string.IsNullOrEmpty(componentTypeName))
                return gameObject;

            Type componentType = MCPComponentCommands.FindType(componentTypeName);
            if (componentType == null)
            {
                error = $"Component type '{componentTypeName}' was not found";
                return null;
            }

            int componentIndex = GetInt(args, "componentIndex", 0);
            var components = gameObject.GetComponents(componentType);
            if (componentIndex < 0 || componentIndex >= components.Length)
            {
                error = $"Component '{componentTypeName}' at index {componentIndex} was not found on '{gameObject.name}'";
                return null;
            }

            return components[componentIndex];
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            return MCPComponentCommands.FindType(typeName) ??
                   AppDomain.CurrentDomain.GetAssemblies()
                       .SelectMany(GetLoadableTypes)
                       .FirstOrDefault(type => string.Equals(type.FullName, typeName, StringComparison.Ordinal) ||
                                               string.Equals(type.Name, typeName, StringComparison.Ordinal));
        }

        private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static Dictionary<string, object> BuildTargetResult(UnityEngine.Object target)
        {
            return new Dictionary<string, object>
            {
                { "success", true },
                { "targetName", target.name },
                { "targetType", target.GetType().Name },
                { "targetFullType", target.GetType().FullName },
                { "instanceId", VmObjectId.Get(target) },
                { "assetPath", AssetDatabase.GetAssetPath(target) },
            };
        }

        private static Dictionary<string, object> BuildPropertyInfo(SerializedProperty property, int maxDepth,
            int maxArrayElements)
        {
            return new Dictionary<string, object>
            {
                { "name", property.name },
                { "displayName", property.displayName },
                { "propertyPath", property.propertyPath },
                { "type", property.propertyType.ToString() },
                { "editable", property.editable },
                { "isArray", property.isArray },
                { "arraySize", property.isArray ? property.arraySize : -1 },
                { "value", MCPComponentCommands.GetSerializedValue(property, maxDepth, maxArrayElements) },
            };
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out var value) && value != null ? value.ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is bool boolValue)
                return boolValue;

            return bool.TryParse(value.ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            return int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }

        private static bool TryGetObjectId(Dictionary<string, object> args, string key, out object id)
        {
            id = null;
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return false;

            string text = value.ToString();
            if (string.IsNullOrWhiteSpace(text) || text == "0")
                return false;

            id = value;
            return true;
        }
    }
}
