using System;
using System.Reflection;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAudioMixerParameterPath
    {
        internal static object Create(UnityEngine.Object group, UnityEngine.Object effect, object guid, bool groupVolume)
        {
            string typeName = groupVolume ? "UnityEditor.Audio.AudioGroupParameterPath" :
                "UnityEditor.Audio.AudioEffectParameterPath";
            Type pathType = VmAutomationAssetGraphUtility.FindType(typeName) ?? throw new TypeLoadException(typeName);
            object[] arguments = groupVolume ? new object[] { group, guid } : new object[] { group, effect, guid };
            return Activator.CreateInstance(pathType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, arguments, null);
        }
    }
}
