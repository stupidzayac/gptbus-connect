# GPTBus Connect

在你常用的编程客户端里使用 GPTBus 模型。打开 GPTBus Connect，登录账号、选择客户端和模型，工具就会把 API 地址、密钥和模型写入所选客户端，并备份原设置。

支持 Windows、macOS 和 Linux。[下载最新版本](https://github.com/stupidzayac/gptbus-connect/releases/latest) · [查看接入指南](https://gptbus.ai/about)

### 下载

按电脑的系统和芯片选择 v1.2.1 下载包。解压后打开 GPTBus Connect。

| 系统 | Intel / AMD（x64） | Arm64 |
| --- | --- | --- |
| Windows | [Windows x64](https://github.com/stupidzayac/gptbus-connect/releases/download/v1.2.1/GPTBus-Connect-v1.2.1-win-x64.zip) | [Windows Arm64](https://github.com/stupidzayac/gptbus-connect/releases/download/v1.2.1/GPTBus-Connect-v1.2.1-win-arm64.zip) |
| macOS | [Intel 芯片](https://github.com/stupidzayac/gptbus-connect/releases/download/v1.2.1/GPTBus-Connect-v1.2.1-osx-x64.zip) | [Apple 芯片（M 系列）](https://github.com/stupidzayac/gptbus-connect/releases/download/v1.2.1/GPTBus-Connect-v1.2.1-osx-arm64.zip) |
| Linux | [Linux x86-64](https://github.com/stupidzayac/gptbus-connect/releases/download/v1.2.1/GPTBus-Connect-v1.2.1-linux-x64.zip) | [Linux Arm64](https://github.com/stupidzayac/gptbus-connect/releases/download/v1.2.1/GPTBus-Connect-v1.2.1-linux-arm64.zip) |

### 首次连接

1. **选择客户端。** 在电脑上安装你要使用的客户端，然后打开 GPTBus Connect，选中对应的应用。Codex 桌面版和 Codex CLI 分别列出，请按实际使用方式选择。
2. **登录 GPTBus。** 点击「登录 GPTBus」。在官网确认当前账号，核对网页和工具显示的识别码，再点击「允许并返回工具」。
3. **选择模型并连接。** 返回工具后，从列表中选择模型，点击「连接 GPTBus」。可用模型取决于本次授权的密钥权限。
4. **开始使用。** 按下表打开客户端并选择模型。模型调用按 GPTBus 账户费率计费。

| 客户端 | 连接后如何开始 |
| --- | --- |
| Codex 桌面版 | 完全退出并重新打开 Codex，在新对话中选择刚配置的模型。 |
| Codex CLI | 新开一个终端，运行 `codex`。 |
| Claude Code | 新开一个终端，运行 `claude`。 |
| OpenCode | 重新打开 OpenCode，在模型列表中选择 GPTBus 下的模型。 |
| Pi | 重新打开 Pi，输入 `/model`，选择 GPTBus 下的模型。 |

**已有 API 密钥？** 展开「手动输入密钥」，粘贴密钥并点击「获取可用模型」。也可以直接输入有权使用的完整模型名称，再点击「连接 GPTBus」。

### 恢复设置与管理授权

要切回原来的配置，在 GPTBus Connect 中选择同一个客户端，点击「恢复原设置」，再重新打开客户端。

如果工具提示配置已被其他程序更改，请先核对并保存这些更改，再处理恢复操作。

要撤销通过官网登录授予的访问权限，在 GPTBus 控制台停用对应的 GPTBus Connect 密钥。密钥保存在本机的客户端配置中，分享配置文件前请移除密钥。[管理 API 密钥](https://gptbus.ai/keys)

### 接入地址

GPTBus Connect 会自动填写服务地址。手动检查配置时，OpenAI 兼容客户端的 API 基础地址为 `https://api.gptbus.ai/v1`，Claude Code 的 `ANTHROPIC_BASE_URL` 为 `https://api.gptbus.ai`。账号登录和授权在 `gptbus.ai` 完成。

### 项目致谢

部分客户端配置逻辑移植自 CC Switch。移植范围、上游版本和许可证可在仓库中查阅。[CC Switch 来源说明](https://github.com/stupidzayac/gptbus-connect/tree/main/third_party/cc-switch) · [第三方许可](https://github.com/stupidzayac/gptbus-connect/blob/main/third_party/NOTICES.txt)
