using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationMaterialCommands
    {
        public static object GetProperties(Dictionary<string, object> args)
        {
            if (!TryValidateTopLevelKeys(args,
                    new[] { "assetPath", "propertyNames", "offset", "limit", "_agentId" },
                    out object keyError))
                return keyError;
            if (!TryLoadMaterial(args, out string assetPath, out Material material, out object error))
                return error;

            if (!TryGetStringArray(args, "propertyNames", out string[] requested,
                    out string arrayError))
                return VmAutomationResponse.Error(arrayError, "invalid_arguments");
            string[] declared = GetDeclaredPropertyNames(material.shader);
            string unknown = requested.FirstOrDefault(name =>
                !declared.Contains(name, StringComparer.Ordinal));
            if (unknown != null)
                return VmAutomationResponse.Error(
                    $"Material shader '{material.shader?.name}' has no property '{unknown}'.",
                    "material_property_not_found");

            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(500, GetInt(args, "limit", 100)));
            string[] candidates = requested.Length > 0 ? requested : declared;
            string[] selected = candidates.Skip(offset).Take(limit).ToArray();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "assetPath", assetPath },
                { "propertyTotal", candidates.Length },
                { "offset", offset },
                { "limit", limit },
                { "hasMore", offset + selected.Length < candidates.Length },
                { "nextOffset", offset + selected.Length < candidates.Length
                    ? (object)(offset + selected.Length)
                    : null },
                { "material", ReadMaterial(material, selected) },
            };
        }

        public static object SetProperties(Dictionary<string, object> args)
        {
            if (!TryValidateTopLevelKeys(args,
                    new[]
                    {
                        "assetPath", "properties", "keywords", "shader", "renderQueue",
                        "enableInstancing", "doubleSidedGI", "globalIlluminationFlags",
                        "dryRun", "_agentId",
                    },
                    out object keyError))
                return keyError;
            if (!TryLoadMaterial(args, out string assetPath, out Material material, out object error))
                return error;
            if (!TryGetOptionalDictionary(args, "properties",
                    out Dictionary<string, object> properties, out string dictionaryError))
                return VmAutomationResponse.Error(dictionaryError, "invalid_arguments");
            if (!TryGetOptionalDictionary(args, "keywords",
                    out Dictionary<string, object> keywords, out dictionaryError))
                return VmAutomationResponse.Error(dictionaryError, "invalid_arguments");
            properties ??= new Dictionary<string, object>();
            bool hasMaterialSetting = HasAny(args, "shader", "renderQueue", "enableInstancing",
                "doubleSidedGI", "globalIlluminationFlags");
            if (properties.Count == 0 && keywords == null && !hasMaterialSetting)
                return VmAutomationResponse.Error(
                    "Provide properties, keywords, shader, renderQueue, enableInstancing, doubleSidedGI, or globalIlluminationFlags.",
                    "invalid_arguments");

            string replacementShader = GetString(args, "shader");
            Shader replacement = string.IsNullOrEmpty(replacementShader)
                ? null
                : Shader.Find(replacementShader);
            if (!string.IsNullOrEmpty(replacementShader) && replacement == null)
                return VmAutomationResponse.Error($"Shader '{replacementShader}' was not found.",
                    "shader_not_found");

            try
            {
                ValidateKeywordChanges(keywords);
                ValidateOnClone(material, replacement, properties, keywords, args);
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "invalid_material_setting");
            }

            var before = ReadMaterial(material, properties.Keys.ToArray());
            if (GetBool(args, "dryRun", false))
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "assetPath", assetPath },
                    { "before", before },
                    { "requestedProperties", properties },
                    { "requestedKeywords", keywords },
                };
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VM Unity Automation Set Material Properties");
            try
            {
                Undo.RecordObject(material, "VM Unity Automation Set Material Properties");
                if (replacement != null)
                    material.shader = replacement;
                if (args.TryGetValue("renderQueue", out object renderQueue))
                    material.renderQueue = Convert.ToInt32(renderQueue);
                if (args.TryGetValue("enableInstancing", out object enableInstancing))
                    material.enableInstancing = Convert.ToBoolean(enableInstancing);
                if (args.TryGetValue("doubleSidedGI", out object doubleSidedGi))
                    material.doubleSidedGI = Convert.ToBoolean(doubleSidedGi);
                if (args.TryGetValue("globalIlluminationFlags", out object giFlags))
                    material.globalIlluminationFlags =
                        ParseEnum<MaterialGlobalIlluminationFlags>(giFlags);

                foreach (var pair in properties)
                    SetShaderProperty(material, pair.Key, pair.Value);
                ApplyKeywords(material, keywords);

                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "assetPath", assetPath },
                    { "before", before },
                    { "after", ReadMaterial(material, properties.Keys.ToArray()) },
                };
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "material_update_failed");
            }
        }

        private static void ValidateOnClone(Material source, Shader replacement,
            Dictionary<string, object> properties,
            Dictionary<string, object> keywords, Dictionary<string, object> args)
        {
            Material clone = null;
            try
            {
                clone = new Material(source);
                if (replacement != null)
                    clone.shader = replacement;
                foreach (string propertyName in properties.Keys)
                {
                    if (!clone.HasProperty(propertyName))
                    {
                        throw new ArgumentException(
                            $"Material shader '{clone.shader?.name}' has no property '{propertyName}'.");
                    }
                }

                if (args.TryGetValue("renderQueue", out object renderQueue))
                {
                    int queue = Convert.ToInt32(renderQueue);
                    if (queue < -1 || queue > 5000)
                        throw new ArgumentException(
                            "renderQueue must be -1 or between 0 and 5000.");
                    clone.renderQueue = queue;
                }
                if (args.TryGetValue("enableInstancing", out object enableInstancing))
                    clone.enableInstancing = Convert.ToBoolean(enableInstancing);
                if (args.TryGetValue("doubleSidedGI", out object doubleSidedGi))
                    clone.doubleSidedGI = Convert.ToBoolean(doubleSidedGi);
                if (args.TryGetValue("globalIlluminationFlags", out object giFlags))
                    clone.globalIlluminationFlags =
                        ParseEnum<MaterialGlobalIlluminationFlags>(giFlags);
                foreach (var pair in properties)
                    SetShaderProperty(clone, pair.Key, pair.Value);
                ApplyKeywords(clone, keywords);
            }
            finally
            {
                if (clone != null)
                    UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private static Dictionary<string, object> ReadMaterial(Material material,
            IReadOnlyCollection<string> requested)
        {
            Shader shader = material.shader;
            var properties = new Dictionary<string, object>();
            if (shader != null)
            {
                for (int index = 0; index < shader.GetPropertyCount(); index++)
                {
                    string propertyName = shader.GetPropertyName(index);
                    if (requested != null &&
                        !requested.Contains(propertyName))
                        continue;
                    properties[propertyName] = ReadShaderProperty(material, shader, index);
                }
            }

            return new Dictionary<string, object>
            {
                { "name", material.name ?? "" },
                { "shader", shader != null ? shader.name : "" },
                { "renderQueue", material.renderQueue },
                { "enableInstancing", material.enableInstancing },
                { "doubleSidedGI", material.doubleSidedGI },
                { "globalIlluminationFlags", material.globalIlluminationFlags.ToString() },
                { "keywords", ReadKeywords(material) },
                { "properties", properties },
            };
        }

        private static Dictionary<string, object> ReadShaderProperty(Material material,
            Shader shader, int index)
        {
            string name = shader.GetPropertyName(index);
            ShaderPropertyType type = shader.GetPropertyType(index);
            var result = new Dictionary<string, object>
            {
                { "type", type.ToString() },
                { "description", shader.GetPropertyDescription(index) ?? "" },
                { "flags", shader.GetPropertyFlags(index).ToString() },
            };

            switch (type)
            {
                case ShaderPropertyType.Color:
                    result["value"] = ColorValue(material.GetColor(name));
                    break;
                case ShaderPropertyType.Vector:
                    result["value"] = VectorValue(material.GetVector(name));
                    break;
                case ShaderPropertyType.Texture:
                    Texture texture = material.GetTexture(name);
                    result["value"] = texture == null
                        ? null
                        : new Dictionary<string, object>
                        {
                            { "name", texture.name ?? "" },
                            { "assetPath", AssetDatabase.GetAssetPath(texture) ?? "" },
                            { "type", texture.GetType().Name },
                        };
                    result["scale"] = Vector2Value(material.GetTextureScale(name));
                    result["offset"] = Vector2Value(material.GetTextureOffset(name));
                    break;
                default:
                    result["value"] = type.ToString() == "Int"
                        ? (object)material.GetInt(name)
                        : material.GetFloat(name);
                    if (type == ShaderPropertyType.Range)
                    {
                        Vector2 limits = shader.GetPropertyRangeLimits(index);
                        result["range"] = Vector2Value(limits);
                    }
                    break;
            }
            return result;
        }

        private static void SetShaderProperty(Material material, string name, object rawValue)
        {
            Shader shader = material.shader;
            int index = shader != null ? shader.FindPropertyIndex(name) : -1;
            if (index < 0)
                throw new ArgumentException($"Shader property '{name}' was not found.");
            ShaderPropertyType type = shader.GetPropertyType(index);

            switch (type)
            {
                case ShaderPropertyType.Color:
                    material.SetColor(name, ReadColor(rawValue));
                    return;
                case ShaderPropertyType.Vector:
                    material.SetVector(name, ReadVector4(rawValue));
                    return;
                case ShaderPropertyType.Texture:
                    SetTexture(material, name, rawValue);
                    return;
                default:
                    if (type.ToString() == "Int")
                        material.SetInt(name, Convert.ToInt32(UnwrapValue(rawValue)));
                    else
                        material.SetFloat(name, Convert.ToSingle(UnwrapValue(rawValue)));
                    return;
            }
        }

        private static void SetTexture(Material material, string name, object rawValue)
        {
            if (rawValue == null)
            {
                material.SetTexture(name, null);
                return;
            }
            if (!(rawValue is Dictionary<string, object> value))
                throw new ArgumentException(
                    $"Texture property '{name}' must be null or an object with assetPath.");

            string unknown = value.Keys.FirstOrDefault(key =>
                key != "assetPath" && key != "scale" && key != "offset");
            if (unknown != null)
                throw new ArgumentException(
                    $"Texture property '{name}' does not support field '{unknown}'.");
            if (!value.ContainsKey("assetPath") &&
                !value.ContainsKey("scale") && !value.ContainsKey("offset"))
                throw new ArgumentException(
                    $"Texture property '{name}' must provide assetPath, scale, or offset.");
            if (value.TryGetValue("assetPath", out object assetPathValue))
            {
                string assetPath = assetPathValue?.ToString() ?? "";
                Texture texture = string.IsNullOrEmpty(assetPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                if (!string.IsNullOrEmpty(assetPath) && texture == null)
                    throw new ArgumentException($"Texture asset '{assetPath}' was not found.");
                material.SetTexture(name, texture);
            }
            if (value.TryGetValue("scale", out object scale))
                material.SetTextureScale(name, ReadVector2(scale));
            if (value.TryGetValue("offset", out object offset))
                material.SetTextureOffset(name, ReadVector2(offset));
        }

        private static void ApplyKeywords(Material material, Dictionary<string, object> keywords)
        {
            if (keywords == null)
                return;
            TryGetStringArray(keywords, "enable", out string[] enable, out _);
            TryGetStringArray(keywords, "disable", out string[] disable, out _);
            foreach (string keyword in enable)
                material.EnableKeyword(keyword);
            foreach (string keyword in disable)
                material.DisableKeyword(keyword);
        }

        private static void ValidateKeywordChanges(Dictionary<string, object> keywords)
        {
            if (keywords == null)
                return;
            string unknown = keywords.Keys.FirstOrDefault(key =>
                key != "enable" && key != "disable");
            if (unknown != null)
                throw new ArgumentException(
                    $"keywords.{unknown} is not supported. Use enable and disable.");
            if (!TryGetStringArray(keywords, "enable", out string[] enable,
                    out string error) ||
                !TryGetStringArray(keywords, "disable", out string[] disable,
                    out error))
                throw new ArgumentException(error);
            string conflict = enable.FirstOrDefault(keyword =>
                disable.Contains(keyword, StringComparer.Ordinal));
            if (conflict != null)
                throw new ArgumentException(
                    $"Keyword '{conflict}' cannot be enabled and disabled in the same transaction.");
        }

        private static string[] ReadKeywords(Material material)
        {
            PropertyInfo property = typeof(Material).GetProperty("shaderKeywords",
                BindingFlags.Instance | BindingFlags.Public);
            return (property?.GetValue(material) as string[] ?? Array.Empty<string>())
                .OrderBy(keyword => keyword, StringComparer.Ordinal).ToArray();
        }

        private static string[] GetDeclaredPropertyNames(Shader shader)
        {
            if (shader == null)
                return Array.Empty<string>();
            var names = new string[shader.GetPropertyCount()];
            for (int index = 0; index < names.Length; index++)
                names[index] = shader.GetPropertyName(index);
            return names;
        }

        private static bool TryLoadMaterial(Dictionary<string, object> args, out string assetPath,
            out Material material, out object error)
        {
            assetPath = GetString(args, "assetPath");
            material = null;
            error = null;
            if (string.IsNullOrEmpty(assetPath))
            {
                error = VmAutomationResponse.Error("assetPath is required.", "invalid_arguments");
                return false;
            }
            material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
                return true;
            error = VmAutomationResponse.Error($"Material asset '{assetPath}' was not found.",
                "material_not_found");
            return false;
        }

        private static object UnwrapValue(object value)
        {
            return value is Dictionary<string, object> dictionary &&
                   dictionary.Count == 1 && dictionary.TryGetValue("value", out object scalar)
                ? scalar
                : value;
        }

        private static Color ReadColor(object value)
        {
            if (!(value is Dictionary<string, object> values))
                throw new ArgumentException("Color value must be an object with r/g/b/a.");
            ValidateVectorKeys(values, "Color", new[] { "r", "g", "b", "a" },
                new[] { "r", "g", "b" });
            return new Color(GetFloat(values, "r"), GetFloat(values, "g"),
                GetFloat(values, "b"), GetFloat(values, "a", 1f));
        }

        private static Vector4 ReadVector4(object value)
        {
            if (!(value is Dictionary<string, object> values))
                throw new ArgumentException("Vector value must be an object with x/y/z/w.");
            ValidateVectorKeys(values, "Vector", new[] { "x", "y", "z", "w" },
                new[] { "x", "y", "z", "w" });
            return new Vector4(GetFloat(values, "x"), GetFloat(values, "y"),
                GetFloat(values, "z"), GetFloat(values, "w"));
        }

        private static Vector2 ReadVector2(object value)
        {
            if (!(value is Dictionary<string, object> values))
                throw new ArgumentException("Vector2 value must be an object with x/y.");
            ValidateVectorKeys(values, "Vector2", new[] { "x", "y" },
                new[] { "x", "y" });
            return new Vector2(GetFloat(values, "x"), GetFloat(values, "y"));
        }

        private static void ValidateVectorKeys(Dictionary<string, object> values,
            string label, IEnumerable<string> allowed, IEnumerable<string> required)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = values.Keys.FirstOrDefault(key => !allowedSet.Contains(key));
            if (!string.IsNullOrEmpty(unknown))
                throw new ArgumentException(
                    $"{label} does not support field '{unknown}'. Expected: " +
                    string.Join(", ", allowedSet.OrderBy(item => item)) + ".");
            string missing = required.FirstOrDefault(key => !values.ContainsKey(key));
            if (!string.IsNullOrEmpty(missing))
                throw new ArgumentException($"{label}.{missing} is required.");
        }

        private static bool TryValidateTopLevelKeys(Dictionary<string, object> values,
            IEnumerable<string> allowed, out object error)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = values?.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (string.IsNullOrEmpty(unknown))
            {
                error = null;
                return true;
            }

            error = VmAutomationResponse.Error(
                $"Unsupported argument '{unknown}'. Allowed arguments: " +
                string.Join(", ", allowedSet.Where(key => key != "_agentId")
                    .OrderBy(key => key)) + ".",
                "invalid_arguments");
            return false;
        }

        private static Dictionary<string, object> ColorValue(Color value)
        {
            return new Dictionary<string, object>
            {
                { "r", value.r }, { "g", value.g }, { "b", value.b }, { "a", value.a },
            };
        }

        private static Dictionary<string, object> VectorValue(Vector4 value)
        {
            return new Dictionary<string, object>
            {
                { "x", value.x }, { "y", value.y }, { "z", value.z }, { "w", value.w },
            };
        }

        private static Dictionary<string, object> Vector2Value(Vector2 value)
        {
            return new Dictionary<string, object> { { "x", value.x }, { "y", value.y } };
        }

        private static T ParseEnum<T>(object value) where T : struct
        {
            if (value != null && Enum.TryParse(value.ToString(), true, out T parsed))
                return parsed;
            throw new ArgumentException(
                $"Unknown {typeof(T).Name} value '{value}'. Expected one of: {string.Join(", ", Enum.GetNames(typeof(T)))}.");
        }

        private static bool HasAny(Dictionary<string, object> values, params string[] keys)
        {
            return values != null && keys.Any(values.ContainsKey);
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static bool GetBool(Dictionary<string, object> values, string key, bool defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToBoolean(value)
                : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> values, string key, int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToInt32(value)
                : defaultValue;
        }

        private static float GetFloat(Dictionary<string, object> values, string key,
            float defaultValue = 0f)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToSingle(value)
                : defaultValue;
        }

        private static bool TryGetOptionalDictionary(Dictionary<string, object> values,
            string key, out Dictionary<string, object> result, out string error)
        {
            result = null;
            error = null;
            if (values == null || !values.TryGetValue(key, out object value) || value == null)
                return true;
            result = value as Dictionary<string, object>;
            if (result != null)
                return true;
            error = $"{key} must be an object.";
            return false;
        }

        private static bool TryGetStringArray(Dictionary<string, object> values, string key,
            out string[] result, out string error)
        {
            result = Array.Empty<string>();
            error = null;
            if (values == null || !values.TryGetValue(key, out object value) || value == null)
                return true;
            if (value is string text)
            {
                result = text.Split(',').Select(item => item.Trim())
                    .Where(item => item.Length > 0).ToArray();
                return true;
            }
            if (value is List<object> list)
            {
                if (list.Any(item => !(item is string)))
                {
                    error = $"{key} must contain only strings.";
                    return false;
                }
                result = list.Cast<string>()
                    .Where(item => !string.IsNullOrEmpty(item)).ToArray();
                return true;
            }
            error = $"{key} must be a string array or comma-separated string.";
            return false;
        }
    }
}
