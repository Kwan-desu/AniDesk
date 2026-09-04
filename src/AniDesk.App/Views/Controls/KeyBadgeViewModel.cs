namespace AniDesk.App.Views.Controls;

public class KeyBadgeViewModel
{
    public string Text { get; set; } = string.Empty;
    public bool IsWinKey { get; set; }
    public bool IsShiftKey { get; set; }
    public bool IsSpecialSymbol => IsWinKey || IsShiftKey;
}
