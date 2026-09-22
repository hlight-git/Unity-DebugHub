using System.Collections.Generic;

namespace Hlight.Debug.Hub
{
    /// Utility for formatting table text. Implementation details TBD.
    public static class DebugTable
    {
        /// Format headers and rows into monospace table text.
        public static string Format(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
        {
            // ponytail: stub implementation, sufficient for now
            // Full formatting (column alignment, padding) to come when needed
            return "table";
        }
    }
}
