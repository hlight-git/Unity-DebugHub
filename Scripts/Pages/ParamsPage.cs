using System;

namespace Hlight.Debug.Hub
{
    /// Page nhập liệu cho ActionNode nhiều tham số.
    ///
    /// Giá trị nằm ở DebugRegistry.ArgsByKey chứ không phải trong closure: page được dựng lại mỗi
    /// lần điều hướng (kể cả khi back từ page chọn enum) nên để trong closure là mất cái vừa nhập,
    /// và giữ ở registry thì lần sau vào không phải gõ lại.
    public static class ParamsPage
    {
        public static DebugPage For(ActionNode node, Action<DebugNode, string[]> run)
        {
            return new DebugPage(node.Label, panel =>
            {
                var values = DebugRegistry.ArgsFor(node);

                if (!string.IsNullOrEmpty(node.Description)) panel.AddText(node.Description);

                for (var i = 0; i < node.Parameters.Length; i++)
                {
                    var index = i;
                    var parameter = node.Parameters[index];
                    panel.AddField(parameter.Name, parameter.Type, values[index], value =>
                    {
                        values[index] = value;
                        DebugRegistry.StoreArgs(node, values);
                    });
                }

                if (node.Awaitable)
                {
                    panel.AddToggle("Chờ kết quả", DebugRegistry.AwaitEnabled(node),
                        on => DebugRegistry.SetAwait(node, on),
                        "Tắt thì chỉ gọi rồi thôi, in ra chính object Task/UniTask.");
                }

                panel.AddPrimary("Run", () => run(node, values));
            }, searchable: false);
        }
    }
}
