using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUiStyleReferenceState
    {
        internal bool ClrNull { get; }
        internal bool Alive { get; }
        internal string RuntimeType { get; }
        internal string ObjectId { get; }
        private readonly string guid;
        private readonly string localId;

        internal VmUiStyleReferenceState(Object value)
        {
            ClrNull = ReferenceEquals(value, null);
            Alive = value != null;
            RuntimeType = ClrNull ? null : value.GetType().FullName;
            if (RuntimeType != null && RuntimeType.Length > 512)
                throw new VmProjectToolException("style_reference_budget", "A reference runtime type exceeds512 characters.");
            ObjectId = ClrNull ? null : VmObjectId.Get(value);
            if (Alive && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string assetGuid, out long fileId))
            {
                guid = assetGuid;
                localId = fileId.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal bool AgreesWith(VmUiStyleReferenceState other) =>
            ClrNull == other.ClrNull && Alive == other.Alive &&
            RuntimeType == other.RuntimeType && ObjectId == other.ObjectId;

        internal Dictionary<string, object> ToResult() => new()
        {
            ["clrNull"] = ClrNull, ["alive"] = Alive, ["runtimeType"] = RuntimeType,
            ["objectId"] = ObjectId, ["guid"] = guid, ["localFileId"] = localId
        };
    }
}
