using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationVFXRuntimeState
    {
        private int discoveredRecords;

        internal Dictionary<string, object> Inspect(Component component,
            int systemOffset, int maxSystems, int outputEventOffset,
            int maxOutputEvents)
        {
            if (VmAutomationVFXReflection.Get(component, "visualEffectAsset") == null)
                throw new InvalidOperationException(
                    "Runtime VFX state requires an assigned VisualEffectAsset.");

            List<string> systemNames = Names(component, "GetSystemNames");
            List<string> particleNames = Names(component,
                "GetParticleSystemNames");
            List<string> spawnNames = Names(component, "GetSpawnSystemNames");
            List<string> outputEventNames = Names(component,
                "GetOutputEventNames");
            AddDiscovered(systemNames.Count + particleNames.Count +
                          spawnNames.Count + outputEventNames.Count);

            var particles = new HashSet<string>(particleNames,
                StringComparer.Ordinal);
            var spawners = new HashSet<string>(spawnNames,
                StringComparer.Ordinal);
            List<string> unowned = particles.Concat(spawners).Where(name =>
                    !systemNames.Contains(name, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal).ToList();
            if (unowned.Count > 0)
                throw new InvalidOperationException(
                    "VisualEffect.GetSystemNames omitted declared particle or spawn systems: " +
                    string.Join(", ", unowned) + ".");

            List<Dictionary<string, object>> systems = systemNames
                .Skip(systemOffset).Take(maxSystems)
                .Select(name => SystemSummary(component, name,
                    particles.Contains(name), spawners.Contains(name)))
                .ToList();
            List<string> outputEvents = outputEventNames.Skip(outputEventOffset)
                .Take(maxOutputEvents).ToList();
            return new Dictionary<string, object>
            {
                { "hasAnySystemAwake", Convert.ToBoolean(
                    VmAutomationVFXReflection.Invoke(component, "HasAnySystemAwake")) },
                { "systemCount", systemNames.Count },
                { "systemOffset", systemOffset },
                { "returnedSystemCount", systems.Count },
                { "systemsTruncated", systemOffset + systems.Count <
                    systemNames.Count },
                { "nextSystemOffset", systemOffset + systems.Count <
                    systemNames.Count ? (object)(systemOffset + systems.Count) :
                    null },
                { "systems", systems },
                { "outputEventCount", outputEventNames.Count },
                { "outputEventOffset", outputEventOffset },
                { "returnedOutputEventCount", outputEvents.Count },
                { "outputEventsTruncated", outputEventOffset +
                    outputEvents.Count < outputEventNames.Count },
                { "nextOutputEventOffset", outputEventOffset +
                    outputEvents.Count < outputEventNames.Count
                        ? (object)(outputEventOffset + outputEvents.Count) : null },
                { "outputEvents", outputEvents },
            };
        }

        private static Dictionary<string, object> SystemSummary(
            Component component, string name, bool particle, bool spawner)
        {
            var result = new Dictionary<string, object>
            {
                { "name", name },
                { "particleSystem", particle },
                { "spawnSystem", spawner },
            };
            if (particle)
            {
                object info = VmAutomationVFXReflection.Invoke(component,
                    "GetParticleSystemInfo", name);
                result["particleState"] = new Dictionary<string, object>
                {
                    { "aliveCount", VmAutomationVFXValueCodec.Sanitize(
                        VmAutomationVFXReflection.Get(info, "aliveCount")) },
                    { "capacity", VmAutomationVFXValueCodec.Sanitize(
                        VmAutomationVFXReflection.Get(info, "capacity")) },
                    { "sleeping", VmAutomationVFXReflection.Get(info, "sleeping") },
                    { "bounds", VmAutomationVFXValueCodec.Sanitize(
                        VmAutomationVFXReflection.Get(info, "bounds")) },
                };
            }
            if (spawner)
            {
                object state = VmAutomationVFXReflection.Invoke(component,
                    "GetSpawnSystemInfo", name);
                try
                {
                    result["spawnState"] = SpawnStateSummary(state);
                }
                finally
                {
                    if (state is IDisposable disposable)
                        disposable.Dispose();
                }
            }
            return result;
        }

        private static Dictionary<string, object> SpawnStateSummary(object state)
        {
            var result = new Dictionary<string, object>();
            foreach (string member in new[]
                     {
                         "delayAfterLoop", "delayBeforeLoop", "deltaTime",
                         "loopCount", "loopDuration", "loopIndex", "loopState",
                         "newLoop", "playing", "spawnCount", "totalTime",
                     })
                result[member] = VmAutomationVFXValueCodec.Sanitize(
                    VmAutomationVFXReflection.Get(state, member));
            return result;
        }

        private static List<string> Names(Component component,
            string methodName)
        {
            var names = new List<string>();
            VmAutomationVFXReflection.Invoke(component, methodName, names);
            if (names.Count > VmAutomationVFXLimits.CollectionItems)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"{methodName} returned more than {VmAutomationVFXLimits.CollectionItems} names.");
            if (names.Any(string.IsNullOrEmpty))
                throw new InvalidOperationException(
                    $"{methodName} returned an empty VFX name.");
            if (names.Distinct(StringComparer.Ordinal).Count() != names.Count)
                throw new InvalidOperationException(
                    $"{methodName} returned duplicate VFX names.");
            return names;
        }

        private void AddDiscovered(int count)
        {
            discoveredRecords += count;
            if (discoveredRecords > VmAutomationVFXLimits.ReturnedRuntimeRecordsPerRequest)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"Component inspection discovered more than {VmAutomationVFXLimits.ReturnedRuntimeRecordsPerRequest} runtime system and event records.");
        }
    }
}
