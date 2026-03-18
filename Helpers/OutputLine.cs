namespace WinOptimizerHub.Helpers
{
    public enum LineType { Info, Step, Success, Warning, Error }

    public class OutputLine
    {
        public string Text { get; set; } = string.Empty;
        public LineType Type { get; set; } = LineType.Info;
        public bool IsPercent { get; set; }

        public string Color => Type switch
        {
            LineType.Success => "#22C55E",
            LineType.Step => "#3B82F6",
            LineType.Warning => "#F59E0B",
            LineType.Error => "#EF4444",
            _ => "#8B949E"
        };

        public OutputLine() { }

        public OutputLine(string text, LineType type = LineType.Info)
        {
            Text = text;
            Type = type;
            IsPercent = false;
        }

        public OutputLine(string text, bool isPercent)
        {
            Text = text;
            Type = LineType.Info;
            IsPercent = isPercent;
        }
    }
}