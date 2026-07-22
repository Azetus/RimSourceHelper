import numpy as np
import httpx
from .base import EmbeddingProvider


class ApiProvider(EmbeddingProvider):
    """外部 API：通过 OpenAI 兼容端点（Ollama / LM Studio / 云端）获取向量。"""

    def __init__(self, endpoint: str, model: str, api_key: str = ""):
        self._endpoint = endpoint.rstrip("/")
        self._model = model
        self._api_key = api_key

        # 探测维度：编码单条 "test" 文本
        test_vectors = self.embed(["test"])
        self._dimension = test_vectors.shape[1]

    def embed(self, texts: list[str]) -> np.ndarray:
        headers = {"Content-Type": "application/json"}
        if self._api_key:
            headers["Authorization"] = f"Bearer {self._api_key}"

        payload = {
            "model": self._model,
            "input": texts
        }

        with httpx.Client(timeout=30) as client:
            resp = client.post(self._endpoint, json=payload, headers=headers)
            resp.raise_for_status()
            data = resp.json()

        # OpenAI 兼容格式：data 按 index 排序后取 embedding
        items = sorted(data["data"], key=lambda x: x["index"])
        vectors = [item["embedding"] for item in items]
        return np.array(vectors, dtype=np.float32)

    def get_dimension(self) -> int:
        return self._dimension

    def get_info(self) -> dict:
        return {
            "provider": "api",
            "model": self._model,
            "endpoint": self._endpoint,
            "dimension": self._dimension
        }
