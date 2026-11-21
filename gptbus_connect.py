"""Launch a local Codex CLI or Claude Code session connected to GPTBus."""

import argparse
import getpass
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
import warnings


SITE = "https://gptbus.ai"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None  # The bearer credential belongs only to GPTBus.


def api_key():
    key = os.environ.get("GPTBUS_API_KEY", "").strip()
    if not key:
        if not sys.stdin.isatty():
            raise ValueError("非交互运行时，请先设置 GPTBUS_API_KEY。")
        with warnings.catch_warnings():
            warnings.simplefilter("error", getpass.GetPassWarning)
            key = getpass.getpass("GPTBus API Key（输入隐藏）: ").strip()
    if not key or not all(33 <= ord(c) <= 126 for c in key):
        raise ValueError("请输入有效的 GPTBus API Key。")
    return key


def model_catalog(key):
    req = urllib.request.Request(SITE + "/v1/models", headers={
        "Authorization": "Bearer " + key, "Accept": "application/json",
    })
    try:
        with urllib.request.build_opener(NoRedirect).open(req, timeout=15) as response:
            rows = json.load(response)["data"]
        if not isinstance(rows, list):
            raise ValueError
        return sorted({row["id"] for row in rows if isinstance(row, dict)
                       and isinstance(row.get("id"), str) and row["id"].isprintable() and row["id"]})
    except urllib.error.HTTPError as exc:
        raise ValueError(f"无法读取模型目录（HTTP {exc.code}）。请检查密钥及分组权限。") from None
    except (KeyError, ValueError, TypeError, OSError) as exc:
        raise ValueError("无法读取 GPTBus 模型目录。检查网络，或用 --model 指定已知的模型 ID。") from exc


def select_model(models):
    if not models:
        raise ValueError("这把密钥的模型列表为空，请在 GPTBus 后台核对分组。")
    for index, model in enumerate(models, 1):
        print(f"{index:3}. {model}")
    value = input("输入模型序号或完整 ID: ").strip()
    if value in models:
        return value
    if value.isascii() and value.isdigit() and 1 <= int(value) <= len(models):
        return models[int(value) - 1]
    raise ValueError("模型不在上述目录中。")


def executable(client):
    path = shutil.which(client)
    if not path:
        raise ValueError(f"未找到 {client}，请先安装对应的 CLI 并加入 PATH。")
    if os.name != "nt" or Path(path).suffix.lower() not in {".cmd", ".bat"}:
        return [path]
    # Run the npm entry point directly, without passing arguments through cmd.exe.
    package = "@openai/codex" if client == "codex" else "@anthropic-ai/claude-code"
    directory = Path(path).parent / "node_modules" / package
    manifest = directory / "package.json"
    if manifest.is_file():
        entry = json.loads(manifest.read_text(encoding="utf-8")).get("bin", {})
        name = entry.get(client) if isinstance(entry, dict) else entry
        node = shutil.which("node")
        if node and isinstance(name, str) and (directory / name).is_file():
            return [node, str(directory / name)]
    raise ValueError(f"找到了 {client} 的批处理包装器，但无法定位客户端。请使用官方 CLI 安装方式。")


def client_command(client, model, key):
    env = os.environ.copy()
    if client == "codex":
        env["GPTBUS_API_KEY"] = key
        args = ["-c", 'model_provider="gptbus"', "-c",
                'model_providers.gptbus={ name="GPTBus", base_url="https://gptbus.ai/v1", '
                'wire_api="responses", env_key="GPTBUS_API_KEY" }', "--model", model]
    else:
        for var in ("ANTHROPIC_API_KEY", "CLAUDE_CODE_OAUTH_TOKEN", "CLAUDE_CODE_USE_BEDROCK",
                    "CLAUDE_CODE_USE_VERTEX", "CLAUDE_CODE_USE_FOUNDRY"):
            env.pop(var, None)
        env.update(ANTHROPIC_BASE_URL=SITE, ANTHROPIC_AUTH_TOKEN=key)
        args = ["--model", model]
    return executable(client) + args, env


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("client", nargs="?", choices=["codex", "claude"])
    parser.add_argument("--model", help="完整模型 ID；省略时从密钥的可用目录选择")
    parser.add_argument("--list-models", action="store_true", help="列出密钥可见的模型并退出")
    parser.add_argument("--preview", action="store_true", help="只预览客户端、地址、模型和协议")
    args = parser.parse_args()
    if args.list_models:
        print("\n".join(model_catalog(api_key())))
        return 0
    client = args.client or input("选择客户端（codex / claude）: ").strip().lower()
    if client not in ("codex", "claude"):
        raise ValueError("客户端必须为 codex 或 claude。")
    key = None
    model = args.model
    if model is None:
        key = api_key()
        model = select_model(model_catalog(key))
    if not model.strip() or not model.isprintable() or model.startswith("-"):
        raise ValueError("模型 ID 无效。")
    endpoint, protocol = (SITE + "/v1", "Responses") if client == "codex" else (SITE, "Messages")
    print(f"{client}  |  {endpoint}  |  {protocol}  |  {model}", flush=True)
    if args.preview:
        return 0
    command, env = client_command(client, model, key or api_key())
    return subprocess.run(command, env=env).returncode


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError, EOFError, getpass.GetPassWarning) as error:
        sys.exit(f"GPTBus Connect: {error}")
    except KeyboardInterrupt:
        sys.exit(130)
