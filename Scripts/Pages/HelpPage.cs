using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hlight.Debug.Hub
{
    /// Trang Trợ giúp (nút `?` ở Commands gốc): cách dùng hub, ghi chú của game, rồi hai trang tra cứu — cú pháp address
    /// và mọi lệnh. Mỗi mục là một khối chữ: cả trang vài row, không phải một row mỗi dòng (~0,9 ms/row).
    internal static class HelpPage
    {
        /// Khoảng trống thấp giữa hai lệnh trong cùng một khối chữ: một dòng chỉ có dấu cách cỡ nhỏ.
        private const string GAP = "\n<size=35%> </size>\n";

        public static DebugPage Build() => Build(DebugHub.TriggerHints);

        /// <paramref name="openHints"/>: cách gọi lại hub, mỗi trigger đang gắn một dòng (DebugHub.TriggerHints).
        internal static DebugPage Build(IReadOnlyList<string> openHints)
        {
            return new DebugPage("Trợ giúp", panel =>
            {
                if (DebugHub.Notes.Count > 0) Section(panel, "Ghi chú của game", DebugHub.Notes);

                // Hai trang tra lên đầu: thứ hay mở nhất, không bắt cuộn qua cả trang chữ hướng dẫn.
                panel.AddNavigation("Tất cả lệnh", Commands(), "Cùng thứ tự với cây Commands.",
                    DebugRegistry.All.Count.ToString());
                panel.AddNavigation("Cú pháp address", AddressSyntax(), "Gõ ở Objects › Nhập address…; bấm một dòng để copy.");

                Section(panel, "Mở / ẩn hub", openHints.Concat(new[]
                {
                    "Kéo bong bóng vào nút X ở đáy để ẩn; gọi lại bằng các cách trên.",
                    "Bấm nền tối ngoài panel để đóng (mở lại về đúng trang đang xem); ‹ để lùi một trang.",
                }));

                Section(panel, "Đọc một dòng", new[]
                {
                    "› ở cuối: mở sang trang khác.",
                    "Tên màu xanh ngọc: bấm là chạy ngay.",
                    "Công tắc: bật / tắt ngay.",
                    "Ô nhập: gõ rồi Enter hoặc bấm ra ngoài là ghi.",
                    "Nút …: copy giá trị, ghim vào Objects, lưu vào $biến.",
                    "Sao ở nhóm gốc: đưa nhóm lên mục Yêu thích.",
                    "Dòng chữ nổi ở đáy là kết quả lệnh vừa chạy: bấm để mở trang Log tại đúng lần chạy đó.",
                });

                Section(panel, "Header và nút nổi", new[]
                {
                    "Kính lúp: tìm trong trang đang mở; ở Commands gốc là tìm mọi lệnh.",
                    "Log: log của phiên (kèm logcat Android, log hệ thống iOS); chấm đỏ là số lỗi chưa xem.",
                    "Công cụ: Objects (mục đã ghim, biến $) và Duyệt (tìm type / instance rồi mở ra).",
                    "hub.repeat: bật nút nổi chạy lại lệnh cuối.",
                });
            }, searchable: false);
        }

        /// Thụt lề treo: dòng bị xuống hàng thẳng với chữ của mục, không dính lề trái như gạch đầu dòng — mới tách được
        /// từng mục khi đọc lướt.
        private static void Section(DebugHubPanel panel, string title, IEnumerable<string> lines)
        {
            panel.AddText($"<b>{title}</b>");
            panel.AddText("<size=90%>" + string.Join("\n", lines.Select(line => $"–<indent=1.1em>{line}</indent>")) + "</size>");
        }

        /// Bảng tra address (README › Advanced). Bấm một dòng là copy ví dụ để sửa rồi dán vào Objects › Nhập address….
        internal static DebugPage AddressSyntax()
        {
            return new DebugPage("Cú pháp address", panel =>
            {
                panel.AddCopyRow("Lệnh giá trị đã đăng ký", "@economy.coin");
                panel.AddCopyRow("Instance thứ i đang sống, kể cả inactive", "#UnityEngine.Camera[0].fieldOfView");
                panel.AddCopyRow("Member static, tên type đầy đủ", "Namespace.Type.Member");
                panel.AddCopyRow("Biến đã lưu ở … › Lưu vào $…", "$player.health");
                panel.AddCopyRow("Phần tử list, mảng; mảng nhiều chiều cách bằng dấu cách", "$board.Cells[1 2]");
                panel.AddCopyRow("Khoá dictionary có dấu chấm thì để trong nháy", "$save.Map[\"a.b\"]");
                panel.AddCopyRow("Gọi method, có type argument", "$go.GetComponent<UnityEngine.Light>()");
                panel.AddCopyRow("Nhiều overload cùng số tham số: chọn bằng {n}", "Namespace.Type.Pick{1}(5)");
                panel.AddCopyRow("Chữ thật bắt đầu bằng $ trong đối số: viết $$", "Namespace.Type.Say($$5)");
            }, searchable: false);
        }

        /// Mọi lệnh đã đăng ký, mỗi nhóm gốc một khối, cùng thứ tự với cây Commands (CommandsPage.Rows, có priority).
        /// Không gọi getter: Help là chỗ tra, giá trị đã hiện trên chính row của nó — và getter của game có thể có side effect.
        internal static DebugPage Commands()
        {
            return new DebugPage("Tất cả lệnh", panel =>
            {
                var all = DebugRegistry.All;
                if (all.Count == 0)
                {
                    panel.AddText("Chưa có command nào.");
                    return;
                }

                foreach (var row in CommandsPage.Rows(all, Array.Empty<string>()))
                {
                    var entries = new List<DebugRegistry.Entry>();
                    Collect(all, row, Array.Empty<string>(), entries);
                    // Tên nhóm mờ, như tiêu đề nhóm ở Commands gốc: khác hẳn đường dẫn lệnh in đậm bên dưới.
                    if (row.Folder != null) panel.AddText($"<color={Palette.MUTED}><b>{row.Folder}</b></color>");
                    panel.AddText(string.Join(GAP, entries.ConvertAll(Describe)));
                }
            }, searchable: false);
        }

        private static void Collect(IReadOnlyList<DebugRegistry.Entry> all, CommandsPage.Row row, string[] prefix,
            List<DebugRegistry.Entry> into)
        {
            if (row.Entry != null)
            {
                into.Add(row.Entry);
                return;
            }
            var inside = prefix.Append(row.Folder).ToArray();
            foreach (var child in CommandsPage.Rows(all, inside)) Collect(all, child, inside, into);
        }

        private static string Describe(DebugRegistry.Entry entry)
        {
            var line = new StringBuilder("<b>").Append(entry.Path).Append("</b>");
            switch (entry.Node)
            {
                case ActionNode action:
                    foreach (var parameter in action.Parameters)
                        line.Append(" [").Append(DebugValues.ReadableName(parameter.Type)).Append(' ').Append(parameter.Name).Append(']');
                    break;

                case ValueNode value:
                    line.Append(" : ").Append(DebugValues.ReadableName(value.Declared));
                    if (value.Set == null) line.Append(" (chỉ đọc)");
                    break;

                case FolderNode:
                    line.Append(" ›");
                    break;
            }
            if (!string.IsNullOrEmpty(entry.Node.Description))
                line.Append("\n<size=85%><color=").Append(Palette.DIM).Append('>').Append(entry.Node.Description).Append("</color></size>");
            return line.ToString();
        }
    }
}
