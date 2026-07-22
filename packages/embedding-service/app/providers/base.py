from abc import ABC, abstractmethod
import numpy as np


class EmbeddingProvider(ABC):
    """文本 -> 向量的抽象接口。LocalProvider 和 ApiProvider 各自实现。"""

    @abstractmethod
    def embed(self, texts: list[str]) -> np.ndarray:
        """批量文本编码，返回 (N, D) numpy 数组"""
        ...

    @abstractmethod
    def get_dimension(self) -> int:
        """向量维度"""
        ...

    @abstractmethod
    def get_info(self) -> dict:
        """返回 {provider, model, dimension, ...}  供 /info 端点使用"""
        ...
