using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GPTBus;

internal sealed record Backup(byte[]? Original, string Before, string After, string? Catalog = null, string? CodexSignature = null);

[JsonSerializable(typeof(Backup))]
internal partial class BackupJson : JsonSerializerContext { }

internal sealed class ClientSettings(string path, string stateDirectory, string client)
{
    public string Location => path;
    private string Journal => Path.Combine(stateDirectory,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path)))) + ".json");
    public bool HasBackup => File.Exists(Journal);

    public static ClientSettings Open(string client)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string Env(string name, string fallback) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
        var (directory, filename) = client switch
        {
            "codex" or "codex-desktop" => (Env("CODEX_HOME", Path.Combine(home, ".codex")), "config.toml"),
            "claude" => (Env("CLAUDE_CONFIG_DIR", Path.Combine(home, ".claude")), "settings.json"),
            "pi" => (Env("PI_CODING_AGENT_DIR", Path.Combine(home, ".pi", "agent")), "models.json"),
            "opencode" => (Path.Combine(Env("XDG_CONFIG_HOME", Path.Combine(home, ".config")), "opencode"), "opencode.json"),
            _ => throw new InvalidOperationException("无法识别这个客户端。")
        };
        if (client.StartsWith("opencode", StringComparison.Ordinal) && File.Exists(Path.Combine(directory, "opencode.jsonc")))
            filename = "opencode.jsonc";
        if (!Path.IsPathFullyQualified(directory))
            throw new InvalidOperationException("找不到客户端的设置文件夹。如果你自定义过它的位置，请检查是否填写了完整路径。");
        var path = Path.Combine(directory, filename);
        return new(path, Path.Combine(home, ".gptbus-connect", "backups"), client);
    }

    private static string Hash(byte[]? bytes) => bytes == null ? "missing" : Convert.ToHexString(SHA256.HashData(bytes));
    private byte[]? Read()
    {
        if (new FileInfo(path).LinkTarget != null)
            throw new InvalidOperationException("设置文件已链接到其他位置，请在客户端中手动连接。");
        try { return File.ReadAllBytes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static void MakeDirectory(string directory)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void Write(string target, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(target)!;
        MakeDirectory(directory);
        var tmp = Path.Combine(directory, ".gptbus-" + Guid.NewGuid().ToString("N"));
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            using (var stream = new FileStream(tmp, options)) { stream.Write(bytes); stream.Flush(true); }
            File.Move(tmp, target, true);
        }
        finally { File.Delete(tmp); }
    }

    private FileStream Lock()
    {
        MakeDirectory(stateDirectory);
        return new FileStream(Journal + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
    }
    private Backup? Load() => HasBackup
        ? JsonSerializer.Deserialize(File.ReadAllBytes(Journal), BackupJson.Default.Backup) ?? throw new IOException("备份无效。") : null;

    private static void Check(Backup? backup, string current)
    {
        if (backup != null && current != backup.After && current != backup.Before && current != Hash(backup.Original))
            throw new InvalidOperationException("客户端设置已发生变化。为保留这些更改，本次操作已停止。");
    }

    public void Apply(string key, string model)
    {
        using var guard = Lock();
        var current = Read();
        var before = Hash(current);
        var backup = Load();
        Check(backup, before);
        string? catalogPath = null;
        byte[]? catalog = null;
        if (client == "codex-desktop" || (client == "codex" && backup?.Catalog != null))
        {
            catalog = ProviderConfig.CodexCatalog(model);
            catalogPath = Path.Combine(Path.GetDirectoryName(path)!, $"gptbus-models-{Hash(catalog)}.json");
        }
        var next = ProviderConfig.Render(client, current, key, model, catalogPath);
        if (catalogPath != null && catalog != null)
        {
            if (new FileInfo(catalogPath).LinkTarget != null)
                throw new InvalidOperationException("模型列表文件链接到了其他位置，工具已停止设置。");
            if (!File.Exists(catalogPath)) Write(catalogPath, catalog);
            else if (Hash(File.ReadAllBytes(catalogPath)) != Hash(catalog))
                throw new InvalidOperationException("模型列表文件已被修改，工具已停止设置，以保留你的更改。");
        }
        Write(Journal, JsonSerializer.SerializeToUtf8Bytes(new Backup(backup == null ? current : backup.Original, before, Hash(next), catalogPath ?? backup?.Catalog, catalogPath == null ? null : ProviderConfig.CodexSignature(next, next)), BackupJson.Default.Backup));
        if (Hash(Read()) != before) throw new IOException("写入前检测到配置已变化。");
        Write(path, next);
        if (catalogPath != null && backup?.Catalog != catalogPath) RemoveCatalog(backup?.Catalog);
    }

    private void RemoveCatalog(string? catalog)
    {
        if (catalog == null || Path.GetDirectoryName(catalog) != Path.GetDirectoryName(path)) return;
        var info = new FileInfo(catalog);
        if (info.LinkTarget != null || !info.Exists || info.Length > 1024 * 1024) return;
        if (info.Name == $"gptbus-models-{Hash(File.ReadAllBytes(catalog))}.json") File.Delete(catalog);
    }

    public void Restore()
    {
        using var guard = Lock();
        var backup = Load() ?? throw new InvalidOperationException("这台电脑上还没有可恢复的设置。");
        var bytes = Read();
        var current = Hash(bytes);
        if (backup.CodexSignature != null && bytes != null && current != backup.After &&
            current != backup.Before && current != Hash(backup.Original))
        {
            var original = backup.Original ?? [];
            var restored = ProviderConfig.RestoreDesktopFields(bytes, original, backup.CodexSignature);
            if (Hash(Read()) != current) throw new IOException("设置在恢复前已变化。");
            Write(path, restored);
            RemoveCatalog(backup.Catalog);
            File.Delete(Journal);
            return;
        }
        Check(backup, current);
        if (current != Hash(backup.Original))
        {
            if (backup.Original == null) File.Delete(path);
            else Write(path, backup.Original);
        }
        RemoveCatalog(backup.Catalog);
        File.Delete(Journal);
    }
}
