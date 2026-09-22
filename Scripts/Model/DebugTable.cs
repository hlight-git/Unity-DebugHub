using System;
using System.Collections.Generic;
using System.Text;

namespace Hlight.Debug.Hub
{
    /// Bảng cho trang info. Căn cột bằng `<mspace>` + PadRight, **không** bằng `<pos=NN%>`:
    /// `<pos>` lùi được về sau nên ô dài tràn sang là đè chữ lên nhau, còn monospace thì
    /// đếm ký tự là cắt được chính xác.
    ///
    /// Hàm thuần — test bằng assert chuỗi, không cần dựng UI.
    public static class DebugTable
    {
        public const string MONO = "<mspace=0.55em>";
        private const int GAP = 2;

        /// <paramref name="budget"/> là số ký tự tối đa một dòng, tính cả khoảng cách cột.
        /// Quá thì cột rộng nhất bị cắt kèm '…'.
        public static string Format(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, int budget = 34)
        {
            var columns = headers?.Count ?? 0;
            foreach (var row in rows) columns = Math.Max(columns, row.Length);
            if (columns == 0) return string.Empty;

            var widths = new int[columns];
            for (var i = 0; i < columns; i++) widths[i] = Cell(headers, i).Length;
            foreach (var row in rows)
            {
                for (var i = 0; i < columns; i++) widths[i] = Math.Max(widths[i], Cell(row, i).Length);
            }

            Shrink(widths, budget);

            var builder = new StringBuilder(MONO);
            if (headers != null) Append(builder, headers, widths, columns);
            foreach (var row in rows)
            {
                if (builder.Length > MONO.Length) builder.Append('\n');
                Append(builder, row, widths, columns);
            }
            return builder.ToString();
        }

        /// Cắt dần cột rộng nhất cho tới khi cả dòng vừa ngân sách. Cắt cột rộng nhất chứ không
        /// cắt đều: một cột 40 ký tự cạnh một cột 2 ký tự thì cột 2 không có gì để nhường.
        private static void Shrink(int[] widths, int budget)
        {
            while (Total(widths) > budget)
            {
                var widest = 0;
                for (var i = 1; i < widths.Length; i++)
                {
                    if (widths[i] > widths[widest]) widest = i;
                }
                if (widths[widest] <= 1) return;
                widths[widest]--;
            }
        }

        private static int Total(int[] widths)
        {
            var total = 0;
            foreach (var width in widths) total += width + GAP;
            return total - GAP;
        }

        private static void Append(StringBuilder builder, IReadOnlyList<string> row, int[] widths, int columns)
        {
            for (var i = 0; i < columns; i++)
            {
                if (i > 0) builder.Append(' ', GAP);
                var cell = Cell(row, i);
                if (cell.Length > widths[i]) cell = cell.Substring(0, Math.Max(1, widths[i] - 1)) + "…";
                builder.Append(i == columns - 1 ? cell : cell.PadRight(widths[i]));
            }
        }

        private static string Cell(IReadOnlyList<string> row, int index)
        {
            return row != null && index < row.Count ? row[index] ?? string.Empty : string.Empty;
        }
    }
}
