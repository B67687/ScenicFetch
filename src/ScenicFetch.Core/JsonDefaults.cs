using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScenicFetch.Core;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Serializer = CreateDefault();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
