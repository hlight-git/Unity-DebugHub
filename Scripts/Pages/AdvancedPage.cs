namespace Hlight.Debug.Hub
{
    /// Mặt thứ hai của hub: **đồ nghề của chính hub**, không phải một category command.
    /// Ranh giới: cái gì đăng ký được thành node thì ở Commands; cái gì để soi/chỉnh bản thân
    /// runtime thì ở đây.
    internal static class AdvancedPage
    {
        public static DebugPage Root()
        {
            return new DebugPage("Advanced", panel =>
            {
                panel.AddNavigation("Objects", ObjectsPage.Root(), "Object và giá trị đã ghim.",
                    (Watches.All.Count + Vars.All.Count).ToString());
                panel.AddNavigation("Duyệt", BrowsePage.Assemblies(), "Tìm type/instance rồi mở ra.");
            });
        }
    }
}
