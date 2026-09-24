using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Hlight.Debug.Hub.Editor
{
    /// Folder Resources luôn bị đưa vào build, nên cách duy nhất để loại nó ra là đổi tên trước khi
    /// build rồi đổi lại sau. Chỉ làm khi build có DISABLE_DEBUG_HUB.
    public class RenameFolderOnBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// Dựng danh sách lúc cần thay vì trong static initializer: static init có thể chạy trước khi
        /// AssetDatabase sẵn sàng, lúc đó GUIDToAssetPath trả "" và việc đổi tên im lặng không xảy ra.
        private static IEnumerable<Request> GetRequests()
        {
            var packageDirectory = GetPackageDirectory();
            if (!string.IsNullOrEmpty(packageDirectory))
            {
                yield return new Request
                {
                    original = $"{packageDirectory}/Resources",
                    onBuild = $"{packageDirectory}/Resources-NotIncludeInBuild"
                };
            }

            yield return new Request
            {
                original = "Assets/Proxima/Resources",
                onBuild = "Assets/Proxima/Resources-NotIncludeInBuild"
            };
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
#if DISABLE_DEBUG_HUB
            foreach (var request in GetRequests())
            {
                TryRename(request, false);
            }
#endif
        }

        public void OnPostprocessBuild(BuildReport report)
        {
#if DISABLE_DEBUG_HUB
            RecoverOriginalName();
#endif
        }

        /// Tìm folder của package qua GUID của asmdef, không hardcode đường dẫn.
        private static string GetPackageDirectory()
        {
            const string ASMDEF_GUID = "c37e35cd39f8748428443f5955e7ca23";
            var asmdefPath = AssetDatabase.GUIDToAssetPath(ASMDEF_GUID);
            if (string.IsNullOrEmpty(asmdefPath))
            {
                UnityEngine.Debug.LogWarning($"[{nameof(RenameFolderOnBuild)}] Could not resolve the package folder from the asmdef GUID; Resources folders were left as they are.");
                return null;
            }
            return Path.GetDirectoryName(asmdefPath);
        }

        /// Build lỗi giữa đường thì folder vẫn đang mang tên tạm, nên phục hồi mỗi lần load editor.
        [InitializeOnLoadMethod]
        public static void RecoverOriginalName()
        {
            foreach (var request in GetRequests())
            {
                TryRename(request, true);
            }
        }

        private static void TryRename(Request request, bool toOriginal)
        {
            var from = toOriginal ? request.onBuild : request.original;
            var to = toOriginal ? request.original : request.onBuild;
            if (!Directory.Exists(from)) return;

            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError($"[{nameof(RenameFolderOnBuild)}] {from} -> {to}: {error}");
                return;
            }
            AssetDatabase.Refresh();
        }

        public class Request
        {
            public string original;
            public string onBuild;
        }
    }
}
