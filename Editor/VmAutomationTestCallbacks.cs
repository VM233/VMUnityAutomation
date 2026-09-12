using System.Collections.Generic;
using UnityEditor.TestTools.TestRunner.Api;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Test Runner API callbacks that forward events to VmAutomationTestRunnerCommands.
    /// </summary>
    internal class VmAutomationTestCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
            int leafCount = CountLeafTests(testsToRun);
            VmAutomationTestRunnerCommands.OnRunStarted(leafCount);
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            int passed = 0, failed = 0, skipped = 0, inconclusive = 0;
            double totalDuration = result.Duration;
            var leafResults = new List<VmAutomationTestRunnerCommands.TestResult>();

            CountResults(result, ref passed, ref failed, ref skipped, ref inconclusive, leafResults);

            VmAutomationTestRunnerCommands.OnRunFinished(passed, failed, skipped, inconclusive, totalDuration,
                leafResults);
        }

        public void TestStarted(ITestAdaptor test)
        {
            if (!test.HasChildren)
            {
                VmAutomationTestRunnerCommands.OnTestStarted(test.FullName);
            }
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.Test.HasChildren)
            {
                VmAutomationTestRunnerCommands.OnTestFinished(
                    result.Test.FullName,
                    result.Test.Name,
                    result.TestStatus,
                    result.Duration,
                    result.Message,
                    result.StackTrace
                );
            }
        }

        private static int CountLeafTests(ITestAdaptor test)
        {
            if (!test.HasChildren) return 1;
            int count = 0;
            foreach (var child in test.Children)
                count += CountLeafTests(child);
            return count;
        }

        private static void CountResults(ITestResultAdaptor result,
            ref int passed, ref int failed, ref int skipped, ref int inconclusive,
            ICollection<VmAutomationTestRunnerCommands.TestResult> leafResults)
        {
            if (!result.Test.HasChildren)
            {
                leafResults.Add(new VmAutomationTestRunnerCommands.TestResult(
                    result.Test.FullName, result.Test.Name, result.TestStatus.ToString(),
                    result.Duration, result.Message, result.StackTrace));
                switch (result.TestStatus)
                {
                    case TestStatus.Passed:
                        passed++;
                        break;
                    case TestStatus.Failed:
                        failed++;
                        break;
                    case TestStatus.Skipped:
                        skipped++;
                        break;
                    case TestStatus.Inconclusive:
                        inconclusive++;
                        break;
                }
            }
            else if (result.Children != null)
            {
                foreach (var child in result.Children)
                    CountResults(child, ref passed, ref failed, ref skipped, ref inconclusive, leafResults);
            }
        }
    }
}
