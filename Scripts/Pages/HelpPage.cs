using System;
using System.Collections.Generic;
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

            // Toàn văn description ở đây: row trong list cắt ngắn cho vừa hai dòng.
            builder.Append("\n\n<b>Available commands:</b>");
            var commands = new List<DebugCommand>(DebugCommands.All);
            commands.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));

            foreach (var command in commands)
            {
                builder.Append("\n  - ").Append(command.Path);
                foreach (var parameter in command.Parameters)
                {
                    builder.Append(" [").Append(DebugLogConsole.GetTypeReadableName(parameter.Type)).Append(' ')
                        .Append(parameter.Name).Append(']');
                }
                if (!string.IsNullOrEmpty(command.Description))
                    builder.Append("  <color=#7A828C>").Append(command.Description).Append("</color>");
            }
            return builder.ToString();
        }
    }
}
