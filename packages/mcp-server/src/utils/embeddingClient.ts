// HTTP 客户端：调 embedding-service 的 GET /health、GET /info、POST /embed
const FETCH_TIMEOUT_MS = 30_000;

async function fetchJson(url: string, options?: RequestInit): Promise<any> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);

  try {
    const resp = await fetch(url, { ...options, signal: controller.signal });
    if (!resp.ok) throw new Error(`${url}: ${resp.status} ${resp.statusText}`);
    return resp.json();
  } finally {
    clearTimeout(timer);
  }
}

export async function embeddingHealthCheck(baseUrl: string): Promise<boolean> {
  try {
    const data = await fetchJson(`${baseUrl}/health`);
    return data.status === "ok";
  } catch {
    return false;
  }
}

export interface EmbeddingInfo {
  provider: string;
  model: string;
  dimension: number;
}

export async function embeddingGetInfo(baseUrl: string): Promise<EmbeddingInfo> {
  return fetchJson(`${baseUrl}/info`);
}

export async function embeddingEmbed(baseUrl: string, texts: string[]): Promise<number[][]> {
  const resp = await fetchJson(`${baseUrl}/embed`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ texts }),
  });
  return resp.vectors as number[][];
}
