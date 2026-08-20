using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Bounded queue-compatible transport identities for every published persistent
    /// Job snapshot. These reads never enter the Unity main-thread request queue.
    /// </summary>
    internal static class VmAutomationImmediateJobStatusTicketStore
    {
        private const int Capacity = 64;
        private const long TicketIdBase = 8_000_000_000_000_000L;
        private const ulong TicketIdRange = 1_000_000_000_000_000UL;
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
        private static readonly object Sync = new object();
        private static readonly Dictionary<long, Ticket> Tickets =
            new Dictionary<long, Ticket>();
        private static readonly LinkedList<long> Order = new LinkedList<long>();

        internal static Dictionary<string, object> Submit(string agentId,
            Dictionary<string, object> arguments)
        {
            object result = VmAutomationJobHistory.GetPublishedSnapshot(arguments);

            DateTime now = DateTime.UtcNow;
            long ticketId = CreateTicketId();
            var ticket = new Ticket(ticketId, agentId, now, result);

            lock (Sync)
            {
                if (Tickets.ContainsKey(ticketId))
                {
                    throw new InvalidOperationException(
                        $"VM Unity Automation immediate Job-status ticket identity collision '{ticketId}'.");
                }
                if (Tickets.Count == Capacity)
                {
                    long expiredId = Order.First.Value;
                    Order.RemoveFirst();
                    Tickets.Remove(expiredId);
                }
                ticket.OrderNode = Order.AddLast(ticketId);
                Tickets.Add(ticketId, ticket);
            }

            return new Dictionary<string, object>
            {
                { "ticketId", ticketId },
                { "status", ticket.Status },
                { "queuePosition", 0 },
                { "agentId", ticket.AgentId },
                { "reused", false },
            };
        }

        internal static bool TryGetStatus(long ticketId, string agentId,
            out Dictionary<string, object> status)
        {
            lock (Sync)
            {
                if (!Tickets.TryGetValue(ticketId, out Ticket ticket))
                {
                    status = null;
                    return false;
                }
                if (DateTime.UtcNow - ticket.CompletedAt > Lifetime)
                {
                    Tickets.Remove(ticketId);
                    Order.Remove(ticket.OrderNode);
                    status = null;
                    return false;
                }
                if (!string.Equals(ticket.AgentId, agentId, StringComparison.Ordinal))
                {
                    status = VmAutomationResponse.Error(
                        "Ticket belongs to another agent.", "ticket_owner_mismatch");
                    return true;
                }

                status = ticket.BuildStatus();
                return true;
            }
        }

        private static long CreateTicketId()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            ulong suffix = BitConverter.ToUInt64(bytes, 0) % TicketIdRange;
            return TicketIdBase + (long)suffix;
        }

        private sealed class Ticket
        {
            internal Ticket(long ticketId, string agentId, DateTime completedAt,
                object result)
            {
                TicketId = ticketId;
                AgentId = string.IsNullOrEmpty(agentId) ? "anonymous" : agentId;
                CompletedAt = completedAt;
                if (VmAutomationResponse.TryGetError(result, out string message,
                        out string errorCode, out bool retryable))
                {
                    Status = "Failed";
                    ErrorMessage = message;
                    ErrorCode = errorCode;
                    Retryable = retryable;
                    Result = VmAutomationResponse.NormalizeError(result, errorCode, retryable);
                }
                else
                {
                    Status = "Completed";
                    Result = result;
                }
            }

            internal long TicketId { get; }
            internal string AgentId { get; }
            internal DateTime CompletedAt { get; }
            internal string Status { get; }
            internal object Result { get; }
            internal string ErrorMessage { get; }
            internal string ErrorCode { get; }
            internal bool Retryable { get; }
            internal LinkedListNode<long> OrderNode { get; set; }

            internal Dictionary<string, object> BuildStatus()
            {
                var response = new Dictionary<string, object>
                {
                    { "ticketId", TicketId },
                    { "actionName", "jobs/get" },
                    { "status", Status },
                    { "submittedAt", CompletedAt.ToString("O") },
                    { "startedAt", CompletedAt.ToString("O") },
                    { "queueWaitTimeMs", 0L },
                    { "completedAt", CompletedAt.ToString("O") },
                    { "executionTimeMs", 0L },
                    { "result", Result },
                };
                if (Status == "Failed")
                {
                    response["success"] = false;
                    response["error"] = ErrorMessage;
                    response["errorCode"] = ErrorCode;
                    if (Retryable)
                        response["retryable"] = true;
                }
                return response;
            }
        }
    }
}
