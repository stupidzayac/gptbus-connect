# GPTBus Connect

在桌面窗口中将 **Codex CLI 或 Claude Code** 切换到 GPTBus。桌面版移植了 CC Switch 的供应商配置逻辑，内置 GPTBus 服务地址，并提供模型选择、自动备份和恢复。使用者无需安装 Python、.NET 或 CC Switch。

**[下载安装包](https://github.com/stupidzayac/gptbus-connect/releases/latest)**

| 系统 | 选择 |
| --- | --- |
| Windows | Intel / AMD 电脑选 `win-x64`，ARM 电脑选 `win-arm64`。解压后双击 `GPTBus-Connect.exe`。 |
| macOS | M 系列选 `osx-arm64`，Intel 选 `osx-x64`。解压后打开 `GPTBus Connect.app`。 |
| Linux | 桌面系统按架构选择 `linux-x64` 或 `linux-arm64`。解压后运行 `./GPTBus-Connect`。需要兼容的图形桌面及系统库。 |

Windows 发行包暂无 Authenticode 签名，macOS 发行包暂无 Apple Developer ID 公证。下载页提供每个压缩包的 SHA-256。

## 使用

1. 安装你要接入的客户端，并在 [GPTBus](https://gptbus.ai/) 创建 API 密钥。
2. 打开 GPTBus Connect，选择客户端并输入密钥。点击「读取模型目录」，或直接输入完整模型 ID。Codex 需要支持 Responses 的模型，Claude Code 需要支持 Anthropic Messages 的模型。
3. 点击「应用到客户端」，新开终端运行 `codex` 或 `claude`。目录查询访问 `https://gptbus.ai/v1/models`。模型调用按账户费率计费。
4. 要切回原设置，在同一客户端页点击「恢复原配置」。若首次应用前没有该文件，恢复时会移除工具创建的文件。

## 配置与备份

- Codex 写入 `~/.codex/config.toml`，支持 `CODEX_HOME`。使用独立的 `gptbus_connect` 供应商，不覆盖 `auth.json`。Claude Code 合并 `~/.claude/settings.json` 中的服务地址、模型和认证设置，支持 `CLAUDE_CONFIG_DIR`。显式命令行参数、项目级配置及外部环境变量仍可能影响客户端的最终选择。
- 密钥以客户端可读取的形式保存在所选配置文件中。首次应用前的原始文件保存在 `~/.gptbus-connect/backups/`，其中也可能包含原凭据。请仅向当前用户开放这两个目录的访问权限。
- 每次应用和恢复都会核对文件内容。若其他程序更改了配置，工具会停止，避免覆盖后续编辑。备份中的 `Original` 保存原文件字节的 Base64 表示。恢复会还原原文件内容。

## 源码

本项目使用 Avalonia 和 .NET 构建原生桌面窗口。[CC Switch 源码版本、移植范围和 MIT 许可](third_party/cc-switch/README.md) · [第三方许可](third_party/NOTICES.txt)。

开发环境需要 .NET 10 SDK。运行 `dotnet run --project desktop` 启动开发版。生成独立程序可运行以下命令，并按需替换平台标识。

```sh
dotnet publish desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

原终端版仍可通过仓库中的 `gptbus_connect.py` 使用。
