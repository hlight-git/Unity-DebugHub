using System.Text;
using IngameDebugConsole;

namespace Hlight.Debug.Hub
{
    public static class HelpPage
    {
        public static DebugPage Build() => new DebugPage("Help", panel => panel.AddText(BuildText()));

        private static string BuildText()
        {
            var builder = new StringBuilder();
            builder.Append("<b>Dev note:</b>");
            foreach (var note in DebugHub.Notes)
            {
                builder.Append("\n  + ").Append(note);
            }

            builder.Append("\n\n<b>Available commands:</b>");
            foreach (var command in DebugLogConsole.GetAllCommands())
            {
                if (command.IsValid()) builder.Append("\n  - ").Append(command.signature);
            }
            return builder.ToString();
        }
    }
}
