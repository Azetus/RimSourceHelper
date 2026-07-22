# embedding-service

文本向量化服务。为 RimSourceHelper 的语义搜索提供 embedding 能力。

## 安装

```bash
pip install -r requirements.txt
```

## 配置

编辑 `config.json`：

```json
{
  "host": "127.0.0.1",
  "port": 8000,
  "provider": "local",
  "model": "bge-m3",
  "modelPath": "./models",
  "batchSize": 32,

  "api": {
    "endpoint": "http://localhost:11434/v1/embeddings",
    "key": ""
  }
}
```

### provider = "local"

使用本地模型。`model` 为 HuggingFace 模型名，`modelPath` 为权重缓存目录。sentence-transformers 会自动在该目录下查找模型，存在则直接加载，不存在则从 HuggingFace 下载。

### provider = "api"

使用 OpenAI 兼容的 embedding API（Ollama、LM Studio、云端）。

## 启动

```bash
cd packages/embedding-service
python -m app.main
```

可选参数（调试用）：

```bash
python -m app.main --port 8001           # 覆盖端口
python -m app.main --config ./custom.json # 指定配置文件
```

## API

### GET /health

服务健康检查。

```
curl http://127.0.0.1:8000/health
→ {"status":"ok"}
```

### GET /info

返回当前 provider 的配置信息。

```
curl http://127.0.0.1:8000/info
→ {"provider":"local","model":"bge-m3","cacheDir":"...","dimension":1024}
```

### POST /embed

将文本编码为向量。

```
curl -X POST http://127.0.0.1:8000/embed \
  -H "Content-Type: application/json" \
  -d '{"texts":["Pawn Kill method","CompShield PreApplyDamage"]}'

→ {"vectors":[[0.123,-0.456,...],[0.789,0.012,...]],"dimension":1024}
```
