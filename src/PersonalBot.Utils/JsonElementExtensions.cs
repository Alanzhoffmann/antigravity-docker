using System.Text.Json;

namespace PersonalBot.Utils;

public static class JsonElementExtensions
{
    public static string? GetStringSafe(this JsonElement element, string propertyName)
    {
        if (
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var prop)
        )
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        return null;
    }

    public static string? GetNestedStringSafe(this JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (
                current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(name, out current)
            )
                return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}
