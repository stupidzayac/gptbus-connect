using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GPTBus;

internal sealed class ConnectWindow : Window
{
    private readonly ComboBox client = new() { ItemsSource = new[] { "Codex CLI", "Claude Code" }, SelectedIndex = 0, MinWidth = 180 };
    private readonly TextBox key = new() { PasswordChar = '●', Watermark = "粘贴你的 GPTBus 密钥" };
    private readonly TextBox model = new() { Watermark = "从列表选择，或输入完整模型名称" };
    private readonly ComboBox models = new() { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button restore = new() { Content = "恢复原设置" };
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
    private string Client => client.SelectedIndex == 0 ? "codex" : "claude";

    public ConnectWindow()
    {
        Title = "GPTBus Connect";
        Width = 610; Height = 590; MinWidth = 400; MinHeight = 490;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        using var stream = AssetLoader.Open(new Uri("avares://GPTBus-Connect/bus.png"));
        var logo = new Bitmap(stream);
        Icon = new WindowIcon(logo);
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        heading.Children.Add(new Image { Source = logo, Height = 35, Width = 40, Stretch = Stretch.Uniform });
        heading.Children.Add(new TextBlock { Text = Title, FontSize = 25, FontWeight = FontWeight.SemiBold });
        var panel = new StackPanel { Margin = new Thickness(28), Spacing = 10 };
        panel.Children.Add(heading);
        panel.Children.Add(new TextBlock { Text = "选择你想连接的应用", Margin = new Thickness(0,0,0,6) });
        AddField(panel, "客户端", client);
        AddField(panel, "API 密钥", key);
        var load = new Button { Content = "获取可用模型" };
        load.Click += async (_, _) =>
        {
            var token = key.Text?.Trim() ?? "";
            var selected = Client;
            if (token.Length == 0) { status.Text = "请先输入 GPTBus API 密钥。"; return; }
            load.IsEnabled = false;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, ProviderConfig.Site + "/v1/models");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await Http.SendAsync(request);
                if (selected != Client || token != key.Text?.Trim()) return;
                if (!response.IsSuccessStatusCode)
                {
                    status.Text = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "密钥无效或无权查看模型。请检查密钥后再试。",
                        System.Net.HttpStatusCode.TooManyRequests => "请求较多，请稍后再试。",
                        _ => "暂时无法获取模型。请稍后重试，或输入模型名称。"
                    };
                    return;
                }
                using var doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
                var ids = doc.RootElement.GetProperty("data").EnumerateArray()
                    .Select(row => row.GetProperty("id").GetString()).OfType<string>()
                    .Where(id => id.Length > 0 && !id.Any(char.IsControl)).Distinct().Order().ToArray();
                models.ItemsSource = ids;
                models.IsVisible = ids.Length > 0;
                status.Text = ids.Length == 0 ? "这把密钥还没有可用模型。" : $"找到 {ids.Length} 个模型，请选择你想使用的模型。";
            }
            catch (Exception) { status.Text = "暂时无法获取模型。请检查网络，或输入模型名称。"; }
            finally { load.IsEnabled = true; }
        };
        panel.Children.Add(load);
        AddField(panel, "模型", model);
        panel.Children.Add(models);
        models.SelectionChanged += (_, _) => { if (models.SelectedItem is string id) model.Text = id; };
        key.TextChanged += (_, _) => { models.ItemsSource = null; models.IsVisible = false; };
        var actions = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,6,0,0) };
        var apply = new Button { Content = "连接 GPTBus", Margin = new Thickness(0,0,10,0) };
        apply.Classes.Add("accent");
        apply.Click += (_, _) => Run(settings =>
        {
            settings.Apply(key.Text?.Trim() ?? "", model.Text?.Trim() ?? "");
            key.Text = "";
            return $"设置已保存。请在新的终端窗口运行 {Client}，开始对话。";
        });
        restore.Click += (_, _) => Run(settings =>
        {
            settings.Restore();
            return "已恢复连接前的设置。请重新打开客户端。";
        });
        actions.Children.Add(apply); actions.Children.Add(restore);
        panel.Children.Add(new TextBlock { Text = "密钥会保存在这台电脑上。连接前的设置将自动备份。", FontSize = 13, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(actions); panel.Children.Add(status);
        Content = new ScrollViewer { Content = panel };
        client.SelectionChanged += (_, _) =>
        {
            model.Text = ""; models.ItemsSource = null; models.IsVisible = false; status.Text = ""; Refresh();
        };
        Refresh();
    }

    private static void AddField(StackPanel panel, string label, Control field)
    {
        panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(field);
    }
    private void Refresh()
    {
        try
        {
            var settings = ClientSettings.Open(Client);
            restore.IsEnabled = settings.HasBackup;
        }
        catch (Exception) { status.Text = "找不到客户端的设置文件夹。"; restore.IsEnabled = false; }
    }
    private void Run(Func<ClientSettings,string> action)
    {
        try { status.Text = action(ClientSettings.Open(Client)); }
        catch (InvalidOperationException ex) { status.Text = ex.Message; }
        catch (Exception) { status.Text = "无法保存设置。请检查磁盘空间，并确认你有权限更改客户端设置。"; }
        Refresh();
    }
}
