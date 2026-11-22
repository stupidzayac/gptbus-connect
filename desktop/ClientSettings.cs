using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GPTBus;

internal sealed record Backup(byte[]? Original, string Before, string After);

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
        var directory = client == "codex" ? Environment.GetEnvironmentVariable("CODEX_HOME")
                                         : Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(directory)) directory = Path.Combine(home, "." + client);
        if (!Path.IsPathFullyQualified(directory))
            throw new InvalidOperationException("找不到客户端的设置文件夹。请检查客户端的安装位置。");
        var path = Path.Combine(directory, client == "codex" ? "config.toml" : "settings.json");
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
            throw new InvalidOperationException("其他程序已更改客户端设置。为保留这些更改，本次操作已停止。");
    }

    public void Apply(string key, string model)
    {
        using var guard = Lock();
        var current = Read();
        var before = Hash(current);
        var backup = Load();
        Check(backup, before);
        var next = ProviderConfig.Render(client, current, key, model);
        Write(Journal, JsonSerializer.SerializeToUtf8Bytes(new Backup(backup == null ? current : backup.Original, before, Hash(next)), BackupJson.Default.Backup));
        if (Hash(Read()) != before) throw new IOException("写入前检测到配置已变化。");
        Write(path, next);
    }

    public void Restore()
    {
        using var guard = Lock();
        var backup = Load() ?? throw new InvalidOperationException("这台电脑上还没有可恢复的设置。");
        var current = Hash(Read());
        Check(backup, current);
        if (current != Hash(backup.Original))
        {
            if (backup.Original == null) File.Delete(path);
            else Write(path, backup.Original);
        }
        File.Delete(Journal);
    }
}
