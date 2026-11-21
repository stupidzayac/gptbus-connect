# GPTBus Connect

在终端里用自己的 GPTBus API 密钥启动 Codex CLI 或 Claude Code。脚本会列出这把密钥可见的模型，让你选择本次会话使用的模型。需要 Python 3.9 或更高版本，以及已安装且可在终端运行的客户端。

## 开始使用

1. 在 [GPTBus](https://gptbus.ai/) 创建 API 密钥。
2. [下载 gptbus_connect.py](https://raw.githubusercontent.com/stupidzayac/gptbus-connect/main/gptbus_connect.py)，然后在脚本所在目录打开终端。
3. 运行下方任一命令，根据提示输入密钥并选择模型。Windows 可用 `py -3` 代替 `python3`。

```sh
python3 gptbus_connect.py codex
python3 gptbus_connect.py claude
```

密钥输入时隐藏。脚本也支持通过已有的 `GPTBUS_API_KEY` 环境变量读取密钥。GPTBus 设置仅用于本次启动的客户端进程，脚本不会写入密钥或修改客户端配置文件。下次可再次运行脚本。

## 查看模型与连接设置

```sh
python3 gptbus_connect.py --list-models
python3 gptbus_connect.py codex --model MODEL_ID --preview
python3 gptbus_connect.py claude --model MODEL_ID
```

将 `MODEL_ID` 替换为实际的模型 ID。第一条命令用密钥读取模型目录，第二条在本地显示指定模型的连接设置，第三条直接启动 Claude Code。

| 客户端 | GPTBus 接口地址 | 模型需要支持的协议 |
| --- | --- | --- |
| Codex CLI | `https://gptbus.ai/v1` | OpenAI Responses |
| Claude Code | `https://gptbus.ai` | Anthropic Messages |

如果目录中的模型在客户端内返回协议错误，请在[模型广场](https://gptbus.ai/pricing)核对接口兼容性和密钥分组。实际模型请求按 GPTBus 账户费率计费。
