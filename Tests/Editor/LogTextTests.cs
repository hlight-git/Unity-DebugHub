using NUnit.Framework;

namespace Hlight.Debug.Hub.Tests
{
    public class LogTextTests
    {
        private static int Occurrences(string text, string part)
        {
            var n = 0;
            for (var i = text.IndexOf(part, System.StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + 1, System.StringComparison.Ordinal)) n++;
            return n;
        }

        /// Log chứa đúng chuỗi đóng noparse không được thoát ra để TMP đọc phần sau thành tag.
        [Test]
        public void Escape_CannotBeClosedFromInside()
        {
            var escaped = LogText.Escape("a</noparse><b>x");
            Assert.AreEqual(1, Occurrences(escaped, "</noparse>"));
            StringAssert.EndsWith("</noparse>", escaped);
        }

        /// TMP đóng tag theo tên viết hoa và dừng ở `>`, `=` hoặc khoảng trắng: mọi biến thể đó đều phải bị phá.
        [Test]
        public void Escape_CannotBeClosedByCaseOrSpacingVariants()
        {
            foreach (var input in new[] { "a</NoParse><b>x", "a</noparse ><b>x", "a</noparse=x><b>x" })
            {
                var escaped = LogText.Escape(input);
                Assert.AreEqual(1, Occurrences(escaped, "</"), input);
                StringAssert.EndsWith("</noparse>", escaped);
            }
        }

        [Test]
        public void Highlight_WrapsEveryMatch_IgnoringCase()
        {
            Assert.AreEqual(2, Occurrences(LogText.Highlight("Load level LEVEL", "level"), "<mark="));
            Assert.AreEqual(LogText.Escape("abc"), LogText.Highlight("abc", ""));
        }

        /// Giống Unity console: b/i/color vẽ ra, size/material/quad bỏ (hàng cao cố định), `<...>` khác là chữ.
        [Test]
        public void Parse_KeepsConsoleTags_DropsSizing_LeavesTheRestAsText()
        {
            var rich = LogText.Parse("<color=#00FF00><b>[Ads]</b></color> ok <size=40>big</size> List<int> <link=x>y</link>");
            Assert.AreEqual("[Ads] ok big List<int> <link=x>y</link>", rich.Plain);

            var rendered = LogText.Render(rich, null, LogText.ROW_CHARS);
            StringAssert.Contains("<color=#00FF00FF><b><noparse>[Ads]</noparse></b></color>", rendered);
            StringAssert.DoesNotContain("<size", rendered);
            StringAssert.Contains("<noparse> ok big List<int> <link=x>y<​/link></noparse>", rendered);
        }

        /// TMP không biết tên màu kiểu Unity (lime, aqua…): đổi ra mã. Màu sai thì để nguyên là chữ, như console.
        [Test]
        public void Parse_NormalizesColorNames_KeepsInvalidColorsAsText()
        {
            StringAssert.Contains("<color=#00FFFFFF>", LogText.Render(LogText.Parse("<color=aqua>x</color>"), null, 100));
            StringAssert.StartsWith("<color=nope>", LogText.Parse("<color=nope>x</color>").Plain);
        }

        [Test]
        public void Parse_WithoutTags_KeepsTheSameString()
        {
            const string text = "a < b, List<int>";
            Assert.AreSame(text, LogText.Parse(text).Plain);
        }

        /// Tìm trên chữ nhìn thấy: gõ tên tag không tô gì, đoạn khớp vắt qua tag vẫn tô.
        [Test]
        public void Render_HighlightsVisibleTextOnly()
        {
            Assert.AreEqual(1, Occurrences(LogText.Render(LogText.Parse("<color=#FF0000>color</color> red"), "color", 100), "<mark="));
            Assert.AreEqual(0, Occurrences(LogText.Render(LogText.Parse("<b>x</b>"), "b", 100), "<mark="));
            Assert.AreEqual(1, Occurrences(LogText.Render(LogText.Parse("<b>ab</b>cd"), "bc", 100), "<mark="));
        }

        [Test]
        public void Render_CutsOnVisibleCharacters()
        {
            var rendered = LogText.Render(LogText.Parse("<b>" + new string('x', 1000) + "</b>"), null, LogText.ROW_CHARS);
            Assert.AreEqual(LogText.ROW_CHARS, Occurrences(rendered, "x"));
            Assert.AreEqual(LogText.ROW_CHARS, Occurrences(LogText.Render(LogText.Parse(new string('x', 1000)), null, LogText.ROW_CHARS), "x"));
        }

        [Test]
        public void Count_GroupsThousandsWithDots()
        {
            Assert.AreEqual("999", LogText.Count(999));
            Assert.AreEqual("1.204", LogText.Count(1204));
            Assert.AreEqual("1.234.567", LogText.Count(1234567));
        }

        [Test]
        public void Badge_CapsAt99()
        {
            Assert.AreEqual("3", LogText.Badge(3));
            Assert.AreEqual("99+", LogText.Badge(120));
        }
    }
}
