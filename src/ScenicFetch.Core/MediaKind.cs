using System.Text.Json.Serialization;

namespace ScenicFetch.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MediaKind
{
    Image,
    Video,
}
