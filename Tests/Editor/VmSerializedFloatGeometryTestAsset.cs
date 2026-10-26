using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmSerializedFloatGeometryTestAsset : ScriptableObject
    {
        public Color tint = Color.white;
        public Vector2 point;
        public Vector3 position;
        public Vector4 vector;
        public Quaternion rotation = Quaternion.identity;
        public Rect rectangle;
    }
}
