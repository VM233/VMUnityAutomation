using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXDynamicOperatorLinking
    {
        internal static void PrepareInput(
            UnityEngine.Object model, object input, object output)
        {
            if (model == null || input == null || output == null ||
                !Convert.ToBoolean(VmAutomationVFXReflection.Invoke(
                    input, "IsMasterSlot")))
                return;

            Type unifiedType = VmAutomationVFXReflection.FindType(
                "UnityEditor.VFX.IVFXOperatorNumericUnified");
            if (unifiedType != null && unifiedType.IsInstanceOfType(model))
            {
                PrepareUnifiedInput(model, input, output);
                return;
            }

            Type uniformType = VmAutomationVFXReflection.FindType(
                "UnityEditor.VFX.IVFXOperatorUniform");
            if (uniformType != null && uniformType.IsInstanceOfType(model))
                PrepareUniformInput(model, input, output);
        }

        internal static Type GetSlotType(object slot)
        {
            return VmAutomationVFXReflection.Get(
                VmAutomationVFXReflection.Get(slot, "property"), "type") as Type;
        }

        private static void PrepareUnifiedInput(
            UnityEngine.Object model, object input, object output)
        {
            int inputIndex = Convert.ToInt32(
                VmAutomationVFXReflection.Invoke(model, "GetSlotIndex", input));
            Type bestAffinity = GetBestAffinityType(model, GetSlotType(output));
            if (inputIndex < 0 || bestAffinity == null)
                return;

            Type constrainedType = VmAutomationVFXReflection.FindType(
                "UnityEditor.VFX.IVFXOperatorNumericUnifiedConstrained");
            if (constrainedType == null ||
                !constrainedType.IsInstanceOfType(model))
            {
                VmAutomationVFXReflection.Invoke(model, "SetOperandType",
                    inputIndex, bestAffinity);
                return;
            }

            HashSet<int> sameTypeIndices = IntSet(
                VmAutomationVFXReflection.Get(model,
                    "slotIndicesThatMustHaveSameType"));
            HashSet<int> scalarIndices = IntSet(
                VmAutomationVFXReflection.Get(model,
                    "slotIndicesThatCanBeScalar"));
            if (!sameTypeIndices.Contains(inputIndex))
            {
                VmAutomationVFXReflection.Invoke(model, "SetOperandType",
                    inputIndex, bestAffinity);
                return;
            }

            if (scalarIndices.Contains(inputIndex))
            {
                object firstConstrainedInput = InputSlots(model)
                    .Where((slot, index) => sameTypeIndices.Contains(index))
                    .FirstOrDefault();
                if (firstConstrainedInput == null ||
                    GetSlotType(firstConstrainedInput) == bestAffinity)
                {
                    VmAutomationVFXReflection.Invoke(model, "SetOperandType",
                        inputIndex, bestAffinity);
                }
                else if (!CanLink(input, output))
                {
                    Type matchingScalar = GetMatchingScalar(GetSlotType(output));
                    Type scalarAffinity = GetBestAffinityType(model,
                        matchingScalar);
                    if (scalarAffinity != null)
                    {
                        VmAutomationVFXReflection.Invoke(model,
                            "SetOperandType", inputIndex, scalarAffinity);
                    }
                }
                return;
            }

            List<object> inputs = InputSlots(model);
            bool constrainedInputHasLink = inputs
                .Where((slot, index) =>
                    !ReferenceEquals(slot, input) &&
                    sameTypeIndices.Contains(index) &&
                    !scalarIndices.Contains(index))
                .Any(slot => Convert.ToBoolean(
                    VmAutomationVFXReflection.Invoke(slot, "HasLink", true)));
            if (constrainedInputHasLink && CanLink(input, output))
                return;

            Type matchingBestScalar = GetMatchingScalar(bestAffinity);
            foreach (int index in sameTypeIndices.OrderBy(value => value))
            {
                object currentInput = VmAutomationVFXReflection.Invoke(model,
                    "GetInputSlot", index);
                if (!scalarIndices.Contains(index) ||
                    GetSlotType(currentInput) != matchingBestScalar)
                {
                    VmAutomationVFXReflection.Invoke(model, "SetOperandType",
                        index, bestAffinity);
                }
            }
        }

        private static void PrepareUniformInput(
            UnityEngine.Object model, object input, object output)
        {
            int inputIndex = Convert.ToInt32(
                VmAutomationVFXReflection.Invoke(model, "GetSlotIndex", input));
            HashSet<int> staticIndices = IntSet(
                VmAutomationVFXReflection.Get(model, "staticSlotIndex"));
            if (inputIndex < 0 || staticIndices.Contains(inputIndex))
                return;

            List<object> inputs = InputSlots(model);
            bool anotherInputHasLink = inputs.Any(slot =>
                !ReferenceEquals(slot, input) && Convert.ToBoolean(
                    VmAutomationVFXReflection.Invoke(slot, "HasLink")));
            Type bestAffinity = GetBestAffinityType(model, GetSlotType(output));
            if ((!anotherInputHasLink || !CanLink(input, output)) &&
                bestAffinity != null)
            {
                VmAutomationVFXReflection.Invoke(model, "SetOperandType",
                    bestAffinity);
            }
        }

        private static bool CanLink(object input, object output)
        {
            return Convert.ToBoolean(VmAutomationVFXReflection.Invoke(
                       input, "CanLink", output)) &&
                   Convert.ToBoolean(VmAutomationVFXReflection.Invoke(
                       output, "CanLink", input));
        }

        private static Type GetBestAffinityType(
            UnityEngine.Object model, Type sourceType)
        {
            if (sourceType == null)
                return null;
            return VmAutomationVFXReflection.Invoke(model,
                "GetBestAffinityType", sourceType) as Type;
        }

        private static Type GetMatchingScalar(Type type)
        {
            if (type == null)
                return null;
            Type expressionType = VmAutomationVFXReflection.FindType(
                "UnityEditor.VFX.VFXExpression");
            return expressionType == null ? null :
                VmAutomationVFXReflection.Invoke(expressionType,
                    "GetMatchingScalar", type) as Type;
        }

        private static List<object> InputSlots(UnityEngine.Object model)
        {
            return VmAutomationVFXReflection.Enumerate(
                VmAutomationVFXReflection.Get(model, "inputSlots")).ToList();
        }

        private static HashSet<int> IntSet(object values)
        {
            return new HashSet<int>(VmAutomationVFXReflection.Enumerate(values)
                .Select(Convert.ToInt32));
        }
    }
}
