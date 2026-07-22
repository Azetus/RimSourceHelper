import argparse
import json
import os
import sys
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from .providers.local import LocalProvider
from .providers.api import ApiProvider
from .providers.base import EmbeddingProvider

app = FastAPI(title="embedding-service")
provider: EmbeddingProvider | None = None


class EmbedRequest(BaseModel):
    texts: list[str]


class EmbedResponse(BaseModel):
    vectors: list[list[float]]
    dimension: int


def load_config(config_path: str) -> dict:
    if not os.path.isfile(config_path):
        print(f"Config file not found: {config_path}", file=sys.stderr)
        sys.exit(1)
    with open(config_path, encoding="utf-8") as f:
        return json.load(f)


def create_provider(config: dict) -> EmbeddingProvider:
    provider_type = config.get("provider", "local")
    model = config.get("model")
    if not model:
        raise ValueError("model is required in config")
    batch_size = config.get("batchSize", 32)

    if provider_type == "local":
        model_path = config.get("modelPath")
        if not model_path:
            raise ValueError("modelPath is required when provider=local")
        return LocalProvider(model, model_path, batch_size)

    if provider_type == "api":
        api_cfg = config.get("api", {})
        endpoint = api_cfg.get("endpoint")
        if not endpoint:
            raise ValueError("api.endpoint is required when provider=api")
        return ApiProvider(endpoint, model, api_cfg.get("key", ""))

    raise ValueError(f"Unknown provider: {provider_type}")


@app.get("/health")
def health():
    if provider is None:
        raise HTTPException(status_code=503, detail="Provider not initialized")
    return {"status": "ok"}


@app.get("/info")
def info():
    if provider is None:
        raise HTTPException(status_code=503, detail="Provider not initialized")
    return provider.get_info()


@app.post("/embed", response_model=EmbedResponse)
def embed(request: EmbedRequest):
    if provider is None:
        raise HTTPException(status_code=503, detail="Provider not initialized")
    if not request.texts:
        raise HTTPException(status_code=400, detail="texts must not be empty")

    vectors = provider.embed(request.texts)
    return EmbedResponse(
        vectors=vectors.tolist(),
        dimension=vectors.shape[1]
    )


if __name__ == "__main__":
    import uvicorn

    parser = argparse.ArgumentParser(description="embedding-service")
    parser.add_argument("--config", default=None, help="Config file path (default: ../config.json)")
    parser.add_argument("--host", default=None, help="Override host")
    parser.add_argument("--port", type=int, default=None, help="Override port")
    args = parser.parse_args()

    config_path = args.config or os.path.join(os.path.dirname(__file__), "..", "config.json")
    config = load_config(config_path)

    host = args.host or config.get("host", "127.0.0.1")
    port = args.port or config.get("port", 8000)

    provider = create_provider(config)
    print(f"Provider: {provider.get_info()}", file=sys.stderr)
    print(f"Listening on {host}:{port}", file=sys.stderr)

    uvicorn.run(app, host=host, port=port)
