using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    /// Chạy EditMode test của package rồi ghi kết quả ra file.
    /// Tồn tại vì agent/MCP không gọi được TestRunnerApi trực tiếp — chỉ gọi được menu item.
    /// Người thì cứ dùng cửa sổ Test Runner như bình thường.
    public static class AgentTestRunner
    {
        private const string RESULT_PATH = "Temp/debug-hub-tests.txt";
        private const string ASSEMBLY = "Hlight.Debug.Hub.Tests";

        // Giữ static: TestRunnerApi là ScriptableObject, để nó rơi ra khỏi scope thì
        // callback bị GC và RunFinished không bao giờ được gọi.
        private static TestRunnerApi api;

        [MenuItem("Tools/Hlight/Run Debug Hub Tests")]
        public static void Run()
        {
            if (File.Exists(RESULT_PATH)) File.Delete(RESULT_PATH);

            if (api == null) api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultWriter());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { ASSEMBLY }
            }));
        }

        private class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                File.WriteAllText(RESULT_PATH + ".started", testsToRun.TestCaseCount.ToString());
            }

            public void RunFinished(ITestResultAdaptor testResults)
            {
                var builder = new StringBuilder();
                builder.Append("PASS=").Append(testResults.PassCount)
                       .Append(" FAIL=").Append(testResults.FailCount)
                       .Append(" SKIP=").Append(testResults.SkipCount).Append('\n');
                Collect(testResults, builder);
                File.WriteAllText(RESULT_PATH, builder.ToString());
            }

            private static void Collect(ITestResultAdaptor node, StringBuilder builder)
            {
                if (!node.HasChildren)
                {
                    builder.Append(node.TestStatus).Append(' ').Append(node.FullName).Append('\n');
                    if (node.TestStatus == TestStatus.Failed)
                    {
                        builder.Append("  ").Append(node.Message).Append('\n');
                        builder.Append("  ").Append(node.StackTrace).Append('\n');
                    }
                }

                if (node.Children == null) return;
                foreach (var child in node.Children) Collect(child, builder);
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
