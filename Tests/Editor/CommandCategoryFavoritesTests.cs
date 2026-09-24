using NUnit.Framework;
using UnityEngine;

namespace Hlight.Debug.Hub.Tests
{
    public class CommandCategoryFavoritesTests
    {
        private const string Key = "DebugHub.CommandCategoryFavorites";
        private string backup;

        [SetUp]
        public void SetUp()
        {
            backup = PlayerPrefs.GetString(Key, string.Empty);
            PlayerPrefs.DeleteKey(Key);
        }

        [TearDown]
        public void TearDown() => PlayerPrefs.SetString(Key, backup);

        [Test]
        public void Toggle_PersistsRootCategoryOrder_AndRemovesAnExistingCategory()
        {
            CommandCategoryFavorites.Toggle("console");
            CommandCategoryFavorites.Toggle("level");
            CollectionAssert.AreEqual(new[] { "console", "level" }, CommandCategoryFavorites.All);

            CommandCategoryFavorites.Toggle("console");
            CollectionAssert.AreEqual(new[] { "level" }, CommandCategoryFavorites.All);
        }
    }
}
