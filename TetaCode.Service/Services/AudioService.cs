using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TetaCode.Core.Entities;
using TetaCode.Data;
using TetaCode.Service.Dtos;

namespace TetaCode.Service.Services;

public class AudioService : IAudioService
{
    private const string BaseUrlV1Beta = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string BaseUrlV1 = "https://generativelanguage.googleapis.com/v1/models";

    private static readonly string[] ManualFallbackModels =
    {
        "gemini-2.0-flash-exp",
        "gemini-2.0-flash-lite-preview",
        "gemini-2-flash-lite",
        "gemini-2.0-flash",
        "gemini-3.1-pro"
    };

    private const string AudioPrompt =
        "Bu ses kaydını kelimesi kelimesine, hiçbir kısmını atlamadan ve teknik terimleri koruyarak, düzgün noktalama işaretleriyle tam bir dijital metne dönüştür. Özetleme yapma, sadece tam transkript üret.";

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AudioService> _logger;
    private readonly AppDbContext _context;

    public AudioService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<AudioService> logger,
        AppDbContext context)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _context = context;
    }

    public async Task<NoteDto> CreateNoteFromAudioAsync(
        int userId,
        Stream audioStream,
        string fileName,
        string? title,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioStream);

        if (audioStream.CanSeek)
            audioStream.Position = 0;

        var transcript = await TranscribeAudioAsync(audioStream, fileName, cancellationToken);
        var safeTitle = string.IsNullOrWhiteSpace(title) ? "Sesli Not" : title.Trim();

        // Description artık nvarchar(max); transcription'ı olduğu gibi saklayabiliriz.
        var safeDescription = string.IsNullOrWhiteSpace(transcript)
            ? "(Boş transcription)"
            : transcript.Trim();

        var note = new Note
        {
            Title = safeTitle,
            Description = safeDescription,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.Notes.Add(note);
        await _context.SaveChangesAsync(cancellationToken);

        return new NoteDto
        {
            Id = note.Id,
            Title = note.Title,
            Description = note.Description,
            FilePath = note.FilePath,
            Tags = note.Tags,
            Category = note.Category,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt
        };
    }

    private async Task<string> TranscribeAudioAsync(Stream audioStream, string fileName, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Gemini:ApiKey"] ?? _configuration["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API anahtarı bulunamadı. appsettings.json içinde 'Gemini:ApiKey' değerini ayarlayın.");
        }

        // Basit MIME tayini (uzantıya göre)
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        var mimeType = ext switch
        {
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            ".m4a" => "audio/m4a",
            ".mp3" => "audio/mpeg",
            _ => "audio/webm"
        };

        using var ms = new MemoryStream();
        await audioStream.CopyToAsync(ms, cancellationToken);
        var audioBytes = ms.ToArray();
        if (audioBytes.Length == 0)
        {
            throw new InvalidOperationException("Ses kaydı boş. Lütfen geçerli bir ses dosyası gönderin.");
        }

        var base64 = Convert.ToBase64String(audioBytes);

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
                Console.WriteLine("[Audio Gemini] ListModels: " + name);
                _logger.LogInformation("Audio Gemini ListModels: {Model}", name);
            }

            if (!string.IsNullOrEmpty(flashModelId))
            {
                modelIdsToTry.Add(flashModelId);
                Console.WriteLine("[Audio Gemini] Dinamik seçim (flash): " + flashModelId);
                _logger.LogInformation("Audio Gemini: Dinamik seçim (flash içeren ilk model): {Model}", flashModelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio Gemini: ListModels isteği başarısız, sadece manuel liste kullanılacak.");
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
                        new { text = AudioPrompt },
                        new { inline_data = new { mime_type = mimeType, data = base64 } }
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
                    return ParseGeminiTextResponse(lastResponseBody) ?? string.Empty;

                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"Gemini API hatası ({lastStatusCode}): {lastReasonPhrase}. ResponseBody: {lastResponseBody}");
                }
            }

            _logger.LogWarning("Audio Gemini: {Model} v1beta 404 döndü, v1 ile deneniyor.", modelName);
            using (var contentV1 = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                var responseV1 = await httpClient.PostAsync(urlV1, contentV1, cancellationToken);
                lastResponseBody = await responseV1.Content.ReadAsStringAsync(cancellationToken);
                lastStatusCode = (int)responseV1.StatusCode;
                lastReasonPhrase = responseV1.ReasonPhrase;

                if (responseV1.IsSuccessStatusCode)
                    return ParseGeminiTextResponse(lastResponseBody) ?? string.Empty;

                if (responseV1.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"Gemini API hatası ({lastStatusCode}): {lastReasonPhrase}. ResponseBody: {lastResponseBody}");
                }
            }

            _logger.LogWarning("Audio Gemini: {Model} v1 de 404. Sıradaki modele geçiliyor.", modelName);
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

