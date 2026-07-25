using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RimAnalyzer.Analysis.Embedding;

// embeding-service 的 HTTP 客户端：调 GET /health、GET /info、POST /embed
public class EmbeddingClient
{
    private readonly HttpClient _http;

    public EmbeddingClient(string baseUrl)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
    }

    // GET /health → 返回服务是否可用
    public bool HealthCheck()
    {
        var resp = _http.GetAsync("/health").Result;
        return resp.IsSuccessStatusCode;
    }

    // GET /info → 返回 {provider, model, dimension}
    public EmbeddingInfo GetInfo()
    {
        var resp = _http.GetAsync("/info").Result;
        resp.EnsureSuccessStatusCode();
        return resp.Content.ReadFromJsonAsync<EmbeddingInfo>().Result!;
    }

    // POST /embed → 返回 N×D 向量数组
    public float[][] Embed(string[] texts)
    {
        var request = new EmbedRequest { texts = texts };
        var resp = _http.PostAsJsonAsync("/embed", request).Result;
        resp.EnsureSuccessStatusCode();
        var result = resp.Content.ReadFromJsonAsync<EmbedResponse>().Result!;
        return result.vectors;
    }

    private class EmbedRequest
    {
        public string[] texts { get; set; } = [];
    }

    private class EmbedResponse
    {
        public float[][] vectors { get; set; } = [];
        public int dimension { get; set; }
    }
}

public record EmbeddingInfo(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("dimension")] int Dimension
);
