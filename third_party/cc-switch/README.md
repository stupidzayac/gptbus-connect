# CC Switch

GPTBus Connect 的供应商配置写入逻辑移植自 [CC Switch v4.0.38](https://github.com/farion1231/cc-switch/tree/2c084ec752a985fa77470facc14cad76ee67e8e1)，上游采用 [MIT 许可证](LICENSE)。

- Claude Code 的环境变量合并和模型别名映射来自 [build_claude_settings](https://github.com/farion1231/cc-switch/blob/2c084ec752a985fa77470facc14cad76ee67e8e1/src-tauri/src/deeplink/provider.rs)。
- Codex 的供应商表、Responses 协议和供应商级密钥来自 [CodexLinkConfig::render](https://github.com/farion1231/cc-switch/blob/2c084ec752a985fa77470facc14cad76ee67e8e1/src-tauri/src/deeplink/codex_link.rs) 和 [供应商级凭据用例](https://github.com/farion1231/cc-switch/blob/2c084ec752a985fa77470facc14cad76ee67e8e1/src-tauri/src/deeplink/tests.rs#L735-L745)。

本项目将上述实现移植到 C#，限定服务地址为 GPTBus，使用独立的供应商标识、桌面界面和备份目录。
