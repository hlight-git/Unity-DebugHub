using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Hlight.Debug.Hub.Editor
{
    public class RenameFolderOnBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static readonly Request[] requests = new Request[]
        {
            new Request()
            {
                original = $"{GetDirectoryContainsIngameDebugConsoleResourceFolder()}/Resources",
                onBuild = $"{GetDirectoryContainsIngameDebugConsoleResourceFolder()}/Resources-NotIncludeInBuild"
            },
            new Request()
            {
                original = "Assets/Proxima/Resources",
                onBuild = "Assets/Proxima/Resources-NotIncludeInBuild"
            },
        };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
#if PRODUCTION
            foreach (var request in requests)
            {
                TryRename(request, false);
            }
#endif
        }
    
        public void OnPostprocessBuild(BuildReport report)
        {
#if PRODUCTION
            RecoverOriginalName();
#endif
        }

        private static string GetDirectoryContainsIngameDebugConsoleResourceFolder()
        {
            const string ASMDEF_GUID = "c37e35cd39f8748428443f5955e7ca23";
            var asmdefPath = AssetDatabase.GUIDToAssetPath(ASMDEF_GUID);
            return Path.GetDirectoryName(asmdefPath);
        }

        [InitializeOnLoadMethod]
        public static void RecoverOriginalName()
        {
            foreach (var request in requests)
            {
                TryRename(request, true);
            }
        }

        private static void TryRename(Request request, bool toOriginal)
        {
            var from = toOriginal ? request.onBuild : request.original;
            var to = toOriginal ? request.original : request.onBuild;
            if (!Directory.Exists(from)) return;
            UnityEngine.Debug.Log(from + " " + to);
            AssetDatabase.MoveAsset(from, to);
            AssetDatabase.Refresh();
        }

        public class Request
        {
            public string original;
            public string onBuild;
        }
    }
}