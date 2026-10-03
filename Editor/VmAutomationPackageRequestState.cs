using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationPackageRequestState : ScriptableSingleton<VmAutomationPackageRequestState>
    {
        [SerializeField] private string jobId;
        [SerializeField] private AddRequest addition;
        [SerializeField] private RemoveRequest removal;
        [SerializeField] private SearchRequest search;

        internal string JobId => jobId;

        internal void Issue(VmAutomationWorkspaceJob job)
        {
            if (!string.IsNullOrEmpty(jobId))
                throw new InvalidOperationException($"Package request '{job.JobId}' overlaps '{jobId}'.");
            jobId = job.JobId;
            switch (job.Operation)
            {
                case "packages/update-git":
                    addition = Client.Add(job.RequestedPackageIdentifier);
                    break;
                case "packages/add":
                    addition = Client.Add((string)job.Request["identifier"]);
                    break;
                case "packages/remove":
                    removal = Client.Remove((string)job.Request["name"]);
                    break;
                case "packages/search":
                    search = Client.Search((string)job.Request["query"]);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported package operation '{job.Operation}'.");
            }
        }

        internal Request GetRequest(VmAutomationWorkspaceJob job)
        {
            if (jobId != job.JobId)
                throw new InvalidOperationException($"Package request '{job.JobId}' lost its native owner.");
            return job.Operation switch
            {
                "packages/add" => addition,
                "packages/update-git" => addition,
                "packages/remove" => removal,
                "packages/search" => search,
                _ => throw new InvalidOperationException($"Unsupported package operation '{job.Operation}'."),
            };
        }

        internal void Retire(VmAutomationWorkspaceJob job)
        {
            if (jobId != job.JobId)
                return;
            addition = null;
            removal = null;
            search = null;
            jobId = null;
        }
    }
}
