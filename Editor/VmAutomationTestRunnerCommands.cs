using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Automation command handler for running Unity Test Runner tests.
    /// Uses an async job-based pattern: start a test run (returns job ID),
    /// then poll for status/results via the job ID.
    /// </summary>
    [InitializeOnLoad]
    public static class VmAutomationTestRunnerCommands
    {
        internal const string JobType = "unity-test";
        // ─── Job Tracking ────────────────────────────────────────────

        private static readonly Dictionary<string, TestJob> _jobs = new Dictionary<string, TestJob>();
        private static string _currentJobId;
        private static volatile bool _isTestRunActive;
        private static readonly VmAutomationTestJobSession Session =
            new VmAutomationTestJobSession("VmAutomationTestRunner.v2");
        private static TestRunnerApi _testRunnerApi;
        private static VmAutomationTestCallbacks _callbacks;

        internal const int MaxFailuresTracked = 50;
        private const double StuckThresholdSeconds = 120.0;
        private const double CompletionCallbackGraceSeconds = 0.5;
        private const double JobExpiryMinutes = 30.0;

        // ─── PlayMode Domain Reload Guard ────────────────────────────
        private const string PlayModeGuardKey = "VmAutomationTestRunner_PlayModeGuard";
        private const string PlayModeOriginalEnabledKey = "VmAutomationTestRunner_OriginalPMOEnabled";
        private const string PlayModeOriginalOptionsKey = "VmAutomationTestRunner_OriginalPMOOptions";

        static VmAutomationTestRunnerCommands()
        {
            // Restore state after domain reload
            RestoreFromSessionState();
            SetTestRunActive(_currentJobId != null &&
                             _jobs.TryGetValue(_currentJobId, out var restoredJob) &&
                             IsActive(restoredJob.Status));

            // Re-register callbacks if a test job is in progress
            // The session records preserve in-flight progress across reloads.
            if (_currentJobId != null && _jobs.TryGetValue(_currentJobId, out var job)
                && IsActive(job.Status))
            {
                EnsureCallbacksRegistered();
                Debug.Log($"[Automation TestRunner] Re-registered callbacks after domain reload for job {_currentJobId}");
            }

            // Restore PlayMode options if test run completed during Play Mode
            // but OnRunFinished didn't fire (crash recovery)
            if (SessionState.GetBool(PlayModeGuardKey, false) && _currentJobId == null)
            {
                RestorePlayModeOptions();
            }
        }

        // ─── Public API ──────────────────────────────────────────────

        /// <summary>
        /// Start a test run. Returns a job ID immediately.
        /// Route: testing/run-tests
        /// </summary>
        public static object RunTests(Dictionary<string, object> args)
        {
            // Check if a test run is already in progress
            if (_currentJobId != null && _jobs.TryGetValue(_currentJobId, out var existing)
                && IsActive(existing.Status))
            {
                // Allow force-clear of stuck jobs
                bool clearStuck = args.ContainsKey("clearStuck") && Convert.ToBoolean(args["clearStuck"]);
                if (clearStuck)
                {
                    existing.Status = TestJobStatus.Failed;
                    existing.Error = "Force-cleared by user";
                    existing.CompletedAt = DateTime.UtcNow;
                    _currentJobId = null;
                    SetTestRunActive(false);
                    SaveToSessionState(existing);
                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "message", "Stuck job cleared" },
                        { "clearedJobId", existing.JobId }
                    };
                }

                return new Dictionary<string, object>
                {
                    { "error", "A test run is already in progress" },
                    { "currentJobId", _currentJobId },
                    { "hint", "Use clearStuck=true to force-clear if the job appears stuck" }
                };
            }

            // Check for Play Mode — don't run tests while playing
            if (EditorApplication.isPlaying)
            {
                return new Dictionary<string, object>
                {
                    { "error", "Cannot run tests while Play Mode is active. Stop the scene first." }
                };
            }

            // Check for compilation
            if (EditorApplication.isCompiling)
            {
                return new Dictionary<string, object>
                {
                    { "error", "Cannot run tests while scripts are compiling. Wait for compilation to finish." }
                };
            }

            // Parse mode
            string modeStr = args.ContainsKey("mode") ? args["mode"].ToString() : "EditMode";
            TestMode testMode;
            switch (modeStr.ToLowerInvariant())
            {
                case "editmode":
                case "edit":
                    testMode = TestMode.EditMode;
                    break;
                case "playmode":
                case "play":
                    testMode = TestMode.PlayMode;
                    break;
                default:
                    return new Dictionary<string, object>
                    {
                        { "error", $"Unknown test mode: {modeStr}. Use 'EditMode' or 'PlayMode'." }
                    };
            }

            // Parse filters
            string[] testNames = ParseStringArray(args, "testNames");
            string[] testCategories = ParseStringArray(args, "categories");
            string[] assemblyNames = ParseStringArray(args, "assemblies");
            string[] groupNames = ParseStringArray(args, "groupNames");

            // Create the job
            var job = new TestJob
            {
                JobId = Guid.NewGuid().ToString("N").Substring(0, 12),
                Mode = testMode,
                Status = TestJobStatus.Running,
                StartedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                TestNames = testNames,
                Categories = testCategories,
                Assemblies = assemblyNames,
                HasExplicitFilters = (testNames?.Length ?? 0) > 0 ||
                                     (testCategories?.Length ?? 0) > 0 ||
                                     (assemblyNames?.Length ?? 0) > 0 ||
                                     (groupNames?.Length ?? 0) > 0,
                AgentId = args != null && args.TryGetValue("_agentId", out object agentValue)
                    ? agentValue?.ToString()
                    : "anonymous",
            };

            _jobs[job.JobId] = job;
            _currentJobId = job.JobId;
            SetTestRunActive(true);

            // Clean up old jobs
            CleanupExpiredJobs();
            SaveToSessionState(job);
            Session.PublishMembership(_jobs.Keys);

            // Build the filter
            var filter = new Filter
            {
                testMode = testMode,
            };

            if (testNames != null && testNames.Length > 0)
                filter.testNames = testNames;
            if (testCategories != null && testCategories.Length > 0)
                filter.categoryNames = testCategories;
            if (assemblyNames != null && assemblyNames.Length > 0)
                filter.assemblyNames = assemblyNames;
            if (groupNames != null && groupNames.Length > 0)
                filter.groupNames = groupNames;

            // For PlayMode tests, disable domain reload to preserve callbacks
            if (testMode == TestMode.PlayMode)
            {
                SaveAndDisableDomainReload();
            }

            // Start the test run
            EnsureCallbacksRegistered();

            var executionSettings = new ExecutionSettings(filter);
            try
            {
                job.RunGuid = _testRunnerApi.Execute(executionSettings);
                SaveToSessionState(job);
            }
            catch (Exception ex)
            {
                job.Status = TestJobStatus.Failed;
                job.Error = ex.GetBaseException().Message;
                job.ErrorCode = "test_start_failed";
                job.CompletedAt = DateTime.UtcNow;
                _currentJobId = null;
                SetTestRunActive(false);
                SaveToSessionState(job);
                return VmAutomationResponse.Error(job.Error, job.ErrorCode, false,
                    new Dictionary<string, object> { { "jobId", job.JobId } });
            }

            Debug.Log($"[Automation TestRunner] Started test job {job.JobId} (mode={testMode})");

            var response = new Dictionary<string, object>
            {
                { "success", true },
                { "jobId", job.JobId },
                { "jobType", JobType },
                { "status", "running" },
                { "mode", testMode.ToString() }
            };
            VmAutomationJobHistory.PublishAccessToken(response, JobType, job.JobId, job.AgentId);
            return response;
        }

        internal static object CancelTestJob(Dictionary<string, object> args)
        {
            string jobId = args != null && args.TryGetValue("jobId", out object jobValue)
                ? jobValue?.ToString()
                : null;
            if (string.IsNullOrEmpty(jobId) || !_jobs.TryGetValue(jobId, out var job))
                return VmAutomationResponse.Error($"Test job '{jobId}' was not found.", "job_not_found");

            if (!VmAutomationJobHistory.CanAccess(JobType, job.JobId,
                    job.AgentId ?? "anonymous", args))
                return VmAutomationResponse.Error(
                    "Test job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            if (!IsActive(job.Status))
                return VmAutomationResponse.Error("Test job is already terminal.", "job_already_terminal",
                    false, SerializeJob(job, false, false, false, 0, 100, 20));
            if (string.IsNullOrEmpty(job.RunGuid))
                return VmAutomationResponse.Error("Unity Test Runner has not assigned a run GUID yet.",
                    "job_cancel_not_ready", true, new Dictionary<string, object>
                    {
                        { "jobId", job.JobId },
                        { "status", job.Status.ToString().ToLowerInvariant() },
                    });

            bool accepted;
            try
            {
                accepted = TestRunnerApi.CancelTestRun(job.RunGuid);
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(exception.GetBaseException().Message, "job_cancel_failed");
            }

            if (!accepted)
                return VmAutomationResponse.Error("Unity Test Runner rejected the cancellation request.",
                    "job_cancel_rejected", true, new Dictionary<string, object>
                    {
                        { "jobId", job.JobId },
                        { "runGuid", job.RunGuid },
                    });

            job.CancelRequested = true;
            job.Status = TestJobStatus.Canceling;
            job.LastUpdatedAt = DateTime.UtcNow;
            SaveToSessionState(job);
            return new Dictionary<string, object>
            {
                { "success", true },
                { "jobId", job.JobId },
                { "jobType", JobType },
                { "status", "canceling" },
                { "cancelRequested", true },
                { "cancelMode", "unity-test-runner" },
            };
        }

        /// <summary>
        /// Get the status/results of a test job.
        /// Route: testing/get-job
        /// </summary>
        internal static bool IsTestRunActive => _isTestRunActive;

        private static void SetTestRunActive(bool active)
        {
            _isTestRunActive = active;
            VmAutomationRuntimeState.SetBusyReason(active ? "test_run" : null);
        }

        public static object GetTestJob(Dictionary<string, object> args)
        {
            string jobId = args.ContainsKey("jobId") ? args["jobId"].ToString() : null;
            if (string.IsNullOrEmpty(jobId))
            {
                // If no jobId, return the current/latest job
                if (_currentJobId != null)
                    jobId = _currentJobId;
                else if (_jobs.Count > 0)
                    jobId = _jobs.Values.OrderByDescending(j => j.StartedAt).First().JobId;
                else
                    return new Dictionary<string, object> { { "error", "No test jobs found" } };
            }

            if (!_jobs.TryGetValue(jobId, out var job))
            {
                return new Dictionary<string, object>
                {
                    { "error", $"Job '{jobId}' not found" },
                    { "availableJobs", _jobs.Keys.ToArray() }
                };
            }
            if (!VmAutomationJobHistory.CanAccess(JobType, job.JobId,
                    job.AgentId ?? "anonymous", args))
                return VmAutomationResponse.Error(
                    "Test job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");

            bool includeDetails = args.ContainsKey("includeDetails") && Convert.ToBoolean(args["includeDetails"]);
            bool includeFailedOnly = args.ContainsKey("includeFailedOnly") && Convert.ToBoolean(args["includeFailedOnly"]);
            bool includeStackTrace = args.ContainsKey("includeStackTrace") &&
                                     Convert.ToBoolean(args["includeStackTrace"]);
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(GetInt(args, "limit", 100), 500));
            int failureLimit = Math.Max(1, Math.Min(GetInt(args, "failureLimit", 20), 100));

            TryFinalizeFromLeafResults(job);
            var result = SerializeJob(job, includeDetails, includeFailedOnly, includeStackTrace, offset, limit,
                failureLimit);
            VmAutomationJobHistory.PublishAccessToken(result, JobType, job.JobId, job.AgentId);
            return result;
        }

        /// <summary>
        /// List available tests (discovery).
        /// Route: testing/list-tests
        ///
        /// Uses a callback because RetrieveTestList fires its callback on the
        /// next editor frame, not synchronously. The bridge's deferred execution
        /// path retains the invocation until resolve is called.
        /// </summary>
        public static void ListTests(Dictionary<string, object> args, Action<object> resolve)
        {
            string modeStr = args.ContainsKey("mode") ? args["mode"].ToString() : "EditMode";
            TestMode testMode;
            switch (modeStr.ToLowerInvariant())
            {
                case "editmode":
                case "edit":
                    testMode = TestMode.EditMode;
                    break;
                case "playmode":
                case "play":
                    testMode = TestMode.PlayMode;
                    break;
                default:
                    resolve(new Dictionary<string, object>
                    {
                        { "error", $"Unknown test mode: {modeStr}. Use 'EditMode' or 'PlayMode'." }
                    });
                    return;
            }

            string nameFilter = args.ContainsKey("nameFilter") ? args["nameFilter"].ToString() : null;
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int maxResults = Math.Max(1, Math.Min(GetInt(args, "maxResults", 100), 500));

            EnsureCallbacksRegistered();

            _testRunnerApi.RetrieveTestList(testMode, root =>
            {
                if (root == null)
                {
                    resolve(new Dictionary<string, object>
                    {
                        { "error", "Unity returned a null test tree." },
                        { "mode", testMode.ToString() }
                    });
                    return;
                }

                var tests = new List<Dictionary<string, object>>();
                int totalMatches = 0;
                CollectLeafTests(root, tests, nameFilter, offset, maxResults, ref totalMatches);
                int nextOffset = offset + tests.Count;

                resolve(new Dictionary<string, object>
                {
                    { "mode", testMode.ToString() },
                    { "totalTests", totalMatches },
                    { "returnedTests", tests.Count },
                    { "offset", offset },
                    { "limit", maxResults },
                    { "truncated", nextOffset < totalMatches },
                    { "hasMore", nextOffset < totalMatches },
                    { "nextOffset", nextOffset < totalMatches ? (object)nextOffset : null },
                    { "tests", tests }
                });
            });
        }

        // ─── Test Runner Callbacks ───────────────────────────────────

        private static void EnsureCallbacksRegistered()
        {
            if (_testRunnerApi == null)
            {
                _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            }

            if (_callbacks == null)
            {
                _callbacks = new VmAutomationTestCallbacks();
                _testRunnerApi.RegisterCallbacks(_callbacks);
            }
        }

        internal static void OnRunStarted(int totalTests)
        {
            if (_currentJobId == null || !_jobs.TryGetValue(_currentJobId, out var job))
                return;

            job.TotalTests = totalTests;
            job.CompletedTests = 0;
            job.LastUpdatedAt = DateTime.UtcNow;
            SaveToSessionState(job);
            Debug.Log($"[Automation TestRunner] Job {job.JobId}: Run started, {totalTests} tests to execute");
        }

        internal static void OnTestStarted(string testFullName)
        {
            if (_currentJobId == null || !_jobs.TryGetValue(_currentJobId, out var job))
                return;

            job.CurrentTestName = testFullName;
            job.CurrentTestStartedAt = DateTime.UtcNow;
            SaveToSessionState(job);
        }

        internal static void OnTestFinished(string testFullName, string testName, TestStatus resultStatus,
            double durationSeconds, string message, string stackTrace)
        {
            if (_currentJobId == null || !_jobs.TryGetValue(_currentJobId, out var job))
                return;

            var result = new TestResult(testFullName, testName, resultStatus.ToString(),
                durationSeconds, message, stackTrace);
            Session.PublishResult(job.JobId, job.CompletedTests, result);
            job.CompletedTests++;
            job.CurrentTestName = null;
            job.CurrentTestStartedAt = null;
            job.LastUpdatedAt = DateTime.UtcNow;

            job.AllResults.Add(result);

            if (result.IsFailure && job.FailuresSoFar.Count < MaxFailuresTracked)
                job.FailuresSoFar.Add(result);
            if (resultStatus == TestStatus.Failed)
            {
                job.FailedCount++;
            }
            else if (resultStatus == TestStatus.Passed)
            {
                job.PassedCount++;
            }
            else
            {
                job.SkippedCount++;
            }

            SaveToSessionState(job);
        }

        internal static void OnRunFinished(int totalPassed, int totalFailed, int totalSkipped,
            int totalInconclusive, double totalDuration, IReadOnlyCollection<TestResult> leafResults)
        {
            if (_currentJobId == null || !_jobs.TryGetValue(_currentJobId, out var job))
                return;

            var completedResults = leafResults.ToList();
            int completedCount = totalPassed + totalFailed + totalSkipped + totalInconclusive;
            if (completedResults.Count != completedCount)
                throw new InvalidOperationException($"Test Runner job {job.JobId}: canonical result count " +
                    $"{completedResults.Count} does not match completed count {completedCount}.");
            Session.ReplaceResults(job.JobId, job.AllResults.Count, completedResults);
            job.AllResults = completedResults;
            job.FailuresSoFar = completedResults.Where(result => result.IsFailure)
                .Take(MaxFailuresTracked).ToList();
            job.PassedCount = totalPassed;
            job.FailedCount = totalFailed;
            job.SkippedCount = totalSkipped + totalInconclusive;
            job.CompletedTests = completedCount;
            job.TotalTests = Math.Max(job.TotalTests, completedCount);
            FinalizeJob(job, totalDuration, false);

            Debug.Log($"[Automation TestRunner] Job {job.JobId}: Finished — " +
                      $"{totalPassed} passed, {totalFailed} failed, {totalSkipped} skipped " +
                       $"({totalDuration:F1}s)");
        }

        private static void TryFinalizeFromLeafResults(TestJob job)
        {
            if (!IsActive(job.Status) || job.TotalTests <= 0 ||
                job.CompletedTests < job.TotalTests || job.CurrentTestName != null)
                return;

            if ((DateTime.UtcNow - job.LastUpdatedAt).TotalSeconds < CompletionCallbackGraceSeconds)
                return;

            double duration = job.AllResults.Sum(result => result.Duration);
            FinalizeJob(job, duration, true);
            Debug.LogWarning($"[Automation TestRunner] Job {job.JobId}: RunFinished callback was late; " +
                             "finalized from completed leaf results.");
        }

        private static void FinalizeJob(TestJob job, double totalDuration, bool recoveredFromLeafResults)
        {
            if (job.CancelRequested)
            {
                job.Status = TestJobStatus.Canceled;
                job.Error = "Canceled by request.";
                job.ErrorCode = "job_canceled";
            }
            else if (job.TotalTests == 0 && job.HasExplicitFilters)
            {
                job.Status = TestJobStatus.Failed;
                job.Error = "No tests matched the requested filters.";
                job.ErrorCode = "no_tests_matched";
            }
            else
            {
                job.Status = job.FailedCount > 0 ? TestJobStatus.Failed : TestJobStatus.Succeeded;
            }
            job.CompletedAt = DateTime.UtcNow;
            job.TotalDuration = totalDuration;
            job.CurrentTestName = null;
            job.CurrentTestStartedAt = null;
            job.CompletionRecovered = recoveredFromLeafResults;

            if (job.Mode == TestMode.PlayMode)
                RestorePlayModeOptions();

            if (_currentJobId == job.JobId)
            {
                _currentJobId = null;
                SetTestRunActive(false);
            }
            SaveToSessionState(job);
        }

        // ─── Serialization ───────────────────────────────────────────

        internal static Dictionary<string, object> SerializeJob(TestJob job, bool includeDetails,
            bool includeFailedOnly, bool includeStackTrace, int offset, int limit, int failureLimit)
        {
            var result = new Dictionary<string, object>
            {
                // Polling found and serialized the requested job. Its own outcome is represented
                // independently by status and error.
                { "success", true },
                { "jobId", job.JobId },
                { "jobType", JobType },
                { "status", job.Status.ToString().ToLowerInvariant() },
                { "mode", job.Mode.ToString() },
                { "startedAt", job.StartedAt.ToString("O") },
            };

            // Progress info
            var progress = new Dictionary<string, object>
            {
                { "completed", job.CompletedTests },
                { "total", job.TotalTests },
                { "passed", job.PassedCount },
                { "failed", job.FailedCount },
                { "skipped", job.SkippedCount },
            };

            if (job.CurrentTestName != null)
            {
                progress["currentTest"] = job.CurrentTestName;
                if (job.CurrentTestStartedAt.HasValue)
                {
                    double elapsed = (DateTime.UtcNow - job.CurrentTestStartedAt.Value).TotalSeconds;
                    progress["currentTestElapsed"] = Math.Round(elapsed, 1);
                    progress["stuckSuspected"] = elapsed > StuckThresholdSeconds;
                }
            }

            if (job.FailuresSoFar.Count > 0)
            {
                progress["failuresSoFar"] = job.FailuresSoFar.Take(failureLimit).Select(f =>
                    new Dictionary<string, object>
                {
                    { "name", f.Name },
                    { "fullName", f.FullName },
                    { "message", f.Message ?? "" },
                }).ToList();
                progress["totalFailuresSoFar"] = job.FailuresSoFar.Count;
                progress["failuresTruncated"] = job.FailuresSoFar.Count > failureLimit;
            }

            // Blocked reason detection
            if (IsActive(job.Status))
            {
                if (EditorApplication.isCompiling)
                    progress["blockedReason"] = "compiling";
                else if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                    progress["editorActive"] = false;
            }

            result["progress"] = progress;

            if (job.CompletedAt.HasValue)
            {
                result["completedAt"] = job.CompletedAt.Value.ToString("O");
                result["totalDuration"] = job.TotalDuration;
            }

            if (job.Error != null)
                result["error"] = job.Error;
            if (job.ErrorCode != null)
                result["errorCode"] = job.ErrorCode;
            if (job.CompletionRecovered)
                result["completionRecoveredFromLeafResults"] = true;

            // Summary
            result["summary"] = new Dictionary<string, object>
            {
                { "total", job.TotalTests },
                { "passed", job.PassedCount },
                { "failed", job.FailedCount },
                { "skipped", job.SkippedCount },
                { "duration", job.TotalDuration }
            };

            if (includeDetails || includeFailedOnly)
            {
                IEnumerable<TestResult> tests = job.AllResults;
                if (includeFailedOnly)
                    tests = tests.Where(t => t.Status == "Failed" || t.Status == "Inconclusive");

                var filteredTests = tests.ToList();
                var page = filteredTests.Skip(offset).Take(limit).ToList();
                result["tests"] = page.Select(t =>
                {
                    var test = new Dictionary<string, object>
                    {
                        { "name", t.Name },
                        { "fullName", t.FullName },
                        { "status", t.Status },
                        { "duration", t.Duration },
                    };
                    if (string.IsNullOrEmpty(t.Message) == false)
                        test["message"] = t.Message;
                    if (includeStackTrace && string.IsNullOrEmpty(t.StackTrace) == false)
                        test["stackTrace"] = t.StackTrace;
                    return test;
                }).ToList();
                int nextOffset = offset + page.Count;
                result["resultOffset"] = offset;
                result["resultLimit"] = limit;
                result["totalResults"] = filteredTests.Count;
                result["returnedResults"] = page.Count;
                result["resultsTruncated"] = nextOffset < filteredTests.Count;
                result["hasMoreResults"] = nextOffset < filteredTests.Count;
                result["nextResultOffset"] = nextOffset < filteredTests.Count ? (object)nextOffset : null;
                }

            return result;
        }

        // ─── Test Discovery Helpers ──────────────────────────────────

        private static void CollectLeafTests(ITestAdaptor test, List<Dictionary<string, object>> results,
            string nameFilter, int offset, int maxResults, ref int totalMatches)
        {
            if (!test.HasChildren)
            {
                // Leaf test
                if (nameFilter != null && test.FullName.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                int matchIndex = totalMatches++;
                if (matchIndex < offset || results.Count >= maxResults)
                    return;

                results.Add(new Dictionary<string, object>
                {
                    { "name", test.Name },
                    { "fullName", test.FullName },
                    { "categories", test.Categories?.ToArray() ?? Array.Empty<string>() },
                    { "runState", test.RunState.ToString() }
                });
            }
            else
            {
                foreach (var child in test.Children)
                    CollectLeafTests(child, results, nameFilter, offset, maxResults, ref totalMatches);
            }
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;
            return int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }

        // ─── Session State Persistence ───────────────────────────────

        private static void SaveToSessionState(TestJob job)
        {
            Session.PublishJob(job, _currentJobId);
            VmAutomationJobHistory.Record(JobType, job.JobId, job.AgentId, job.Status.ToString(),
                SerializeJob(job, false, false, false, 0, 100, 20));
        }

        private static void RestoreFromSessionState()
        {
            string current = Session.CurrentJobId;
            _currentJobId = current.Length == 0 ? null : current;
            foreach (string jobId in Session.ReadMembership())
            {
                TestJob job = Session.ReadJob(jobId);
                _jobs.Add(jobId, job);
            }
        }

        private static void CleanupExpiredJobs()
        {
            var expired = _jobs.Values
                .Where(j => !IsActive(j.Status) && j.CompletedAt.HasValue &&
                            (DateTime.UtcNow - j.CompletedAt.Value).TotalMinutes > JobExpiryMinutes)
                .ToList();
            foreach (TestJob job in expired)
            {
                _jobs.Remove(job.JobId);
                Session.RetireJob(job.JobId, job.AllResults.Count);
            }
        }

        // ─── PlayMode Domain Reload Guard ────────────────────────────

        /// <summary>
        /// Save current EnterPlayModeOptions and disable domain reload.
        /// This prevents Unity from destroying our callbacks when entering Play Mode.
        /// </summary>
        private static void SaveAndDisableDomainReload()
        {
            // Save original settings
            SessionState.SetBool(PlayModeOriginalEnabledKey, EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(PlayModeOriginalOptionsKey, (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetBool(PlayModeGuardKey, true);

            // Enable enter play mode options with domain reload disabled
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EditorSettings.enterPlayModeOptions | EnterPlayModeOptions.DisableDomainReload;

            Debug.Log("[Automation TestRunner] Disabled domain reload for PlayMode tests");
        }

        /// <summary>
        /// Restore original EnterPlayModeOptions after PlayMode tests complete.
        /// </summary>
        private static void RestorePlayModeOptions()
        {
            if (!SessionState.GetBool(PlayModeGuardKey, false))
                return;

            bool originalEnabled = SessionState.GetBool(PlayModeOriginalEnabledKey, false);
            int originalOptions = SessionState.GetInt(PlayModeOriginalOptionsKey, 0);

            EditorSettings.enterPlayModeOptionsEnabled = originalEnabled;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)originalOptions;

            SessionState.SetBool(PlayModeGuardKey, false);

            Debug.Log("[Automation TestRunner] Restored original EnterPlayModeOptions");
        }

        // ─── Helpers ─────────────────────────────────────────────────

        private static string[] ParseStringArray(Dictionary<string, object> args, string key)
        {
            if (!args.ContainsKey(key)) return null;

            var value = args[key];
            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str)) return null;
                return str.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            }

            if (value is List<object> list)
                return list.Select(o => o.ToString()).Where(s => s.Length > 0).ToArray();

            return null;
        }

        // ─── Data Types ──────────────────────────────────────────────

        internal enum TestJobStatus
        {
            Running,
            Canceling,
            Canceled,
            Succeeded,
            Failed
        }

        internal class TestJob
        {
            public string JobId;
            public string AgentId;
            public string RunGuid;
            public bool CancelRequested;
            public TestMode Mode;
            public TestJobStatus Status;
            public DateTime StartedAt;
            public DateTime? CompletedAt;
            public DateTime LastUpdatedAt;
            public string Error;
            public string ErrorCode;

            // Filters used
            public string[] TestNames;
            public string[] Categories;
            public string[] Assemblies;
            public bool HasExplicitFilters;

            // Progress
            public int TotalTests;
            public int CompletedTests;
            public int PassedCount;
            public int FailedCount;
            public int SkippedCount;
            public double TotalDuration;
            public bool CompletionRecovered;

            // Current test being executed
            public string CurrentTestName;
            public DateTime? CurrentTestStartedAt;

            // Results
            public List<TestResult> AllResults = new List<TestResult>();
            public List<TestResult> FailuresSoFar = new List<TestResult>();
        }

        private static bool IsActive(TestJobStatus status)
        {
            return status == TestJobStatus.Running || status == TestJobStatus.Canceling;
        }

        internal sealed class TestResult
        {
            internal readonly string FullName;
            internal readonly string Name;
            internal readonly string Status;
            internal readonly double Duration;
            internal readonly string Message;
            internal readonly string StackTrace;

            internal TestResult(string fullName, string name, string status, double duration,
                string message, string stackTrace)
            {
                FullName = fullName;
                Name = name;
                Status = status;
                Duration = duration;
                Message = message;
                StackTrace = stackTrace;
            }

            internal bool IsFailure => Status == "Failed" || Status == "Inconclusive";
        }
    }
}
