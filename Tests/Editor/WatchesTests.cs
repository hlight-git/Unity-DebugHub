using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class WatchesTests
    {
        private const string KEY = "DebugHub.Watches";
        private string backup;

        [SetUp]
        public void SetUp()
        {
            backup = PlayerPrefs.GetString(KEY, string.Empty);
            PlayerPrefs.DeleteKey(KEY);
        }

        [TearDown] public void TearDown() => PlayerPrefs.SetString(KEY, backup);

        [Test]
        public void Add_RefusesAnAddressRootedInAVariable()
        {
            Vars.Bind("x", 1);
            try
            {
                Assert.IsFalse(Watches.TryAdd("$x.Foo", out var error));
                StringAssert.Contains("biến", error.ToLowerInvariant());
                Assert.AreEqual(0, Watches.All.Count);
            }
            finally { Vars.Remove("x"); }
        }

        [Test]
        public void Load_DropsAStoredVariableAddress()
        {
            // Bản lưu từ trước khi có luật này.
            PlayerPrefs.SetString(KEY, "A.b\n$rootScope.iapControl");

            Assert.AreEqual(1, Watches.All.Count,
                "biến $ chết theo domain reload — ghim nó là bảo đảm một dòng đỏ ở lần chạy sau");
        }

        [Test]
        public void Add_StoresAndSurvivesAReadBack()
        {
            Assert.IsTrue(Watches.TryAdd("Hlight.Debug.Hub.Tests.AddressFixture.Instance.Number", out var error), error);

            Assert.AreEqual(1, Watches.All.Count);
            Assert.IsTrue(Watches.Contains("Hlight.Debug.Hub.Tests.AddressFixture.Instance.Number"));
        }

        [Test]
        public void Add_IsIdempotent()
        {
            Watches.TryAdd("A.b", out _);
            Watches.TryAdd("A.b", out _);

            Assert.AreEqual(1, Watches.All.Count);
        }

        [Test]
        public void Add_RefusesAnAddressThatCallsAMethod()
        {
            Assert.IsFalse(Watches.TryAdd("Hlight.Debug.Hub.Tests.AddressFixture.Instance.Doubled(2)", out var error));
            StringAssert.Contains("method", error.ToLowerInvariant());
            Assert.AreEqual(0, Watches.All.Count);
        }

        [Test]
        public void Load_DropsAStoredAddressThatCallsAMethod()
        {
            // Bản lưu từ lần trước, hoặc do người dùng sửa tay PlayerPrefs.
            PlayerPrefs.SetString(KEY, "A.b\nHlight.Debug.Hub.Tests.AddressFixture.Instance.Doubled(2)");

            Assert.AreEqual(1, Watches.All.Count, "address có method không được sống lại từ PlayerPrefs");
        }

        [Test]
        public void Remove_TakesItOut()
        {
            Watches.TryAdd("A.b", out _);
            Watches.Remove("A.b");

            Assert.AreEqual(0, Watches.All.Count);
        }
    }
}
