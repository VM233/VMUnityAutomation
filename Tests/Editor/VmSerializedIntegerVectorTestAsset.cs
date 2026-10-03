using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmSerializedIntegerVectorTestAsset : ScriptableObject
    {
        public Vector2Int resolution = new(1200, 800);
        public Vector3Int position = new(1, 2, 3);
    }
}
