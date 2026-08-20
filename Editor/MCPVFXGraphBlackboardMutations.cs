using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class MCPVFXGraphBlackboardMutations
    {
        internal static Dictionary<string, object> Apply(
            MCPVFXGraphMutationContext context, string op,
            Dictionary<string, object> operation)
        {
            switch (op)
            {
                case "add-category": return AddCategory(context, operation);
                case "set-category": return SetCategory(context, operation);
                case "remove-category": return RemoveCategory(context, operation);
                case "move-category": return MoveCategory(context, operation);
                case "add-custom-attribute": return AddCustomAttribute(context,
                    operation);
                case "set-custom-attribute": return SetCustomAttribute(context,
                    operation);
                case "remove-custom-attribute": return RemoveCustomAttribute(context,
                    operation);
                case "move-custom-attribute": return MoveCustomAttribute(context,
                    operation);
                case "add-group": return AddGroup(context, operation);
                case "set-group": return SetGroup(context, operation);
                case "remove-group": return RemoveGroup(context, operation);
                case "add-sticky-note": return AddSticky(context, operation);
                case "set-sticky-note": return SetSticky(context, operation);
                case "remove-sticky-note": return RemoveSticky(context, operation);
                case "set-ui-bounds": return SetUIBounds(context, operation);
                default:
                    throw new ArgumentException(
                        $"Unsupported VFX blackboard/UI operation '{op}'.");
            }
        }

        internal static bool IsBlackboardOrUIOperation(string op)
        {
            return op == "add-category" || op == "set-category" ||
                   op == "remove-category" || op == "move-category" ||
                   op == "add-custom-attribute" ||
                   op == "set-custom-attribute" ||
                   op == "remove-custom-attribute" ||
                   op == "move-custom-attribute" || op == "add-group" ||
                   op == "set-group" || op == "remove-group" ||
                   op == "add-sticky-note" || op == "set-sticky-note" ||
                   op == "remove-sticky-note" || op == "set-ui-bounds";
        }

        private static Dictionary<string, object> AddCategory(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            string name = RequireString(operation, "name");
            IList categories = Categories(context);
            EnsureUniqueCategory(categories, name, -1);
            object category = Activator.CreateInstance(CategoryType(categories));
            Set(category, "name", name);
            Set(category, "collapsed", MCPVFXGraphMutationContext.GetBool(operation,
                "collapsed", false));
            int index = operation.ContainsKey("index")
                ? MCPVFXGraphMutationContext.GetInt(operation, "index")
                : categories.Count;
            RequireInsertIndex(index, categories.Count, "index");
            categories.Insert(index, category);
            Dirty(context);
            return Result("category", name, "index", index);
        }

        private static Dictionary<string, object> SetCategory(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            IList categories = Categories(context);
            int index = ResolveCategory(categories, operation);
            object category = categories[index];
            string previousName = MCPVFXReflection.Get(category, "name")?.ToString() ?? "";
            string name = MCPVFXGraphMutationContext.GetString(operation, "name",
                previousName);
            EnsureUniqueCategory(categories, name, index);
            if (!string.Equals(name, previousName, StringComparison.Ordinal))
            {
                foreach (UnityEngine.Object parameter in context.Session.Models.Where(
                             model => MCPVFXReflection.HasBaseType(model.GetType(),
                                 MCPVFXReflection.ParameterTypeName) && string.Equals(
                                 MCPVFXReflection.Get(model, "category")?.ToString(),
                                 previousName, StringComparison.Ordinal)))
                    Set(parameter, "category", name);
            }
            Set(category, "name", name);
            if (operation.TryGetValue("collapsed", out object collapsed))
                Set(category, "collapsed", MCPVFXValueCodec.ConvertTo(collapsed,
                    typeof(bool), "operation.collapsed"));
            categories[index] = category;
            Dirty(context);
            return Result("category", name, "index", index);
        }

        private static Dictionary<string, object> RemoveCategory(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            IList categories = Categories(context);
            int index = ResolveCategory(categories, operation);
            string name = MCPVFXReflection.Get(categories[index], "name")?.ToString() ?? "";
            string disposition = RequireString(operation, "parameterDisposition")
                .ToLowerInvariant();
            List<UnityEngine.Object> parameters = context.Session.Models.Where(model =>
                MCPVFXReflection.HasBaseType(model.GetType(),
                    MCPVFXReflection.ParameterTypeName) &&
                string.Equals(MCPVFXReflection.Get(model, "category")?.ToString(),
                    name, StringComparison.Ordinal)).ToList();
            if (disposition == "uncategorize")
            {
                foreach (UnityEngine.Object parameter in parameters)
                    Set(parameter, "category", "");
            }
            else if (disposition == "delete")
            {
                foreach (UnityEngine.Object parameter in parameters)
                    context.RemoveModel(parameter);
            }
            else
            {
                throw new ArgumentException(
                    "parameterDisposition must be uncategorize or delete.");
            }
            categories.RemoveAt(index);
            Dirty(context);
            return Result("category", name, "affectedParameters", parameters.Count);
        }

        private static Dictionary<string, object> MoveCategory(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            IList categories = Categories(context);
            int oldIndex = ResolveCategory(categories, operation);
            int newIndex = MCPVFXGraphMutationContext.GetInt(operation, "index", -1);
            RequireExistingIndex(newIndex, categories.Count, "index");
            object category = categories[oldIndex];
            categories.RemoveAt(oldIndex);
            categories.Insert(newIndex, category);
            Dirty(context);
            return Result("oldIndex", oldIndex, "index", newIndex);
        }

        private static Dictionary<string, object> AddCustomAttribute(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            string name = RequireString(operation, "name");
            string typeName = RequireString(operation, "valueType");
            string description = MCPVFXGraphMutationContext.GetString(operation,
                "description");
            AddCustomAttributeCore(context, name, typeName, description);
            if (operation.TryGetValue("expanded", out object expanded))
                SetCustomAttributeExpanded(context, name,
                    (bool)MCPVFXValueCodec.ConvertTo(expanded, typeof(bool),
                        "operation.expanded"));
            if (operation.ContainsKey("index"))
                SetCustomAttributeOrder(context, name,
                    MCPVFXGraphMutationContext.GetInt(operation, "index"));
            Dirty(context);
            return Result("customAttribute", name, "valueType", typeName);
        }

        private static Dictionary<string, object> SetCustomAttribute(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            string oldName = RequireString(operation, "attributeName");
            object descriptor = FindCustomAttribute(context, oldName);
            string newName = MCPVFXGraphMutationContext.GetString(operation, "name",
                oldName);
            bool wasExpanded = MCPVFXReflection.Get(descriptor,
                "isExpanded") is bool currentExpanded && currentExpanded;
            if (!string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                bool renamed = Convert.ToBoolean(MCPVFXReflection.Invoke(
                    context.Session.Graph, "TryRenameCustomAttribute", oldName,
                    newName));
                if (!renamed)
                    throw MCPVFXError.Create("custom_attribute_conflict",
                        $"Unity rejected custom attribute rename '{oldName}' -> '{newName}'.");
                descriptor = FindCustomAttribute(context, newName);
            }
            if (operation.TryGetValue("description", out object description))
                Set(descriptor, "description", description?.ToString() ?? "");
            if (operation.TryGetValue("valueType", out object typeValue))
            {
                string current = MCPVFXReflection.Get(descriptor, "type")?.ToString() ?? "";
                string requested = typeValue?.ToString() ?? "";
                if (!string.Equals(current, requested, StringComparison.OrdinalIgnoreCase))
                {
                    bool used = Convert.ToBoolean(MCPVFXReflection.Invoke(
                        context.Session.Graph, "IsCustomAttributeUsed", newName));
                    if (used && !MCPVFXGraphMutationContext.GetBool(operation,
                            "removeUsages", false))
                        throw MCPVFXError.Create("custom_attribute_in_use",
                            $"Custom attribute '{newName}' is in use; set removeUsages=true to remove those models before changing its type.");
                    string desc = MCPVFXReflection.Get(descriptor,
                        "description")?.ToString() ?? "";
                    MCPVFXReflection.Invoke(context.Session.Graph,
                        "RemoveCustomAttribute", newName);
                    AddCustomAttributeCore(context, newName, requested, desc);
                    SetCustomAttributeExpanded(context, newName, wasExpanded);
                }
            }
            if (operation.TryGetValue("expanded", out object expanded))
                SetCustomAttributeExpanded(context, newName,
                    (bool)MCPVFXValueCodec.ConvertTo(expanded, typeof(bool),
                        "operation.expanded"));
            if (operation.ContainsKey("index"))
                SetCustomAttributeOrder(context, newName,
                    MCPVFXGraphMutationContext.GetInt(operation, "index"));
            Dirty(context);
            return Result("customAttribute", newName, "renamedFrom", oldName);
        }

        private static Dictionary<string, object> RemoveCustomAttribute(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            string name = RequireString(operation, "attributeName");
            FindCustomAttribute(context, name);
            bool used = Convert.ToBoolean(MCPVFXReflection.Invoke(
                context.Session.Graph, "IsCustomAttributeUsed", name));
            if (used && !MCPVFXGraphMutationContext.GetBool(operation,
                    "removeUsages", false))
                throw MCPVFXError.Create("custom_attribute_in_use",
                    $"Custom attribute '{name}' is in use; set removeUsages=true to remove its usage models.");
            MCPVFXReflection.Invoke(context.Session.Graph, "RemoveCustomAttribute",
                name);
            Dirty(context);
            return Result("customAttribute", name, "removedUsages", used);
        }

        private static Dictionary<string, object> MoveCustomAttribute(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            string name = RequireString(operation, "attributeName");
            FindCustomAttribute(context, name);
            int index = MCPVFXGraphMutationContext.GetInt(operation, "index", -1);
            SetCustomAttributeOrder(context, name, index);
            Dirty(context);
            return Result("customAttribute", name, "index", index);
        }

        private static Dictionary<string, object> AddGroup(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            object ui = UI(context);
            Array groups = GetArray(ui, "groupInfos");
            object group = NewNestedUIInfo(ui, "GroupInfo");
            ApplyUIInfo(context, group, operation, true, true);
            int index = operation.ContainsKey("index")
                ? MCPVFXGraphMutationContext.GetInt(operation, "index")
                : groups.Length;
            RequireInsertIndex(index, groups.Length, "index");
            SetArray(ui, "groupInfos", Insert(groups, group, index));
            Dirty(context);
            return Result("groupIndex", index, "title",
                MCPVFXReflection.Get(group, "title")?.ToString() ?? "");
        }

        private static Dictionary<string, object> SetGroup(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            object ui = UI(context);
            Array groups = GetArray(ui, "groupInfos");
            int index = RequireArrayIndex(operation, "groupIndex", groups.Length);
            object group = groups.GetValue(index);
            ApplyUIInfo(context, group, operation, false, true);
            Dirty(context);
            return Result("groupIndex", index, "title",
                MCPVFXReflection.Get(group, "title")?.ToString() ?? "");
        }

        private static Dictionary<string, object> RemoveGroup(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            object ui = UI(context);
            Array groups = GetArray(ui, "groupInfos");
            int index = RequireArrayIndex(operation, "groupIndex", groups.Length);
            SetArray(ui, "groupInfos", Remove(groups, index));
            Dirty(context);
            return Result("groupIndex", index, "removed", true);
        }

        private static Dictionary<string, object> AddSticky(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            object ui = UI(context);
            Array notes = GetArray(ui, "stickyNoteInfos");
            object note = NewNestedUIInfo(ui, "StickyNoteInfo");
            ApplySticky(note, operation, true);
            int index = operation.ContainsKey("index")
                ? MCPVFXGraphMutationContext.GetInt(operation, "index")
                : notes.Length;
            RequireInsertIndex(index, notes.Length, "index");
            SetArray(ui, "stickyNoteInfos", Insert(notes, note, index));
            ReindexStickyReferences(ui, index, 1);
            Dirty(context);
            return Result("stickyNoteIndex", index, "title",
                MCPVFXReflection.Get(note, "title")?.ToString() ?? "");
        }

        private static Dictionary<string, object> SetSticky(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            object ui = UI(context);
            Array notes = GetArray(ui, "stickyNoteInfos");
            int index = RequireArrayIndex(operation, "stickyNoteIndex", notes.Length);
            ApplySticky(notes.GetValue(index), operation, false);
            Dirty(context);
            return Result("stickyNoteIndex", index, "updated", true);
        }

        private static Dictionary<string, object> RemoveSticky(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            object ui = UI(context);
            Array notes = GetArray(ui, "stickyNoteInfos");
            int index = RequireArrayIndex(operation, "stickyNoteIndex", notes.Length);
            RemoveStickyReferences(ui, index);
            SetArray(ui, "stickyNoteInfos", Remove(notes, index));
            ReindexStickyReferences(ui, index, -1);
            Dirty(context);
            return Result("stickyNoteIndex", index, "removed", true);
        }

        private static Dictionary<string, object> SetUIBounds(
            MCPVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            object rawBounds = operation.TryGetValue("bounds", out object value)
                ? value : throw new ArgumentException("bounds is required.");
            Rect bounds = (Rect)MCPVFXValueCodec.ConvertTo(rawBounds, typeof(Rect),
                "operation.bounds");
            Set(UI(context), "uiBounds", bounds);
            Dirty(context);
            return Result("bounds", MCPVFXValueCodec.Sanitize(bounds), "updated", true);
        }

        private static void ApplyUIInfo(MCPVFXGraphMutationContext context,
            object info, Dictionary<string, object> operation, bool requireFields,
            bool allowContents)
        {
            if (requireFields || operation.ContainsKey("title"))
                Set(info, "title", RequireString(operation, "title"));
            if (requireFields || operation.ContainsKey("position"))
            {
                object raw = operation.TryGetValue("position", out object position)
                    ? position : throw new ArgumentException("position is required.");
                Set(info, "position", MCPVFXValueCodec.ConvertTo(raw, typeof(Rect),
                    "operation.position"));
            }
            if (allowContents && operation.TryGetValue("contents", out object contents))
                Set(info, "contents", BuildGroupContents(context, contents));
        }

        private static void ApplySticky(object note,
            Dictionary<string, object> operation, bool requireFields)
        {
            if (requireFields || operation.ContainsKey("title"))
                Set(note, "title", RequireString(operation, "title"));
            if (requireFields || operation.ContainsKey("position"))
            {
                object raw = operation.TryGetValue("position", out object position)
                    ? position : throw new ArgumentException("position is required.");
                Set(note, "position", MCPVFXValueCodec.ConvertTo(raw, typeof(Rect),
                    "operation.position"));
            }
            if (operation.TryGetValue("contents", out object contents))
                Set(note, "contents", contents?.ToString() ?? "");
            if (operation.TryGetValue("theme", out object theme))
                Set(note, "theme", theme?.ToString());
            if (operation.TryGetValue("textSize", out object textSize))
                Set(note, "textSize", textSize?.ToString());
            if (operation.TryGetValue("colorTheme", out object colorTheme))
                Set(note, "colorTheme", MCPVFXValueCodec.ConvertTo(colorTheme,
                    typeof(int), "operation.colorTheme"));
        }

        private static Array BuildGroupContents(MCPVFXGraphMutationContext context,
            object rawContents)
        {
            List<object> selectors = MCPVFXGraphMutationContext.AsList(rawContents) ??
                throw new ArgumentException("contents must be an array.");
            Type nodeIdType = MCPVFXReflection.RequireType(
                MCPVFXReflection.NodeIdTypeName);
            Array result = Array.CreateInstance(nodeIdType, selectors.Count);
            for (int index = 0; index < selectors.Count; index++)
            {
                string selector = selectors[index]?.ToString() ?? "";
                object nodeId;
                if (selector.StartsWith("sticky:", StringComparison.Ordinal) &&
                    int.TryParse(selector.Substring(7), out int stickyIndex))
                {
                    nodeId = Activator.CreateInstance(nodeIdType,
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic, null, new object[] { stickyIndex }, null);
                }
                else
                {
                    UnityEngine.Object model = context.ResolveNodeSelector(selector,
                        $"contents[{index}]", out int? parameterNodeId);
                    nodeId = Activator.CreateInstance(nodeIdType,
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic, null,
                        new object[] { model, parameterNodeId ?? 0 }, null);
                }
                result.SetValue(nodeId, index);
            }
            return result;
        }

        private static void AddCustomAttributeCore(
            MCPVFXGraphMutationContext context, string name, string typeName,
            string description)
        {
            Type valueType = MCPVFXReflection.FindType(
                MCPVFXReflection.ValueTypeName) ??
                throw new MissingMemberException(MCPVFXReflection.ValueTypeName);
            object enumValue = MCPVFXValueCodec.ConvertTo(typeName, valueType,
                "operation.valueType");
            MethodInfo method = context.Session.Graph.GetType().GetMethod(
                "TryAddCustomAttribute", BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                throw new MissingMethodException(context.Session.Graph.GetType().FullName,
                    "TryAddCustomAttribute");
            object[] arguments = { name, enumValue, description, false, null };
            bool success = Convert.ToBoolean(MCPVFXReflection.InvokeMethod(method,
                context.Session.Graph, arguments));
            if (!success)
                throw MCPVFXError.Create("custom_attribute_conflict",
                    $"Unity rejected custom attribute '{name}' ({typeName}).");
        }

        private static object FindCustomAttribute(
            MCPVFXGraphMutationContext context, string name)
        {
            object descriptor = MCPVFXReflection.Enumerate(MCPVFXReflection.Get(
                    context.Session.Graph, "customAttributes"))
                .FirstOrDefault(item => string.Equals(
                    MCPVFXReflection.Get(item, "attributeName")?.ToString(), name,
                    StringComparison.OrdinalIgnoreCase));
            if (descriptor == null)
                throw MCPVFXError.Create("model_not_found",
                    $"Custom VFX attribute '{name}' was not found.");
            if (MCPVFXReflection.Get(descriptor, "isReadOnly") is bool readOnly &&
                readOnly)
                throw new InvalidOperationException(
                    $"Custom VFX attribute '{name}' is read-only because it comes from a dependency.");
            return descriptor;
        }

        private static void SetCustomAttributeOrder(
            MCPVFXGraphMutationContext context, string name, int index)
        {
            int count = MCPVFXReflection.Enumerate(MCPVFXReflection.Get(
                context.Session.Graph, "customAttributes")).Count();
            RequireExistingIndex(index, count, "index");
            MCPVFXReflection.Invoke(context.Session.Graph,
                "SetCustomAttributeOrder", name, index);
        }

        private static void SetCustomAttributeExpanded(
            MCPVFXGraphMutationContext context, string name, bool expanded)
        {
            MCPVFXReflection.Invoke(context.Session.Graph,
                "SetCustomAttributeExpanded", name, expanded);
        }

        private static object UI(MCPVFXGraphMutationContext context)
        {
            return MCPVFXReflection.Get(context.Session.Graph, "UIInfos") ??
                   throw new MissingMemberException(
                       context.Session.Graph.GetType().FullName, "UIInfos");
        }

        private static IList Categories(MCPVFXGraphMutationContext context)
        {
            object ui = UI(context);
            IList categories = MCPVFXReflection.Get(ui, "categories") as IList;
            if (categories != null)
                return categories;
            Type category = ui.GetType().GetNestedType("CategoryInfo",
                BindingFlags.Public | BindingFlags.NonPublic);
            object created = Activator.CreateInstance(
                typeof(List<>).MakeGenericType(category));
            Set(ui, "categories", created);
            return (IList)created;
        }

        private static Type CategoryType(IList categories)
        {
            return categories.GetType().GetGenericArguments().FirstOrDefault() ??
                   throw new InvalidOperationException(
                       "VFX category collection has no element type.");
        }

        private static int ResolveCategory(IList categories,
            Dictionary<string, object> operation)
        {
            if (operation.TryGetValue("categoryIndex", out object rawIndex))
            {
                int index = (int)MCPVFXValueCodec.ConvertTo(rawIndex,
                    typeof(int), "operation.categoryIndex");
                RequireExistingIndex(index, categories.Count, "categoryIndex");
                return index;
            }
            string name = RequireString(operation, "categoryName");
            List<int> matches = Enumerable.Range(0, categories.Count).Where(index =>
                string.Equals(MCPVFXReflection.Get(categories[index], "name")
                        ?.ToString(), name, StringComparison.Ordinal)).ToList();
            if (matches.Count == 0)
                throw new ArgumentException($"VFX category '{name}' was not found.");
            return matches[0];
        }

        private static void EnsureUniqueCategory(IList categories, string name,
            int exceptIndex)
        {
            if (Enumerable.Range(0, categories.Count).Any(index =>
                    index != exceptIndex && string.Equals(MCPVFXReflection.Get(
                        categories[index], "name")?.ToString(), name,
                        StringComparison.Ordinal)))
                throw new ArgumentException(
                    $"A VFX parameter category named '{name}' already exists.");
        }

        private static object NewNestedUIInfo(object ui, string nestedName)
        {
            Type type = ui.GetType().GetNestedType(nestedName,
                BindingFlags.Public | BindingFlags.NonPublic) ??
                throw new MissingMemberException(ui.GetType().FullName, nestedName);
            return Activator.CreateInstance(type);
        }

        private static Array GetArray(object target, string member)
        {
            Type arrayType = MCPVFXReflection.GetMemberType(target, member) ??
                throw new MissingMemberException(target.GetType().FullName, member);
            return MCPVFXReflection.Get(target, member) as Array ??
                   Array.CreateInstance(arrayType.GetElementType(), 0);
        }

        private static void SetArray(object target, string member, Array value)
        {
            Set(target, member, value);
        }

        private static Array Insert(Array source, object value, int index)
        {
            Type elementType = source.GetType().GetElementType();
            Array result = Array.CreateInstance(elementType, source.Length + 1);
            for (int oldIndex = 0; oldIndex < source.Length; oldIndex++)
                result.SetValue(source.GetValue(oldIndex),
                    oldIndex < index ? oldIndex : oldIndex + 1);
            result.SetValue(value, index);
            return result;
        }

        private static Array Remove(Array source, int index)
        {
            Type elementType = source.GetType().GetElementType();
            Array result = Array.CreateInstance(elementType, source.Length - 1);
            for (int oldIndex = 0; oldIndex < source.Length; oldIndex++)
            {
                if (oldIndex == index)
                    continue;
                result.SetValue(source.GetValue(oldIndex),
                    oldIndex < index ? oldIndex : oldIndex - 1);
            }
            return result;
        }

        private static void RemoveStickyReferences(object ui, int removedIndex)
        {
            foreach (object group in GetArray(ui, "groupInfos"))
            {
                Array contents = MCPVFXReflection.Get(group, "contents") as Array;
                if (contents == null)
                    continue;
                List<object> kept = contents.Cast<object>().Where(item =>
                    !(MCPVFXReflection.Get(item, "isStickyNote") is bool sticky &&
                      sticky && Convert.ToInt32(MCPVFXReflection.Get(item, "id")) ==
                      removedIndex)).ToList();
                Array replacement = Array.CreateInstance(
                    contents.GetType().GetElementType(), kept.Count);
                for (int index = 0; index < kept.Count; index++)
                    replacement.SetValue(kept[index], index);
                Set(group, "contents", replacement);
            }
        }

        private static void ReindexStickyReferences(object ui, int pivot, int delta)
        {
            foreach (object group in GetArray(ui, "groupInfos"))
            {
                Array contents = MCPVFXReflection.Get(group, "contents") as Array;
                if (contents == null)
                    continue;
                for (int index = 0; index < contents.Length; index++)
                {
                    object item = contents.GetValue(index);
                    if (!(MCPVFXReflection.Get(item, "isStickyNote") is bool sticky) ||
                        !sticky)
                        continue;
                    int id = Convert.ToInt32(MCPVFXReflection.Get(item, "id"));
                    bool shouldShift = delta > 0 ? id >= pivot : id > pivot;
                    if (!shouldShift)
                        continue;
                    Set(item, "id", id + delta);
                    contents.SetValue(item, index);
                }
            }
        }

        private static void Dirty(MCPVFXGraphMutationContext context)
        {
            object ui = UI(context);
            if (ui is UnityEngine.Object uiObject)
                EditorUtility.SetDirty(uiObject);
            if (context.Session.Graph is UnityEngine.Object graphObject)
                EditorUtility.SetDirty(graphObject);
        }

        private static void Set(object target, string member, object value)
        {
            if (!MCPVFXReflection.TrySet(target, member, value))
                throw new MissingMemberException(target.GetType().FullName, member);
        }

        private static string RequireString(Dictionary<string, object> operation,
            string key)
        {
            string value = MCPVFXGraphMutationContext.GetString(operation, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(key + " is required.");
            return value;
        }

        private static int RequireArrayIndex(Dictionary<string, object> operation,
            string key, int count)
        {
            if (!operation.ContainsKey(key))
                throw new ArgumentException(key + " is required.");
            int index = MCPVFXGraphMutationContext.GetInt(operation, key, -1);
            RequireExistingIndex(index, count, key);
            return index;
        }

        private static void RequireExistingIndex(int index, int count, string key)
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(key,
                    $"{key} must be between 0 and {Math.Max(0, count - 1)}.");
        }

        private static void RequireInsertIndex(int index, int count, string key)
        {
            if (index < 0 || index > count)
                throw new ArgumentOutOfRangeException(key,
                    $"{key} must be between 0 and {count}.");
        }

        private static Dictionary<string, object> Result(string key1,
            object value1, string key2, object value2)
        {
            return new Dictionary<string, object>
            {
                { key1, value1 }, { key2, value2 },
            };
        }
    }
}
