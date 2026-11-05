using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal static class VmUiTransitionPublicationGraph
    {
        internal static HashSet<string> Covered(IEnumerable<VmUiTransitionSourceEdge> edges, bool nativeDriver = false)
        {
            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            var next = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var edge in edges)
            {
                if (edge.Kind == "StateTransition") continue;
                if (!incoming.ContainsKey(edge.To)) incoming.Add(edge.To, 0);
                if (incoming.Count > 24000) throw new InvalidOperationException("UI publication graph exceeds24000 methods.");
                if (nativeDriver ? edge.Kind == "NativeDriverCallback" : edge.Kind is "PublicationCallback" or "InitializationCallback") continue;
                incoming[edge.To]++;
                if (edge.Kind is "DeferredCallback" or "AsyncCall" or "NativeDriverCallback" or "PublicationCallback" or "InitializationCallback") continue;
                if (!next.TryGetValue(edge.From, out var targets)) next.Add(edge.From, targets = new List<string>());
                targets.Add(edge.To);
            }
            var covered = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            foreach (var pair in incoming)
                if (pair.Value == 0 && covered.Add(pair.Key)) pending.Enqueue(pair.Key);
            while (pending.Count > 0)
            {
                string current = pending.Dequeue();
                if (!next.TryGetValue(current, out var targets)) continue;
                foreach (string target in targets)
                    if (--incoming[target] == 0 && covered.Add(target)) pending.Enqueue(target);
            }
            return covered;
        }
    }
}
