using System;
using Object = UnityEngine.Object;

namespace Hlight.Debug.Hub
{
    /// Làm gì với hub sau khi command chạy xong.
    public enum DismissMode
    {
        /// Giữ nguyên panel — cho row bật/tắt liên tiếp.
        Stay,

        /// Đóng panel, entry vẫn còn.
        ClosePanel,

        /// Ẩn cả entry để nhìn game không bị hub che. Gọi lại bằng trigger (lắc/gõ góc); panel mở lại
        /// đúng page đang xem vì DebugHubPanel.Close() giữ stack.
        HideHub,
    }

    /// Một tham số của command.
    ///
    /// <see cref="Current"/> là thứ quyết định hình thái row: đọc được giá trị hiện tại thì row hiện
    /// luôn state (switch / field inline), không thì phải mở page nhập liệu mới biết nhập gì.
    public sealed class DebugParameter
    {
        public readonly string Name;
        public readonly Type Type;
        public readonly Func<object> Current;

        public DebugParameter(string name, Type type, Func<object> current = null)
        {
            Name = name;
            Type = type;
            Current = current;
        }
    }

    /// Một command trong storage của hub. Không đi qua registry của IngameDebugConsole nữa: hub cần
    /// giữ thêm description, hình thái, state hiện tại, owner — những thứ ConsoleMethodInfo không có.
    public sealed class DebugCommand
    {
        /// "prefs.set.int" — dùng để hiển thị và tách cây page, **không phải key**: hai command trùng
        /// path là hợp lệ (overload), muốn bỏ thì giữ lại object này rồi gọi DebugCommands.Remove.
        public readonly string Path;

        /// Path đã tách theo dấu '.', cache sẵn vì page được dựng lại mỗi lần điều hướng.
        public readonly string[] Segments;

        public readonly string Description;
        public readonly DebugParameter[] Parameters;

        /// null với command dạng page.
        public readonly Action<object[]> Run;

        /// Command dạng page: bấm row là mở page do chính nó dựng (danh sách động, page tự vẽ...).
        /// Đây là cách duy nhất để cắm một nhánh riêng vào cây mà không phải sửa package.
        public readonly Action<DebugHubPanel> Page;

        /// Owner bị Destroy thì command tự rụng khỏi storage. null = sống suốt phiên.
        public readonly Object Owner;

        /// Phân biệt "không có owner" với "owner đã bị destroy": cả hai đều làm Owner == null.
        private readonly bool owned;

        /// Mặc định là segment cuối của Path.
        public string Label;

        public DismissMode Dismiss;

        /// Bấm thì hỏi lại một page "chắc chưa" trước khi chạy. Cho loại xoá save / reset profile.
        public bool Confirm;

        /// Giá trị đang có trên page nhập liệu. Set sẵn lúc đăng ký = đặt default (thay cho việc
        /// đăng ký thêm overload chỉ để điền một tham số), và giữ lại giá trị vừa nhập cho lần sau.
        public string[] Args;

        internal DebugCommand(Object owner, string path, string description, DebugParameter[] parameters,
            Action<object[]> run, Action<DebugHubPanel> page = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Command path rỗng.", nameof(path));

            Owner = owner;
            owned = owner;
            Path = path;
            Segments = path.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            Description = description;
            Parameters = parameters ?? Array.Empty<DebugParameter>();
            Run = run;
            Page = page;
            Label = Segments.Length > 0 ? Segments[Segments.Length - 1] : path;

            // Chạy cheat xong gần như luôn là để nhìn game, nên đóng panel là default đúng hơn; riêng
            // row bật/tắt tại chỗ thì phải ở lại, không ai bật một switch rồi muốn panel biến mất.
            Dismiss = IsInline ? DismissMode.Stay : DismissMode.ClosePanel;
        }

        /// Có hiện dòng kết quả sau khi chạy hay không.
        ///
        /// Mặc định suy từ <see cref="Dismiss"/>, vì đó là cùng một câu chuyện: panel ở lại = đang đọc
        /// dữ liệu nên cần thấy output; panel đóng hoặc ẩn hub = ra nhìn game nên đừng che màn hình.
        /// Không thể chỉ dựa vào "command có in log hay không": `level.goto` tự nó không log nhưng
        /// luồng load level log đồng bộ trong lúc invoke, và log đó bị bắt vào kết quả.
        ///
        /// Lỗi thì luôn hiện, bất kể cờ này.
        public bool ShowsResult => showsResult ?? Dismiss == DismissMode.Stay;

        private bool? showsResult;

        /// Luôn hiện kết quả, kể cả khi command đóng panel sau khi chạy.
        public DebugCommand Reports()
        {
            showsResult = true;
            return this;
        }

        /// Không bao giờ hiện kết quả, kể cả command in log.
        public DebugCommand Silent()
        {
            showsResult = false;
            return this;
        }

        /// Command đọc/ghi dữ liệu: ở lại page để tra tiếp, kết quả đã hiện ở dòng kết quả.
        public DebugCommand Stays()
        {
            Dismiss = DismissMode.Stay;
            return this;
        }

        /// Command mà muốn xem kết quả thì hub phải biến mất (ẩn UI, mở debugger của SDK, chụp ảnh).
        public DebugCommand HidesHub()
        {
            Dismiss = DismissMode.HideHub;
            return this;
        }

        public DebugCommand Confirms()
        {
            Confirm = true;
            return this;
        }

        /// Giá trị mặc định của page nhập liệu — thay cho việc đăng ký thêm overload chỉ để điền
        /// sẵn một tham số.
        public DebugCommand Defaults(params string[] args)
        {
            Args = args;
            return this;
        }

        /// Owner còn sống (hoặc không có owner).
        public bool Alive => !owned || Owner;

        /// Command của chính package (prefs, time, inspect, sdk...) — nhận ra qua owner nằm trong
        /// assembly của hub. Chúng bị dồn vào một menu riêng ở cuối page Commands: dev mở hub ra là
        /// để tìm cheat của game, không phải để xem đồ có sẵn.
        public bool IsBuiltIn => Owner && Owner.GetType().Assembly == typeof(DebugCommand).Assembly;

        /// Bấm là chạy ngay, không có gì phải nhập.
        public bool IsInstant => Page == null && Parameters.Length == 0;

        /// Row hiện state ngay tại chỗ (switch / page chọn / field) thay vì mở page nhập liệu.
        public bool IsInline => Page == null && Parameters.Length == 1 && Parameters[0].Current != null;
    }
}
