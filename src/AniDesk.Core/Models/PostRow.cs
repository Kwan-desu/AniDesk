namespace AniDesk.Core.Models;

/// <summary>
/// A row of N posts for the virtualized gallery grid.
/// Null slots are invisible placeholders (partial last row).
/// </summary>
public sealed class PostRow
{
    public IReadOnlyList<MoebooruPost?> Items { get; }

    public PostRow(IEnumerable<MoebooruPost?> items)
    {
        Items = items.ToArray();
    }

    public int Count => Items.Count;
}
