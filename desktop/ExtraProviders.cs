using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GPTBus;

internal static class ExtraProviders
{
    // CC Switch keeps these providers alongside existing entries; the user
    // selects the model in the client. Keep unrelated JSONC comments verbatim.
    public static string Add(string client, string existing, string key, string model)
    {
        var bytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(existing) ? "{}\n" : existing);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        bool legacy = client == "opencode" && !document.RootElement.TryGetProperty("providers", out _);
        var group = legacy ? "provider" : "providers";
        var url = ProviderConfig.ApiOrigin + "/v1";
        var models = new JsonObject { [model] = new JsonObject { ["name"] = model } };
        var provider = client switch
        {
            "pi" => new JsonObject
            {
                ["baseUrl"] = url, ["apiKey"] = key, ["api"] = "openai-responses",
                ["models"] = new JsonArray(new JsonObject { ["id"] = model, ["name"] = model })
            },
            "opencode" when legacy => new JsonObject
            {
                ["name"] = "GPTBus", ["npm"] = "@ai-sdk/openai",
                ["options"] = new JsonObject { ["baseURL"] = url, ["apiKey"] = key }, ["models"] = models
            },
            "opencode" => new JsonObject
            {
                ["name"] = "GPTBus", ["package"] = "@opencode/ai/providers/openai-compatible",
                ["settings"] = new JsonObject { ["baseURL"] = url, ["apiKey"] = key }, ["models"] = models
            },
            _ => throw new ArgumentException("Unknown provider format")
        };
        if (client == "opencode" && !document.RootElement.TryGetProperty("$schema", out _))
            bytes = SetMember(bytes, new[] { "$schema" }, JsonValue.Create("https://opencode.ai/config.json")!);
        var result = SetMember(bytes, new[] { group, "gptbus_connect" }, provider);
        return Encoding.UTF8.GetString(result);
    }

    private static byte[] SetMember(ReadOnlySpan<byte> text, ReadOnlySpan<string> path, JsonNode value)
    {
        var reader = new Utf8JsonReader(text, new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
        int insert = (int)reader.BytesConsumed;
        bool hasMembers = false;
        int start = -1, end = -1;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            hasMembers = true;
            bool found = reader.ValueTextEquals(path[0]);
            if (!reader.Read()) throw new JsonException();
            int pos = (int)reader.TokenStartIndex;
            reader.Skip();
            if (!found) continue;
            if (start >= 0) throw new JsonException("Duplicate provider entry");
            start = pos;
            end = (int)reader.BytesConsumed;
        }
        if (start >= 0)
        {
            byte[] replacement = path.Length == 1 ? Encoding.UTF8.GetBytes(value.ToJsonString()) :
                SetMember(text[start..end], path[1..], value);
            return [..text[..start], ..replacement, ..text[end..]];
        }
        for (int i = path.Length - 1; i >= 1; i--)
            value = new JsonObject { [path[i]] = value };
        var entry = Encoding.UTF8.GetBytes("\n  \"" + path[0] + "\": " + value.ToJsonString() + (hasMembers ? "," : "") + "\n");
        return [..text[..insert], ..entry, ..text[insert..]];
    }
}
