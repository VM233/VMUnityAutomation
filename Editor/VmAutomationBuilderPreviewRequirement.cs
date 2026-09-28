#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationBuilderPreviewRequirement
    {
        internal string Path;
        internal string ElementName;
        internal int MinImages;
        internal int MinTextEntries;
    }
}
#endif
