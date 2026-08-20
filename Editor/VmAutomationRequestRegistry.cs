using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationRequestRegistry
    {
        private const int Capacity = 256;
        private static readonly object Sync = new();
        private static readonly Dictionary<string, Entry> Entries =
            new(StringComparer.Ordinal);
        private static readonly LinkedList<string> Order = new();

        internal static Task<VmAutomationInvocationResult> Execute(
            string requestId,
            string fingerprint,
            Func<Task<VmAutomationInvocationResult>> factory,
            Func<VmAutomationInvocationResult> conflictFactory)
        {
            TaskCompletionSource<VmAutomationInvocationResult> completion;
            lock (Sync)
            {
                if (Entries.TryGetValue(requestId, out Entry existing))
                {
                    return string.Equals(existing.Fingerprint, fingerprint,
                            StringComparison.Ordinal)
                        ? existing.Task
                        : Task.FromResult(conflictFactory());
                }

                completion = new TaskCompletionSource<VmAutomationInvocationResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                LinkedListNode<string> node = Order.AddLast(requestId);
                Entries.Add(requestId, new Entry(fingerprint, completion.Task, node));
                TrimCompletedEntries();
            }

            _ = CompleteAsync(factory, completion);
            return completion.Task;
        }

        private static async Task CompleteAsync(
            Func<Task<VmAutomationInvocationResult>> factory,
            TaskCompletionSource<VmAutomationInvocationResult> completion)
        {
            try
            {
                completion.TrySetResult(await factory());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        private static void TrimCompletedEntries()
        {
            LinkedListNode<string> node = Order.First;
            while (Entries.Count > Capacity && node != null)
            {
                LinkedListNode<string> next = node.Next;
                Entry entry = Entries[node.Value];
                if (entry.Task.IsCompleted)
                {
                    Entries.Remove(node.Value);
                    Order.Remove(node);
                }
                node = next;
            }
        }

        private sealed class Entry
        {
            internal Entry(
                string fingerprint,
                Task<VmAutomationInvocationResult> task,
                LinkedListNode<string> node)
            {
                Fingerprint = fingerprint;
                Task = task;
                Node = node;
            }

            internal string Fingerprint { get; }

            internal Task<VmAutomationInvocationResult> Task { get; }

            internal LinkedListNode<string> Node { get; }
        }
    }
}
