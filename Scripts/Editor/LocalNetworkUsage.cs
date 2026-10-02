#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace Hlight.Debug.Hub.Editor
{
    /// Dấu hiệu kiểu trang gọi một IP trong LAN → iOS hỏi quyền mạng cục bộ (chỉ tester: request chỉ gửi
    /// sau cử chỉ bí mật). Thiếu khoá này thì hộp thoại không có lời giải thích.
    public class LocalNetworkUsage : IPostprocessBuildWithReport
    {
        private const string KEY = "NSLocalNetworkUsageDescription";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var path = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            // Game đã tự khai thì giữ lời của game.
            if (plist.root.values.ContainsKey(KEY)) return;
            // Lời trung tính: chuỗi này nằm trong mọi build App Store, đừng để nó kể là có công cụ ẩn.
            plist.root.SetString(KEY, "Kết nối tới máy chủ trong mạng nội bộ.");
            plist.WriteToFile(path);
        }
    }
}
#endif
