using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tomlyn;
using Tomlyn.Model;

namespace GPTBus;

internal static class ProviderConfig
{
    public const string Site = "https://gptbus.ai";
    // Port of CC Switch's build_claude_settings and CodexLinkConfig::render.
    // See third_party/cc-switch for the pinned sources and MIT notice.
    public static byte[] Render(string client, byte[]? file, string key, string model)
    {
        if (key.Length == 0 || key.Any(c => c < 33 || c > 126))
            throw new InvalidOperationException("请输入有效的 GPTBus API 密钥。");
        if (string.IsNullOrWhiteSpace(model) || model.Any(char.IsControl))
            throw new InvalidOperationException("请先选择或输入模型 ID。");
        var text = new UTF8Encoding(false, true).GetString(file ?? []).TrimStart('\uFEFF');
        string result;
        try
        {
            result = client switch
            {
                "codex" => Codex(text, key, model),
                "claude" => Claude(text, key, model),
                _ => throw new ArgumentException()
            };
        }
        catch (Exception)
        {
            // Parser diagnostics may quote a line containing an existing credential.
            throw new InvalidOperationException("现有配置格式无法安全合并，请先检查客户端配置文件。");
        }
        return Encoding.UTF8.GetBytes(result);
    }

    private static string Codex(string text, string key, string model)
    {
        var root = Toml.ToModel(text);
        root["model_provider"] = "gptbus_connect";
        root["model"] = model;
        if (!root.TryGetValue("model_providers", out var value))
            root["model_providers"] = value = new TomlTable();
        var providers = (TomlTable)value;
        providers["gptbus_connect"] = new TomlTable
        {
            ["name"] = "GPTBus", ["base_url"] = Site + "/v1", ["wire_api"] = "responses",
            ["requires_openai_auth"] = false, ["experimental_bearer_token"] = key
        };
        if (root.TryGetValue("profile", out var active) && active is string name &&
            root.TryGetValue("profiles", out var entries) && entries is TomlTable profiles &&
            profiles.TryGetValue(name, out var entry) && entry is TomlTable profile)
        {
            profile["model_provider"] = "gptbus_connect";
            profile["model"] = model;
        }
        return Toml.FromModel(root);
    }

    private static string Claude(string text, string key, string model)
    {
        var root = text.Length == 0 ? new JsonObject() : JsonNode.Parse(text)!.AsObject();
        var env = root["env"]?.AsObject() ?? new JsonObject();
        if (root["env"] == null) root["env"] = env;
        foreach (var other in new[] { "ANTHROPIC_API_KEY", "CLAUDE_CODE_OAUTH_TOKEN",
                     "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX", "CLAUDE_CODE_USE_FOUNDRY" })
            env.Remove(other);
        env["ANTHROPIC_BASE_URL"] = Site;
        env["ANTHROPIC_AUTH_TOKEN"] = key;
        env["ANTHROPIC_MODEL"] = model;
        foreach (var alias in new[] { "HAIKU", "SONNET", "OPUS" })
            env[$"ANTHROPIC_DEFAULT_{alias}_MODEL"] = model;
        root["model"] = model;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }
}
