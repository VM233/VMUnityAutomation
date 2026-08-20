using System.Collections.Generic;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class MCPTransformSerialization
    {
        public static void AddWorld(Dictionary<string, object> target, Transform transform,
            bool includeScale = false)
        {
            AddVectorIfDifferent(target, "position", transform.position, Vector3.zero);
            AddRotationIfDifferent(target, "rotation", transform.rotation, transform.eulerAngles);
            if (includeScale)
                AddVectorIfDifferent(target, "lossyScale", transform.lossyScale, Vector3.one);
        }

        public static void AddLocal(Dictionary<string, object> target, Transform transform,
            string positionKey = "localPosition", string rotationKey = "localRotation",
            string scaleKey = "localScale")
        {
            AddVectorIfDifferent(target, positionKey, transform.localPosition, Vector3.zero);
            AddRotationIfDifferent(target, rotationKey, transform.localRotation, transform.localEulerAngles);
            AddVectorIfDifferent(target, scaleKey, transform.localScale, Vector3.one);
        }

        public static void AddVectorIfDifferent(Dictionary<string, object> target, string key, Vector3 value,
            Vector3 defaultValue)
        {
            if (Approximately(value, defaultValue) == false)
                target[key] = MCPGameObjectCommands.Vector3ToDict(value);
        }

        private static void AddRotationIfDifferent(Dictionary<string, object> target, string key,
            Quaternion value, Vector3 eulerAngles)
        {
            if (Mathf.Abs(Quaternion.Dot(value, Quaternion.identity)) < 1f - 0.000001f)
                target[key] = MCPGameObjectCommands.Vector3ToDict(eulerAngles);
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                   Mathf.Approximately(left.y, right.y) &&
                   Mathf.Approximately(left.z, right.z);
        }
    }
}
