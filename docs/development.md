# 配置与备份

- Codex 写入 `~/.codex/config.toml`，支持 `CODEX_HOME`。使用独立的 `gptbus_connect` 供应商，不覆盖 `auth.json`。Claude Code 合并 `~/.claude/settings.json` 中的服务地址、模型和认证设置，支持 `CLAUDE_CONFIG_DIR`。显式命令行参数、项目级配置及外部环境变量仍可能影响客户端的最终选择。
- 密钥以客户端可读取的形式保存在所选配置文件中。首次应用前的原始文件保存在 `~/.gptbus-connect/backups/`，其中也可能包含原凭据。请仅向当前用户开放这两个目录的访问权限。
- 每次应用和恢复都会核对文件内容。若其他程序更改了配置，工具会停止，避免覆盖后续编辑。备份中的 `Original` 保存原文件字节的 Base64 表示。恢复会还原原文件内容。

## 源码

本项目使用 Avalonia 和 .NET 构建原生桌面窗口。[CC Switch 源码版本、移植范围和 MIT 许可](../third_party/cc-switch/README.md) · [第三方许可](../third_party/NOTICES.txt)。

开发环境需要 .NET 10 SDK。运行 `dotnet run --project desktop` 启动开发版。生成独立程序可运行以下命令，并按需替换平台标识。

```sh
dotnet publish desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

原终端版仍可通过仓库中的 `gptbus_connect.py` 使用。
