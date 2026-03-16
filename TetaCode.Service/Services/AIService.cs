using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TetaCode.Service.Dtos;

namespace TetaCode.Service.Services;

public class AIService : IAIService
{
    private const string BaseUrlV1Beta = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string BaseUrlV1 = "https://generativelanguage.googleapis.com/v1/models";

    // OCRService ile aynı fallback listesi; ListModels ile gelen ilk flash modeli öne alacağız.
    private static readonly string[] ManualFallbackModels =
    {
        "gemini-2.0-flash-exp",
        "gemini-2.0-flash-lite-preview",
        "gemini-2-flash-lite",
        "gemini-2.0-flash",
        "gemini-3.1-pro"
    };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AIService> _logger;

    public AIService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<AIService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> SummarizeNoteAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        const string prompt =
            "Bu ders notunu en önemli kısımlarını kapsayacak şekilde 5 maddelik kısa bir özete dönüştür. Sadece maddeleri dön.";

        var text = await CallGeminiForTextAsync(prompt, content, cancellationToken);
        return text ?? string.Empty;
    }

    public async Task<IReadOnlyList<QuizQuestionDto>> GenerateQuizAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<QuizQuestionDto>();
        }

        const string prompt =
            "Bu metinden 3 adet çoktan seçmeli soru üret. Yanıtı mutlaka şu JSON formatında dön: " +
            "[{\"Question\": \"...\", \"Options\": [\"A\", \"B\", \"C\", \"D\"], \"CorrectAnswer\": \"...\"}]. " +
            "Hiç açıklama yazma, sadece geçerli JSON üret.";

        var raw = await CallGeminiForTextAsync(prompt, content, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<QuizQuestionDto>();

        try
        {
            // Bazı modeller JSON'u kod bloğu içine koyabilir; bunu temizlemeye çalış.
            var cleaned = raw.Trim();
            if (cleaned.StartsWith("```"))
            {
                var firstNewLine = cleaned.IndexOf('\n');
                if (firstNewLine >= 0)
                {
                    cleaned = cleaned[(firstNewLine + 1)..];
                }

                var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0)
                {
                    cleaned = cleaned[..lastFence];
                }
                cleaned = cleaned.Trim();
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var questions = JsonSerializer.Deserialize<List<QuizQuestionDto>>(cleaned, options);
            if (questions == null || questions.Count == 0)
                return Array.Empty<QuizQuestionDto>();

            // Options null gelirse boş listeye zorla ve doğru cevabı seçenek metnine normalize et
            foreach (var q in questions)
            {
                q.Options ??= new List<string>();
                NormalizeCorrectAnswer(q);
            }

            return questions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quiz JSON parse edilemedi. Raw: {Raw}", raw);
            throw new InvalidOperationException("AI'dan dönen quiz verisi beklenen JSON formatında değil.", ex);
        }
    }

    private static void NormalizeCorrectAnswer(QuizQuestionDto question)
    {
        if (question.Options == null || question.Options.Count == 0)
            return;

        var answerRaw = (question.CorrectAnswer ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(answerRaw))
            return;

        // Eğer sadece harf geldiyse (A/B/C/D) onu ilgili şıkkın metnine çevir
        var upper = answerRaw.ToUpperInvariant();
        var letters = new[] { "A", "B", "C", "D" };
        var letterIndex = Array.IndexOf(letters, upper);
        if (letterIndex >= 0 && letterIndex < question.Options.Count)
        {
            question.CorrectAnswer = question.Options[letterIndex];
            return;
        }

        // Sayısal indeks (1-4) geldiyse onu da yorumla
        if (int.TryParse(answerRaw, out var idx) && idx >= 1 && idx <= question.Options.Count)
        {
            question.CorrectAnswer = question.Options[idx - 1];
            return;
        }

        // Aksi halde, metin olarak şıklar arasında case-insensitive eşleşme ara
        var match = question.Options.FirstOrDefault(
            o => string.Equals(o.Trim(), answerRaw, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            question.CorrectAnswer = match;
        }
    }

    private async Task<string?> CallGeminiForTextAsync(string systemPrompt, string userContent, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Gemini:ApiKey"] ?? _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API anahtarı bulunamadı. appsettings.json içinde 'Gemini:ApiKey' değerini ayarlayın.");
        }

        using var httpClientForList = _httpClientFactory.CreateClient();
        var listUrl = $"{BaseUrlV1Beta}?key={Uri.EscapeDataString(apiKey)}";
        var modelIdsToTry = new List<string>();

        try
        {
            var listResponse = await httpClientForList.GetAsync(listUrl, cancellationToken);
            var listJson = await listResponse.Content.ReadAsStringAsync(cancellationToken);
            var (allNames, flashModelId) = ParseListModelsResponse(listJson);
            foreach (var name in allNames)
            {
                Console.WriteLine("[AI Gemini] ListModels: " + name);
                _logger.LogInformation("AI Gemini ListModels: {Model}", name);
            }

            if (!string.IsNullOrEmpty(flashModelId))
            {
                modelIdsToTry.Add(flashModelId);
                Console.WriteLine("[AI Gemini] Dinamik seçim (flash): " + flashModelId);
                _logger.LogInformation("AI Gemini: Dinamik seçim (flash içeren ilk model): {Model}", flashModelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI Gemini: ListModels isteği başarısız, sadece manuel liste kullanılacak.");
        }

        foreach (var id in ManualFallbackModels)
        {
            if (!modelIdsToTry.Contains(id))
                modelIdsToTry.Add(id);
        }

        if (modelIdsToTry.Count == 0)
            modelIdsToTry.AddRange(ManualFallbackModels);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = systemPrompt },
                        new { text = userContent }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var httpClient = _httpClientFactory.CreateClient();

        string? lastResponseBody = null;
        int? lastStatusCode = null;
        string? lastReasonPhrase = null;

        foreach (var modelName in modelIdsToTry)
        {
            var urlBeta = $"{BaseUrlV1Beta}/{modelName}:generateContent?key={Uri.EscapeDataString(apiKey)}";
            var urlV1 = $"{BaseUrlV1}/{modelName}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var response = await httpClient.PostAsync(urlBeta, content, cancellationToken);
                lastResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                lastStatusCode = (int)response.StatusCode;
                lastReasonPhrase = response.ReasonPhrase;

                if (response.IsSuccessStatusCode)
                    return ParseGeminiTextResponse(lastResponseBody);

                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"Gemini API hatası ({lastStatusCode}): {lastReasonPhrase}. ResponseBody: {lastResponseBody}");
                }
            }

            _logger.LogWarning("AI Gemini: {Model} v1beta 404 döndü, v1 ile deneniyor.", modelName);
            using (var contentV1 = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var responseV1 = await httpClient.PostAsync(urlV1, contentV1, cancellationToken);
                lastResponseBody = await responseV1.Content.ReadAsStringAsync(cancellationToken);
                lastStatusCode = (int)responseV1.StatusCode;
                lastReasonPhrase = responseV1.ReasonPhrase;

                if (responseV1.IsSuccessStatusCode)
                    return ParseGeminiTextResponse(lastResponseBody);

                if (responseV1.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"Gemini API hatası ({lastStatusCode}): {lastReasonPhrase}. ResponseBody: {lastResponseBody}");
                }
            }

            _logger.LogWarning("AI Gemini: {Model} v1 de 404. Sıradaki modele geçiliyor.", modelName);
        }

        throw new InvalidOperationException(
            $"Gemini API: Tüm modeller 404. Son yanıt ({lastStatusCode}): {lastReasonPhrase}. ResponseBody: {lastResponseBody}");
    }

    private static (List<string> allNames, string? flashModelId) ParseListModelsResponse(string listJson)
    {
        var allNames = new List<string>();
        string? flashModelId = null;
        try
        {
            using var doc = JsonDocument.Parse(listJson);
            if (!doc.RootElement.TryGetProperty("models", out var models))
                return (allNames, null);
            foreach (var m in models.EnumerateArray())
            {
                if (!m.TryGetProperty("name", out var nameEl))
                    continue;
                var name = nameEl.GetString();
                if (string.IsNullOrEmpty(name))
                    continue;
                allNames.Add(name);
                if (flashModelId == null && name.Contains("flash", StringComparison.OrdinalIgnoreCase))
                {
                    flashModelId = name.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
                        ? name.Substring(7)
                        : name;
                }
            }
        }
        catch
        {
            // ignore
        }

        return (allNames, flashModelId);
    }

    private static string? ParseGeminiTextResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            if (root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                throw new InvalidOperationException($"Gemini API: {msg.GetString()}");
            return null;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.GetArrayLength() == 0)
            return null;

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textEl))
            {
                var text = textEl.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return null;
    }
}

