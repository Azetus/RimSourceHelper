import os
import numpy as np
from sentence_transformers import SentenceTransformer
from .base import EmbeddingProvider


class LocalProvider(EmbeddingProvider):
    """本地模型：优先从缓存加载，不存在则从 HuggingFace 下载。"""

    def __init__(self, model_name: str, model_path: str, batch_size: int = 32):
        abs_path = os.path.abspath(model_path)
        self._model_name = model_name
        self._cache_dir = abs_path
        self._batch_size = batch_size

        try:
            self._model = SentenceTransformer(model_name, cache_folder=abs_path, local_files_only=True)
        except Exception:
            self._model = SentenceTransformer(model_name, cache_folder=abs_path)

    def embed(self, texts: list[str]) -> np.ndarray:
        return self._model.encode(
            texts,
            batch_size=self._batch_size,
            normalize_embeddings=True,
            show_progress_bar=False
        )

    def get_dimension(self) -> int:
        return self._model.get_sentence_embedding_dimension()

    def get_info(self) -> dict:
        return {
            "provider": "local",
            "model": self._model_name,
            "cacheDir": self._cache_dir,
            "dimension": self.get_dimension()
        }
