using System.Text.Json.Serialization;

namespace AniDesk.Core.Models;

public class MoebooruTag
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonIgnore]
    public string DisplayText => $"{Name} ({Count:N0})";
}
