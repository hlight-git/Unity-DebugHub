using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hlight.Debug.Hub
{
    /// Root command categories in the order the user starred them.
    internal static class CommandCategoryFavorites
    {
        private const string Key = "DebugHub.CommandCategoryFavorites";

        public static IReadOnlyList<string> All
        {
            get
            {
                var result = new List<string>();
                foreach (var category in PlayerPrefs.GetString(Key, string.Empty).Split('\n'))
                    if (!string.IsNullOrWhiteSpace(category) && !result.Contains(category)) result.Add(category);
                return result;
            }
        }

        public static bool Contains(string category) => All.Contains(category);

        public static void Toggle(string category)
        {
            var items = new List<string>(All);
            if (!items.Remove(category)) items.Add(category);
            PlayerPrefs.SetString(Key, string.Join("\n", items));
            PlayerPrefs.Save();
        }
    }
}
