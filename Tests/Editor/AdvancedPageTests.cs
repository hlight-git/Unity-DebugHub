using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hlight.Debug.Hub.Tests
{
    public class AdvancedPageTests
    {
        private const string ROOT = "Hlight.Debug.Hub.Tests.AddressFixture.Instance";
        private DebugHubPanel panel;
        private string watchBackup;

        [SetUp]
        public void SetUp()
        {
            watchBackup = PlayerPrefs.GetString("DebugHub.Watches", string.Empty);
            PlayerPrefs.DeleteKey("DebugHub.Watches");
            AddressFixture.Reset();
            panel = TestPanel.Build();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.SetString("DebugHub.Watches", watchBackup);
            TestPanel.Destroy(panel);
        }

        [Test]
        public void Root_ListsObjectsAndBrowse()
        {
            panel.ShowFromRoot(AdvancedPage.Root());

            var labels = TestPanel.LabelsOf(panel);
            Assert.IsTrue(labels.Exists(l => l.Contains("Objects")));
            Assert.IsTrue(labels.Exists(l => l.Contains("Duyệt")));
            Assert.AreEqual(2, labels.FindAll(l => l.Contains("›") || l.Contains("Objects") || l.Contains("Duyệt")).Count);
        }

        [Test]
        public void ExecutePage_ReadsAnAddress()
        {
            panel.ShowFromRoot(AdvancedPage.ExecutePage());
            TestPanel.Rows(panel)[0].input.onEndEdit.Invoke($"{ROOT}.Number");
            TestPanel.ClickRowContaining(panel, "Get");

            Assert.IsTrue(TestPanel.LabelsOf(panel).Exists(l => l.Contains("1")) ||
                          panel.LastResult.Contains("1"));
        }
    }
}
