using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationShaderGraphDocumentCodec
    {
    internal static ShaderGraphDocument ParseShaderGraphDocument(string content)
    {
        var document = new ShaderGraphDocument();
        foreach (string block in ParseMultiJson(content))
        {
            if (!(MiniJson.Deserialize(block) is Dictionary<string, object> parsed))
                throw new InvalidDataException("Shader Graph contains a non-object JSON block.");

            document.Blocks.Add(block);
            string objectId = GetString(parsed, "m_ObjectId");
            if (string.IsNullOrEmpty(objectId) &&
                parsed.TryGetValue("m_Id", out object idValue) && idValue is string id)
            {
                objectId = id;
            }

            if (!string.IsNullOrEmpty(objectId))
            {
                if (document.ObjectsById.ContainsKey(objectId))
                    throw new InvalidDataException($"Duplicate Shader Graph object ID '{objectId}'.");
                document.ObjectsById.Add(objectId, parsed);
            }

            string type = GetString(parsed, "m_Type");
            if (!string.IsNullOrEmpty(type) &&
                type.EndsWith(".GraphData", StringComparison.Ordinal))
            {
                if (document.GraphData != null)
                    throw new InvalidDataException("Shader Graph contains multiple GraphData objects.");
                document.GraphData = parsed;
            }
        }

        if (document.GraphData == null)
            throw new InvalidDataException("Shader Graph does not contain a GraphData object.");
        return document;
    }

    internal static Dictionary<string, object> BuildShaderPropertyInfo(
        Shader shader,
        int propertyIndex,
        Dictionary<string, Dictionary<string, object>> textureMetadata)
    {
        var propertyType = shader.GetPropertyType(propertyIndex);
        string propertyName = shader.GetPropertyName(propertyIndex);
        var flags = shader.GetPropertyFlags(propertyIndex);
        var property = new Dictionary<string, object>
        {
            { "name", propertyName },
            { "description", shader.GetPropertyDescription(propertyIndex) },
            { "type", propertyType.ToString() },
            { "flags", flags.ToString() },
            { "isHidden", flags.HasFlag(UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) },
        };

        if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Range)
        {
            Vector2 limits = shader.GetPropertyRangeLimits(propertyIndex);
            property["rangeMin"] = limits.x;
            property["rangeMax"] = limits.y;
            property["rangeDefault"] = shader.GetPropertyDefaultFloatValue(propertyIndex);
        }

        if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture)
        {
            property["textureDimension"] = shader.GetPropertyTextureDimension(propertyIndex).ToString();
            if (textureMetadata.TryGetValue(propertyName, out var metadata))
            {
                foreach (var pair in metadata)
                    property[pair.Key] = pair.Value;
            }
        }

        return property;
    }

    internal static Dictionary<string, Dictionary<string, object>> GetTexturePropertyMetadata(
        ShaderGraphDocument document)
    {
        var result = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        foreach (string propertyId in GetReferencedObjectIds(document.GraphData, "m_Properties"))
        {
            if (!document.ObjectsById.TryGetValue(propertyId, out var property))
                throw new InvalidDataException($"GraphData references missing property '{propertyId}'.");

            string type = GetString(property, "m_Type");
            if (string.IsNullOrEmpty(type) ||
                !type.EndsWith("ShaderProperty", StringComparison.Ordinal) ||
                type.IndexOf("Texture", StringComparison.Ordinal) < 0)
            {
                continue;
            }

            string referenceName = GetString(property, "m_OverrideReferenceName");
            if (string.IsNullOrEmpty(referenceName))
                referenceName = GetString(property, "m_DefaultReferenceName");
            if (string.IsNullOrEmpty(referenceName))
                continue;

            var metadata = new Dictionary<string, object>
            {
                { "graphObjectId", propertyId },
                { "graphPropertyType", type },
            };
            AddOptionalString(metadata, "graphDisplayName", GetString(property, "m_Name"));
            AddOptionalBoolean(metadata, "generatePropertyBlock", property, "m_GeneratePropertyBlock");
            AddOptionalBoolean(metadata, "perRendererData", property, "m_PerRendererData");
            AddOptionalBoolean(metadata, "isMainTexture", property, "isMainTexture");
            AddOptionalBoolean(metadata, "useTilingAndOffset", property, "useTilingAndOffset");
            AddOptionalBoolean(metadata, "useTexelSize", property, "useTexelSize");
            result[referenceName] = metadata;
        }

        return result;
    }

    internal static void AddOptionalString(Dictionary<string, object> target, string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
            target[key] = value;
    }

    internal static void AddOptionalBoolean(
        Dictionary<string, object> target,
        string outputKey,
        Dictionary<string, object> source,
        string sourceKey)
    {
        if (source.TryGetValue(sourceKey, out object value) && TryConvertBoolean(value, out bool result))
            target[outputKey] = result;
    }

    internal static string GetString(Dictionary<string, object> dictionary, string key)
    {
        return dictionary != null && dictionary.TryGetValue(key, out object value)
            ? value as string
            : null;
    }

    internal static List<string> GetReferencedObjectIds(
        Dictionary<string, object> owner,
        string collectionName)
    {
        var result = new List<string>();
        if (owner == null || !owner.TryGetValue(collectionName, out object collection) || collection == null)
            return result;
        if (!(collection is IEnumerable<object> references))
            throw new InvalidDataException($"'{collectionName}' is not a JSON array.");

        foreach (object referenceValue in references)
        {
            if (!(referenceValue is Dictionary<string, object> reference))
                throw new InvalidDataException($"'{collectionName}' contains a non-object reference.");
            string id = GetString(reference, "m_Id");
            if (string.IsNullOrEmpty(id))
                throw new InvalidDataException($"'{collectionName}' contains a reference without m_Id.");
            result.Add(id);
        }

        return result;
    }

    internal static List<Dictionary<string, object>> ReadGraphEdges(Dictionary<string, object> graphData)
    {
        var result = new List<Dictionary<string, object>>();
        if (graphData == null || !graphData.TryGetValue("m_Edges", out object edgesValue) ||
            edgesValue == null)
        {
            return result;
        }
        if (!(edgesValue is IEnumerable<object> edges))
            throw new InvalidDataException("'m_Edges' is not a JSON array.");

        foreach (object edgeValue in edges)
        {
            if (!(edgeValue is Dictionary<string, object> edge))
                throw new InvalidDataException("'m_Edges' contains a non-object edge.");

            Dictionary<string, object> outputSlot = GetRequiredDictionary(edge, "m_OutputSlot");
            Dictionary<string, object> inputSlot = GetRequiredDictionary(edge, "m_InputSlot");
            string outputNodeId = GetString(GetRequiredDictionary(outputSlot, "m_Node"), "m_Id");
            string inputNodeId = GetString(GetRequiredDictionary(inputSlot, "m_Node"), "m_Id");
            if (string.IsNullOrEmpty(outputNodeId) || string.IsNullOrEmpty(inputNodeId))
                throw new InvalidDataException("Shader Graph edge contains an empty node ID.");

            result.Add(new Dictionary<string, object>
            {
                { "outputNodeId", outputNodeId },
                { "outputSlotId", GetRequiredInteger(outputSlot, "m_SlotId") },
                { "inputNodeId", inputNodeId },
                { "inputSlotId", GetRequiredInteger(inputSlot, "m_SlotId") },
            });
        }

        return result;
    }

    internal static Dictionary<string, object> GetRequiredDictionary(
        Dictionary<string, object> owner,
        string key)
    {
        if (!owner.TryGetValue(key, out object value) ||
            !(value is Dictionary<string, object> dictionary))
        {
            throw new InvalidDataException($"Shader Graph JSON is missing object '{key}'.");
        }
        return dictionary;
    }

    internal static int GetRequiredInteger(Dictionary<string, object> owner, string key)
    {
        if (!owner.TryGetValue(key, out object value) || value == null)
            throw new InvalidDataException($"Shader Graph JSON is missing integer '{key}'.");
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Shader Graph JSON value '{key}' is not an integer.", ex);
        }
    }

    internal static bool TryGetPosition(
        Dictionary<string, object> node,
        out double positionX,
        out double positionY)
    {
        positionX = 0;
        positionY = 0;
        if (!node.TryGetValue("m_DrawState", out object drawStateValue) ||
            !(drawStateValue is Dictionary<string, object> drawState) ||
            !drawState.TryGetValue("m_Position", out object positionValue) ||
            !(positionValue is Dictionary<string, object> position) ||
            !position.TryGetValue("x", out object xValue) ||
            !position.TryGetValue("y", out object yValue))
        {
            return false;
        }

        try
        {
            positionX = Convert.ToDouble(xValue, CultureInfo.InvariantCulture);
            positionY = Convert.ToDouble(yValue, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static List<string> ParseMultiJson(string content)
    {
        var blocks = new List<string>();
        int index = 0;
        while (index < content.Length)
        {
            while (index < content.Length &&
                   (char.IsWhiteSpace(content[index]) || content[index] == '\uFEFF'))
                index++;
            if (index >= content.Length)
                break;
            if (content[index] != '{')
                throw new InvalidDataException($"Unexpected Shader Graph content at offset {index}.");

            int blockEnd = FindMatchingJsonDelimiter(content, index);
            if (blockEnd < 0)
                throw new InvalidDataException($"Unterminated Shader Graph JSON object at offset {index}.");
            blocks.Add(content.Substring(index, blockEnd - index + 1));
            index = blockEnd + 1;
        }

        return blocks;
    }

    internal static string ExtractJsonString(string json, string key)
    {
        string pattern = $"\"{key}\"\\s*:\\s*\"([^\"]*)\"";
        var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static List<Dictionary<string, object>> ParseEdgesFromJson(string content)
    {
        return ReadGraphEdges(ParseShaderGraphDocument(content).GraphData);
    }

    internal static int FindJsonArrayEnd(string content, string arrayName)
    {
        int idx = content.IndexOf($"\"{arrayName}\"");
        if (idx < 0) return -1;
        int arrayStart = content.IndexOf('[', idx);
        if (arrayStart < 0) return -1;
        return FindMatchingBracket(content, arrayStart);
    }

    internal static int FindMatchingBracket(string content, int openPos)
    {
        return FindMatchingJsonDelimiter(content, openPos);
    }

    internal static int FindMatchingJsonDelimiter(string content, int openPosition)
    {
        if (string.IsNullOrEmpty(content) || openPosition < 0 || openPosition >= content.Length)
            return -1;
        char open = content[openPosition];
        if (open != '{' && open != '[')
            return -1;
        char close = open == '{' ? '}' : ']';
        int depth = 1;
        bool inString = false;
        bool escaped = false;
        for (int index = openPosition + 1; index < content.Length; index++)
        {
            char character = content[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }
            if (character == open)
            {
                depth++;
            }
            else if (character == close)
            {
                depth--;
                if (depth == 0)
                    return index;
            }
        }
        return -1;
    }

    internal static string RemoveEdgesForNode(string graphBlock, string nodeId, out int removedCount)
    {
        removedCount = 0;
        int edgesIdx = graphBlock.IndexOf("\"m_Edges\"");
        if (edgesIdx < 0) return graphBlock;

        int arrayStart = graphBlock.IndexOf('[', edgesIdx);
        int arrayEnd = FindMatchingBracket(graphBlock, arrayStart);
        if (arrayEnd < 0) return graphBlock;

        var edges = ParseEdgesFromJson(graphBlock);
        var keepEdges = new List<string>();

        foreach (var edge in edges)
        {
            string outNode = edge["outputNodeId"].ToString();
            string inNode = edge["inputNodeId"].ToString();

            if (outNode == nodeId || inNode == nodeId)
            {
                removedCount++;
                continue;
            }

            keepEdges.Add($"{{\"m_OutputSlot\":{{\"m_Node\":{{\"m_Id\":\"{outNode}\"}},\"m_SlotId\":{edge["outputSlotId"]}}},\"m_InputSlot\":{{\"m_Node\":{{\"m_Id\":\"{inNode}\"}},\"m_SlotId\":{edge["inputSlotId"]}}}}}");
        }

        string newArray = "[" + string.Join(",", keepEdges) + "]";
        return graphBlock.Substring(0, arrayStart) + newArray + graphBlock.Substring(arrayEnd + 1);
    }

    internal static bool TryNormalizeScalarJsonValue(
        object previousValue,
        object requestedValue,
        out object normalizedValue,
        out string error)
    {
        normalizedValue = null;
        error = null;

        if (previousValue is bool)
        {
            if (!TryConvertBoolean(requestedValue, out bool boolValue))
            {
                error = $"Value '{requestedValue}' is not a boolean.";
                return false;
            }
            normalizedValue = boolValue;
            return true;
        }

        if (IsNumber(previousValue))
        {
            if (!double.TryParse(Convert.ToString(requestedValue, CultureInfo.InvariantCulture),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double numericValue) ||
                double.IsNaN(numericValue) || double.IsInfinity(numericValue))
            {
                error = $"Value '{requestedValue}' is not a finite number.";
                return false;
            }

            if (previousValue is byte || previousValue is sbyte || previousValue is short ||
                previousValue is ushort || previousValue is int || previousValue is uint ||
                previousValue is long || previousValue is ulong)
            {
                if (numericValue % 1 != 0 || numericValue < long.MinValue || numericValue > long.MaxValue)
                {
                    error = $"Value '{requestedValue}' is not an integer in range.";
                    return false;
                }
                normalizedValue = Convert.ToInt64(numericValue);
            }
            else
            {
                normalizedValue = numericValue;
            }
            return true;
        }

        if (previousValue is string)
        {
            normalizedValue = requestedValue?.ToString() ?? string.Empty;
            return true;
        }

        if (previousValue == null &&
            (requestedValue == null || requestedValue is string || requestedValue is bool ||
             IsNumber(requestedValue)))
        {
            normalizedValue = requestedValue;
            return true;
        }

        error = "Only scalar string, number, boolean, or null Shader Graph fields can be edited safely.";
        return false;
    }

    internal static bool TryConvertBoolean(object value, out bool result)
    {
        if (value is bool boolean)
        {
            result = boolean;
            return true;
        }
        return bool.TryParse(value?.ToString(), out result);
    }

    internal static bool IsNumber(object value)
    {
        return value is byte || value is sbyte || value is short || value is ushort ||
               value is int || value is uint || value is long || value is ulong ||
               value is float || value is double || value is decimal;
    }

    internal static bool JsonScalarEquals(object left, object right)
    {
        if (IsNumber(left) && IsNumber(right))
        {
            return Convert.ToDouble(left, CultureInfo.InvariantCulture).Equals(
                Convert.ToDouble(right, CultureInfo.InvariantCulture));
        }
        return Equals(left, right);
    }

    internal static bool TrySetTopLevelJsonProperty(
        string jsonObject,
        string propertyName,
        object value,
        out string modified)
    {
        modified = jsonObject;
        if (!TryFindTopLevelJsonPropertyValue(
                jsonObject, propertyName, out int valueStart, out int valueEnd))
        {
            return false;
        }

        string serializedValue = MiniJson.Serialize(value);
        modified = jsonObject.Substring(0, valueStart) + serializedValue +
                   jsonObject.Substring(valueEnd);
        return true;
    }

    internal static bool TryFindTopLevelJsonPropertyValue(
        string jsonObject,
        string propertyName,
        out int valueStart,
        out int valueEnd)
    {
        valueStart = -1;
        valueEnd = -1;
        int objectDepth = 0;
        int arrayDepth = 0;

        for (int index = 0; index < jsonObject.Length; index++)
        {
            char character = jsonObject[index];
            if (character == '"')
            {
                int stringEnd = FindJsonStringEnd(jsonObject, index);
                if (stringEnd < 0)
                    return false;

                if (objectDepth == 1 && arrayDepth == 0)
                {
                    string keyToken = jsonObject.Substring(index, stringEnd - index + 1);
                    string key = MiniJson.Deserialize(keyToken) as string;
                    int colon = stringEnd + 1;
                    while (colon < jsonObject.Length && char.IsWhiteSpace(jsonObject[colon]))
                        colon++;
                    if (string.Equals(key, propertyName, StringComparison.Ordinal) &&
                        colon < jsonObject.Length && jsonObject[colon] == ':')
                    {
                        valueStart = colon + 1;
                        while (valueStart < jsonObject.Length && char.IsWhiteSpace(jsonObject[valueStart]))
                            valueStart++;
                        valueEnd = FindJsonValueEnd(jsonObject, valueStart);
                        return valueEnd > valueStart;
                    }
                }

                index = stringEnd;
                continue;
            }

            if (character == '{')
                objectDepth++;
            else if (character == '}')
                objectDepth--;
            else if (character == '[')
                arrayDepth++;
            else if (character == ']')
                arrayDepth--;
        }

        return false;
    }

    internal static int FindJsonStringEnd(string json, int quotePosition)
    {
        bool escaped = false;
        for (int index = quotePosition + 1; index < json.Length; index++)
        {
            char character = json[index];
            if (escaped)
            {
                escaped = false;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else if (character == '"')
            {
                return index;
            }
        }
        return -1;
    }

    internal static int FindJsonValueEnd(string json, int valueStart)
    {
        if (valueStart < 0 || valueStart >= json.Length)
            return -1;
        char first = json[valueStart];
        if (first == '"')
        {
            int stringEnd = FindJsonStringEnd(json, valueStart);
            return stringEnd < 0 ? -1 : stringEnd + 1;
        }
        if (first == '{' || first == '[')
        {
            int delimiterEnd = FindMatchingJsonDelimiter(json, valueStart);
            return delimiterEnd < 0 ? -1 : delimiterEnd + 1;
        }

        int end = valueStart;
        while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
            end++;
        while (end > valueStart && char.IsWhiteSpace(json[end - 1]))
            end--;
        return end;
    }

    internal static Type ResolveShaderGraphNodeType(string typeName)
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "Unity.ShaderGraph.Editor") continue;

                // Try exact match
                Type t = asm.GetType($"UnityEditor.ShaderGraph.{typeName}");
                if (t != null) return t;

                // Try with "Node" suffix
                t = asm.GetType($"UnityEditor.ShaderGraph.{typeName}Node");
                if (t != null) return t;

                // Search by name
                foreach (var type in asm.GetTypes())
                {
                    if (type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                        type.Name.Equals(typeName + "Node", StringComparison.OrdinalIgnoreCase))
                        return type;
                }
            }
        }
        catch { }

        return null;
    }

    internal static string TrySerializeNodeViaReflection(Type nodeType, string nodeId, float posX, float posY)
    {
        try
        {
            // Create instance
            var node = Activator.CreateInstance(nodeType);
            if (node == null) return null;

            // Use JsonUtility to get a baseline serialization
            string serialized = JsonUtility.ToJson(node, true);

            // Inject our ID and position
            if (!serialized.Contains("m_ObjectId"))
                serialized = serialized.TrimEnd('}') + $",\"m_ObjectId\":\"{nodeId}\"}}";
            else
                serialized = System.Text.RegularExpressions.Regex.Replace(
                    serialized, "\"m_ObjectId\"\\s*:\\s*\"[^\"]*\"", $"\"m_ObjectId\":\"{nodeId}\"");

            // Inject type info
            if (!serialized.Contains("m_Type"))
                serialized = serialized.TrimEnd('}') + $",\"m_Type\":\"{nodeType.FullName}\"}}";

            // Add draw state with position
            if (!serialized.Contains("m_DrawState"))
            {
                string drawState = $"\"m_DrawState\":{{\"m_Expanded\":true,\"m_Position\":{{\"serializedVersion\":\"2\",\"x\":{posX},\"y\":{posY},\"width\":208,\"height\":311}}}}";
                serialized = serialized.TrimEnd('}') + "," + drawState + "}";
            }

            return serialized;
        }
        catch
        {
            return null;
        }
    }

    internal static string GetNodeTemplate(string nodeType, string nodeId, float posX, float posY)
    {
        string lower = nodeType.ToLowerInvariant();

        // Common node templates
        string position = $"\"x\":{posX},\"y\":{posY},\"width\":208,\"height\":311";
        string drawState = $"\"m_DrawState\":{{\"m_Expanded\":true,\"m_Position\":{{\"serializedVersion\":\"2\",{position}}}}}";

        switch (lower)
        {
            case "add":
            case "addnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.AddNode\",\"m_Name\":\"Add\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "multiply":
            case "multiplynode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.MultiplyNode\",\"m_Name\":\"Multiply\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "subtract":
            case "subtractnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.SubtractNode\",\"m_Name\":\"Subtract\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "divide":
            case "dividenode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.DivideNode\",\"m_Name\":\"Divide\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "lerp":
            case "lerpnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.LerpNode\",\"m_Name\":\"Lerp\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "color":
            case "colornode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.ColorNode\",\"m_Name\":\"Color\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[],\"m_Color\":{{\"r\":1,\"g\":1,\"b\":1,\"a\":1}}}}";
            case "float":
            case "vector1":
            case "vector1node":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.Vector1Node\",\"m_Name\":\"Float\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[],\"m_Value\":0}}";
            case "vector2":
            case "vector2node":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.Vector2Node\",\"m_Name\":\"Vector 2\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "vector3":
            case "vector3node":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.Vector3Node\",\"m_Name\":\"Vector 3\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "vector4":
            case "vector4node":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.Vector4Node\",\"m_Name\":\"Vector 4\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "time":
            case "timenode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.TimeNode\",\"m_Name\":\"Time\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "uv":
            case "uvnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.UVNode\",\"m_Name\":\"UV\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "position":
            case "positionnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.PositionNode\",\"m_Name\":\"Position\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "normal":
            case "normalnode":
            case "normalvector":
            case "normalvectornode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.NormalVectorNode\",\"m_Name\":\"Normal Vector\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "sampletexture2d":
            case "sampletexture2dnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.SampleTexture2DNode\",\"m_Name\":\"Sample Texture 2D\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "fresnel":
            case "fresneleffect":
            case "fresneleffectnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.FresnelEffectNode\",\"m_Name\":\"Fresnel Effect\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "saturate":
            case "saturatenode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.SaturateNode\",\"m_Name\":\"Saturate\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "oneminusx":
            case "oneminusnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.OneMinusNode\",\"m_Name\":\"One Minus\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "power":
            case "powernode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.PowerNode\",\"m_Name\":\"Power\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "split":
            case "splitnode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.SplitNode\",\"m_Name\":\"Split\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            case "combine":
            case "combinenode":
                return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.CombineNode\",\"m_Name\":\"Combine\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
            default:
                return null;
        }
    }
    }
}
