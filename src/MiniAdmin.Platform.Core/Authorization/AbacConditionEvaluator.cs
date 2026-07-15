using System.Collections;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace MiniAdmin.Platform.Authorization;

public static class AbacConditionEvaluator
{
    public static bool Evaluate(
        string? conditionsJson,
        IReadOnlyDictionary<string, object?> attributes)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson))
        {
            return true;
        }

        using var document = JsonDocument.Parse(conditionsJson);
        return EvaluateNode(document.RootElement, attributes);
    }

    public static void Validate(string? conditionsJson)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson))
        {
            return;
        }

        using var document = JsonDocument.Parse(conditionsJson);
        ValidateNode(document.RootElement);
    }

    private static bool EvaluateNode(
        JsonElement node,
        IReadOnlyDictionary<string, object?> attributes)
    {
        EnsureObject(node);
        if (node.TryGetProperty("all", out var all))
        {
            EnsureArray(all, "all");
            return all.EnumerateArray().All(child => EvaluateNode(child, attributes));
        }

        if (node.TryGetProperty("any", out var any))
        {
            EnsureArray(any, "any");
            return any.EnumerateArray().Any(child => EvaluateNode(child, attributes));
        }

        if (node.TryGetProperty("not", out var not))
        {
            return !EvaluateNode(not, attributes);
        }

        var attributeName = GetRequiredString(node, "attribute");
        var operation = GetRequiredString(node, "operator");
        attributes.TryGetValue(attributeName, out var actual);
        node.TryGetProperty("value", out var expectedElement);
        var expected = ResolveExpected(expectedElement, attributes);

        return operation.ToLowerInvariant() switch
        {
            "exists" => actual is not null,
            "notexists" => actual is null,
            "equals" => Compare(actual, expected) == 0,
            "notequals" => Compare(actual, expected) != 0,
            "contains" => Contains(actual, expected),
            "startswith" => ToText(actual).StartsWith(ToText(expected), StringComparison.OrdinalIgnoreCase),
            "endswith" => ToText(actual).EndsWith(ToText(expected), StringComparison.OrdinalIgnoreCase),
            "in" => ToValues(expected).Any(item => Compare(actual, item) == 0),
            "notin" => ToValues(expected).All(item => Compare(actual, item) != 0),
            "greaterthan" => Compare(actual, expected) > 0,
            "greaterthanorequal" => Compare(actual, expected) >= 0,
            "lessthan" => Compare(actual, expected) < 0,
            "lessthanorequal" => Compare(actual, expected) <= 0,
            "ipincidr" => IsIpInCidr(ToText(actual), ToText(expected)),
            _ => throw new InvalidOperationException($"Unsupported ABAC operator '{operation}'.")
        };
    }

    private static void ValidateNode(JsonElement node)
    {
        EnsureObject(node);
        if (node.TryGetProperty("all", out var all))
        {
            EnsureArray(all, "all");
            foreach (var child in all.EnumerateArray())
            {
                ValidateNode(child);
            }

            return;
        }

        if (node.TryGetProperty("any", out var any))
        {
            EnsureArray(any, "any");
            foreach (var child in any.EnumerateArray())
            {
                ValidateNode(child);
            }

            return;
        }

        if (node.TryGetProperty("not", out var not))
        {
            ValidateNode(not);
            return;
        }

        _ = GetRequiredString(node, "attribute");
        var operation = GetRequiredString(node, "operator").ToLowerInvariant();
        var supported = operation is
            "exists" or "notexists" or "equals" or "notequals" or "contains" or
            "startswith" or "endswith" or "in" or "notin" or "greaterthan" or
            "greaterthanorequal" or "lessthan" or "lessthanorequal" or "ipincidr";
        if (!supported)
        {
            throw new InvalidOperationException($"Unsupported ABAC operator '{operation}'.");
        }

        if (operation is not ("exists" or "notexists") && !node.TryGetProperty("value", out _))
        {
            throw new InvalidOperationException("ABAC comparison condition requires a value.");
        }
    }

    private static object? ResolveExpected(
        JsonElement element,
        IReadOnlyDictionary<string, object?> attributes)
    {
        if (element.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (text?.StartsWith('$') == true)
            {
                return attributes.GetValueOrDefault(text[1..]);
            }

            return text;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(item => ResolveExpected(item, attributes))
                .ToArray();
        }

        return element.ValueKind switch
        {
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            _ => element.GetRawText()
        };
    }

    private static bool Contains(object? actual, object? expected)
    {
        if (actual is IEnumerable enumerable and not string)
        {
            return enumerable.Cast<object?>().Any(item => Compare(item, expected) == 0);
        }

        return ToText(actual).Contains(ToText(expected), StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null ? 0 : left is null ? -1 : 1;
        }

        var leftText = ToText(left);
        var rightText = ToText(right);
        if (decimal.TryParse(leftText, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftNumber) &&
            decimal.TryParse(rightText, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (DateTimeOffset.TryParse(leftText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var leftDate) &&
            DateTimeOffset.TryParse(rightText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<object?> ToValues(object? value)
    {
        if (value is IEnumerable enumerable and not string)
        {
            return enumerable.Cast<object?>().ToArray();
        }

        return [value];
    }

    private static string ToText(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool IsIpInCidr(string ipText, string cidrText)
    {
        var parts = cidrText.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(ipText, out var ip) ||
            !IPAddress.TryParse(parts[0], out var network) ||
            !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        var ipBytes = ip.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (ipBytes.Length != networkBytes.Length || prefixLength < 0 || prefixLength > ipBytes.Length * 8)
        {
            return false;
        }

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        for (var index = 0; index < fullBytes; index++)
        {
            if (ipBytes[index] != networkBytes[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (ipBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }

    private static string GetRequiredString(JsonElement node, string propertyName)
    {
        if (!node.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException($"ABAC condition requires '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static void EnsureObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("ABAC condition node must be an object.");
        }
    }

    private static void EnsureArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"ABAC '{propertyName}' must be an array.");
        }
    }
}
