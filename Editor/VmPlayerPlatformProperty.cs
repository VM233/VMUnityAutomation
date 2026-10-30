using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;

namespace VMUnityAutomation.Editor
{
    internal static class VmPlayerPlatformProperty
    {
        private const int MaximumEntries = 128;

        internal static object Set(SerializedObject serialized, string setting,
            string platform, string value)
        {
            var native = NativeSetting(setting);
            Type enumType = VmAutomationAssetGraphUtility.FindType(native.EnumType);
            if (enumType == null || !enumType.IsEnum)
                throw new MissingMemberException(native.EnumType);
            if (string.IsNullOrEmpty(value) || value.Length > 128 ||
                !Enum.IsDefined(enumType, value))
                throw new ArgumentException($"{setting} requires an exact native enum name.");
            FieldInfo[] platforms = typeof(NamedBuildTarget).GetFields(BindingFlags.Public | BindingFlags.Static);
            if (string.IsNullOrEmpty(platform) || platform.Length > 128 ||
                platforms.Length > MaximumEntries || !platforms.Any(field =>
                    field.FieldType == typeof(NamedBuildTarget) &&
                    ((NamedBuildTarget)field.GetValue(null)).TargetName == platform))
                throw new ArgumentException("platform must name a native NamedBuildTarget.");
            SerializedProperty map = serialized.FindProperty(native.Property);
            if (map == null || !map.isArray || map.arraySize > MaximumEntries)
                throw new ArgumentException($"Native platform map '{native.Property}' is unavailable or exceeds 128 entries.");
            int found = -1;
            for (int index = 0; index < map.arraySize; index++)
            {
                SerializedProperty key = map.GetArrayElementAtIndex(index).FindPropertyRelative("first");
                if (key == null || key.propertyType != SerializedPropertyType.String)
                    throw new ArgumentException($"Native map '{native.Property}' has no string key.");
                if (key.stringValue != platform) continue;
                if (found >= 0) throw new ArgumentException($"Native map '{native.Property}' duplicates '{platform}'.");
                found = index;
            }
            object before = null;
            if (found < 0)
            {
                if (map.arraySize == MaximumEntries)
                    throw new ArgumentException("Native platform map has no remaining entry capacity.");
                found = map.arraySize++;
                map.GetArrayElementAtIndex(found).FindPropertyRelative("first").stringValue = platform;
            }
            else
            {
                int old = map.GetArrayElementAtIndex(found).FindPropertyRelative("second").intValue;
                if (!Enum.IsDefined(enumType, old))
                    throw new ArgumentException($"Native map '{native.Property}' contains an invalid enum value for '{platform}'.");
                before = Enum.GetName(enumType, old);
            }
            SerializedProperty target = map.GetArrayElementAtIndex(found).FindPropertyRelative("second");
            if (target == null || target.propertyType != SerializedPropertyType.Integer)
                throw new ArgumentException($"Native map '{native.Property}' has no integer enum value.");
            target.intValue = Convert.ToInt32(Enum.Parse(enumType, value));
            return before;
        }

        private static (string Property, string EnumType) NativeSetting(string setting)
        {
            switch (setting)
            {
                case "ManagedStrippingLevel": return ("managedStrippingLevel", "UnityEditor.ManagedStrippingLevel");
                case "Il2CppCompilerConfiguration": return ("il2cppCompilerConfiguration", "UnityEditor.Il2CppCompilerConfiguration");
                case "Il2CppCodeGeneration": return ("il2cppCodeGeneration", "UnityEditor.Build.Il2CppCodeGeneration");
                default: throw new ArgumentException("setting must name one supported native compiler setting.");
            }
        }
    }
}
