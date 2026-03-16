using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TetaCode.Service.Services;

public class OCRService : IOCRService
{
    private const string BaseUrlV1Beta = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string BaseUrlV1 = "https://generativelanguage.googleapis.com/v1/models";

    /// <summary>Manuel deneme listesi: Dashboard'daki experimental/preview isimleri.</summary>
    private static readonly string[] ManualFallbackModels = {
        "gemini-2.0-flash-exp",
        "gemini-2.0-flash-lite-preview",
        "gemini-2-flash-lite",
        "gemini-2.0-flash",
        "gemini-3.1-pro"
    };

    private const string OcrPrompt = "Sen profesyonel bir OCR motorusun. Bu görseldeki el yazılarını ve metinleri hiçbir yorum eklemeden, olduğu gibi dijital metne çevir.";

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OCRService> _logger;

    public OCRService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<OCRService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> ExtractTextFromImageAsync(Stream imageStream, string? language = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        if (imageStream.CanSeek)
            imageStream.Position = 0;

        var apiKey = _configuration["Gemini:ApiKey"] ?? _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API anahtarı bulunamadı. appsettings.json içinde 'Gemini:ApiKey' değerini ayarlayın.");
        }

        using var httpClientForList = _httpClientFactory.CreateClient();
        var listUrl = $"{BaseUrlV1Beta}?key={Uri.EscapeDataString(apiKey)}";
        var modelIdsToTry = new List<string>();

        // ListModels: tüm model isimlerini al ve terminale bas; "flash" geçen ilk modeli seç
        try
        {
            var listResponse = await httpClientForList.GetAsync(listUrl, cancellationToken);
            var listJson = await listResponse.Content.ReadAsStringAsync(cancellationToken);
            var (allNames, flashModelId) = ParseListModelsResponse(listJson);
            foreach (var name in allNames)
            {
                Console.WriteLine("[OCR Gemini] ListModels: " + name);
                _logger.LogInformation("OCR Gemini ListModels: {ModelName}", name);
            }
            if (!string.IsNullOrEmpty(flashModelId))
            {
                modelIdsToTry.Add(flashModelId);
                Console.WriteLine("[OCR Gemini] Dinamik seçim (flash): " + flashModelId);
                _logger.LogInformation("OCR Gemini: Dinamik seçim (flash içeren ilk model): {ModelId}", flashModelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR Gemini: ListModels isteği başarısız, sadece manuel liste kullanılacak.");
        }

        foreach (var id in ManualFallbackModels)
        {
            if (!modelIdsToTry.Contains(id))
                modelIdsToTry.Add(id);
        }

        if (modelIdsToTry.Count == 0)
            modelIdsToTry.AddRange(ManualFallbackModels);

        var imageBytes = await ReadStreamToByteArrayAsync(imageStream, cancellationToken);
        var base64Data = Convert.ToBase64String(imageBytes);

        // contents[] içinde parts[]: metin + inline_data (mime_type: image/jpeg, data: base64)
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = OcrPrompt },
                        new { inline_data = new { mime_type = "image/jpeg", data = base64Data } }
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

            // Önce v1beta ile dene
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var response = await httpClient.PostAsync(urlBeta, content, cancellationToken);
                lastResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                lastStatusCode = (int)response.StatusCode;
                lastReasonPhrase = response.ReasonPhrase;

                if (response.IsSuccessStatusCode)
                    return ParseGeminiResponse(lastResponseBody);

                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    // 404 dışı hata: açıklayıcı mesaj ile fırlat (örn. "model is no longer available")
                    throw new InvalidOperationException(
                        $"Gemini API hatası ({lastStatusCode}): {lastReasonPhrase}. ResponseBody: {lastResponseBody}");
                }
            }

            // 404: v1 ile tekrar dene
            _logger.LogWarning("OCR Gemini: {Model} v1beta 404 döndü, v1 ile deneniyor.", modelName);
            using (var contentV1 = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var responseV1 = await httpClient.PostAsync(urlV1, contentV1, cancellationToken);
                lastResponseBody = await responseV1.Content.ReadAsStringAsync(cancellationToken);
                lastStatusCode = (int)responseV1.StatusCode;
                lastReasonPhrase = responseV1.ReasonPhrase;

                if (responseV1.IsSuccessStatusCode)
                    return ParseGeminiResponse(lastResponseBody);

                if (responseV1.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"Gemini API hatası ({lastStatusCode}): {lastReasonPhrase}. ResponseBody: {lastResponseBody}");
                }
            }

            _logger.LogWarning("OCR Gemini: {Model} v1 de 404. Sıradaki modele geçiliyor.", modelName);
        }

        // Tüm modeller 404: responseBody'i terminalde görmek için exception ile fırlat (model is no longer available vb.)
        throw new InvalidOperationException(
            $"Gemini API: Tüm modeller 404. Son yanıt ({lastStatusCode}): {lastReasonPhrase}. ResponseBody: {lastResponseBody}");
    }

    /// <summary>ListModels yanıtını parse eder; tüm model isimlerini ve "flash" geçen ilkinin id'sini döner.</summary>
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
        catch { /* ignore */ }
        return (allNames, flashModelId);
    }

    private static async Task<byte[]> ReadStreamToByteArrayAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static string ParseGeminiResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            if (root.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                throw new InvalidOperationException($"Gemini API: {msg.GetString()}");
            return string.Empty;
        }
        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            return string.Empty;
        var part = parts[0];
        if (!part.TryGetProperty("text", out var text))
            return string.Empty;
        return text.GetString() ?? string.Empty;
    }

    public Task<byte[]> GeneratePdfFromTextAsync(string title, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            title = "Belge";
        if (string.IsNullOrWhiteSpace(content))
            content = "(İçerik yok)";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(56.7f, Unit.Point); // 2 cm
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                page.Header().Text(title).Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                page.Content().PaddingVertical(10).Text(content);
                page.Footer().AlignCenter().DefaultTextStyle(s => s.FontSize(9).FontColor(Colors.Grey.Medium)).Text(x =>
                {
                    x.Span("Sayfa ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        using var stream = new MemoryStream();
        doc.GeneratePdf(stream);
        var bytes = stream.ToArray();
        return Task.FromResult(bytes);
    }
}
