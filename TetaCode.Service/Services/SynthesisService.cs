using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TetaCode.Core.Entities;
using TetaCode.Data;
using TetaCode.Service.Dtos;

namespace TetaCode.Service.Services;

public class SynthesisService : ISynthesisService
{
    private const string BaseUrlV1Beta = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string BaseUrlV1 = "https://generativelanguage.googleapis.com/v1/models";

    private static readonly string[] ManualFallbackModels =
    {
        "gemini-2.5-flash",
        "gemini-flash-latest",
        "gemini-2.0-flash",
        "gemini-2.0-flash-lite"
    };

    private const string SynthesisPrompt =
        "Sana bir dersle alakalı farklı kaynaklardan (PDF, ses kaydı, el yazısı) gelen notlar gönderiyorum. " +
        "Bu notların hepsini analiz et. Tekrarlanan bilgileri temizle. Konuları mantıklı bir sıraya koy ve " +
        "bana bu dersin en kapsamlı, profesyonel çalışma notunu (Study Guide) oluştur. Başlıklar, önemli kavramlar " +
        "ve özet şeklinde düzenle.";

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SynthesisService> _logger;

    public SynthesisService(
        AppDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<SynthesisService> logger)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<NoteDto> SynthesizeNotesAsync(int userId, IReadOnlyList<int> noteIds, string? title, CancellationToken cancellationToken = default)
    {
        if (noteIds == null || noteIds.Count < 2)
            throw new InvalidOperationException("En az iki not seçilmelidir.");

        var notes = await _context.Notes
            .Where(n => !n.IsDeleted && n.UserId == userId && noteIds.Contains(n.Id))
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        if (notes.Count < 2)
            throw new InvalidOperationException("Seçilen notlardan kullanıcıya ait en az iki tane bulunamadı.");

        var combinedBuilder = new StringBuilder();
        foreach (var note in notes)
        {
            combinedBuilder.AppendLine($"# {note.Title}");
            combinedBuilder.AppendLine(note.Description);
            combinedBuilder.AppendLine();
        }

        var combinedContent = combinedBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(combinedContent))
            throw new InvalidOperationException("Seçilen notların içeriği boş.");

        var apiText = await CallGeminiForTextAsync(SynthesisPrompt, combinedContent, cancellationToken);
        var finalText = string.IsNullOrWhiteSpace(apiText) ? combinedContent : apiText.Trim();

        var finalTitle = !string.IsNullOrWhiteSpace(title)
            ? title.Trim()
            : $"Birleştirilmiş Not: {notes.First().Title}";

        var newNote = new Note
        {
            Title = finalTitle,
            Description = finalText,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _context.Notes.Add(newNote);
        await _context.SaveChangesAsync(cancellationToken);

        return new NoteDto
        {
            Id = newNote.Id,
            Title = newNote.Title,
            Description = newNote.Description,
            FilePath = newNote.FilePath,
            Tags = newNote.Tags,
            Category = newNote.Category,
            UserId = newNote.UserId,
            CreatedAt = newNote.CreatedAt
        };
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
                Console.WriteLine("[Synthesis Gemini] ListModels: " + name);
                _logger.LogInformation("Synthesis Gemini ListModels: {Model}", name);
            }

            if (!string.IsNullOrEmpty(flashModelId))
            {
                modelIdsToTry.Add(flashModelId);
                Console.WriteLine("[Synthesis Gemini] Dinamik seçim (flash): " + flashModelId);
                _logger.LogInformation("Synthesis Gemini: Dinamik seçim (flash içeren ilk model): {Model}", flashModelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Synthesis Gemini: ListModels isteği başarısız, sadece manuel liste kullanılacak.");
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

            _logger.LogWarning("Synthesis Gemini: {Model} v1beta 404 döndü, v1 ile deneniyor.", modelName);
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

            _logger.LogWarning("Synthesis Gemini: {Model} v1 de 404. Sıradaki modele geçiliyor.", modelName);
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

