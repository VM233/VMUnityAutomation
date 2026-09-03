using System;
using System.Collections.Generic;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationInspectionTestAsset : ScriptableObject
    {
        public string label;
        public bool enabledValue;
        public Vector3 point;
        public UnityEngine.Object reference;
        public List<Entry> entries = new();

        [Serializable]
        public struct Entry
        {
            public string label;
            public int value;
        }
    }
}
