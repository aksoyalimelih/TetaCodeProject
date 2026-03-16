using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TetaCode.Service.Dtos;
using TetaCode.Service.Services;

namespace TetaCode.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly INoteService _noteService;
    private readonly IOCRService _ocrService;
    private readonly IWebHostEnvironment _environment;

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".webp" };

    public NotesController(INoteService noteService, IOCRService ocrService, IWebHostEnvironment environment)
    {
        _noteService = noteService;
        _ocrService = ocrService;
        _environment = environment;
    }

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create([FromForm] CreateNoteDto dto, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var webRootPath = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var created = await _noteService.AddNoteAsync(userId, dto, webRootPath, cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<NoteDto>> Update(int id, [FromForm] UpdateNoteDto dto, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var webRootPath = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var updated = await _noteService.UpdateNoteAsync(userId, id, dto, webRootPath, cancellationToken);

        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> GetAll(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var search = HttpContext.Request.Query["search"].ToString();
        var category = HttpContext.Request.Query["category"].ToString();

        var notes = await _noteService.SearchNotesAsync(userId, search, category, cancellationToken);
        return Ok(notes);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoteDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var note = await _noteService.GetByIdAsync(userId, id, cancellationToken);
        if (note is null)
            return NotFound();

        return Ok(note);
    }

    [HttpGet("archive")]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> GetArchive(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var search = HttpContext.Request.Query["search"].ToString();
        var category = HttpContext.Request.Query["category"].ToString();

        var notes = await _noteService.SearchArchivedNotesAsync(userId, search, category, cancellationToken);
        return Ok(notes);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> SoftDelete(int id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await _noteService.SoftDeleteAsync(userId, id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id}/hard")]
    public async Task<IActionResult> HardDelete(int id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var webRootPath = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var result = await _noteService.HardDeleteAsync(userId, id, webRootPath, cancellationToken);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("analyze-image")]
    public async Task<ActionResult<AnalyzeImageResponse>> AnalyzeImage(
        [FromForm] IFormFile? file,
        [FromForm] string? language,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out _))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("Lütfen bir görsel dosyası seçin.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedImageExtensions.Contains(ext))
        {
            return BadRequest("Geçersiz dosya türü. İzin verilenler: png, jpg, jpeg, bmp, gif, tiff, webp.");
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest("Dosya boyutu en fazla 10MB olabilir.");
        }

        var lang = string.IsNullOrWhiteSpace(language) ? "eng+tur" : language.Trim().ToLowerInvariant();

        await using var stream = file.OpenReadStream();
        var text = await _ocrService.ExtractTextFromImageAsync(stream, lang, cancellationToken);
        return Ok(new AnalyzeImageResponse { Text = text ?? string.Empty });
    }

    [HttpPost("convert-to-pdf")]
    public async Task<ActionResult<NoteDto>> ConvertToPdf([FromBody] ConvertToPdfDto dto, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest("Başlık zorunludur.");
        }

        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var pdfBytes = await _ocrService.GeneratePdfFromTextAsync(dto.Title, dto.Content ?? string.Empty, cancellationToken);
        // Kartlarda ve detayda görünsün diye not metnini OCR içeriği (Content) yapıyoruz; Description opsiyonel ek açıklama
        var description = !string.IsNullOrWhiteSpace(dto.Content)
            ? (dto.Content ?? string.Empty).Trim()
            : (dto.Description ?? string.Empty).Trim();
        var category = !string.IsNullOrWhiteSpace(dto.Category) ? dto.Category.Trim() : null;
        var created = await _noteService.AddNoteWithPdfAsync(userId, dto.Title, description, pdfBytes, webRootPath, category, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    [HttpPost("upload-pdf")]
    public async Task<ActionResult<NoteDto>> UploadPdf([FromForm] IFormFile? file, [FromForm] string? title, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("Lütfen geçerli bir PDF dosyası seçin.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Sadece PDF dosyalarına izin verilmektedir.");
        }

        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var safeTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(file.FileName)
            : title.Trim();

        await using var stream = file.OpenReadStream();
        var created = await _noteService.AddNoteFromPdfAsync(userId, safeTitle, stream, webRootPath, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
    }

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await _noteService.RestoreAsync(userId, id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("{id:int}/download-pdf")]
    public async Task<IActionResult> DownloadPdf(int id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var note = await _noteService.GetByIdAsync(userId, id, cancellationToken);
        if (note is null)
            return NotFound();

        var bytes = await _ocrService.GeneratePdfFromTextAsync(note.Title, note.Description, cancellationToken);
        var fileName = $"{Slugify(note.Title)}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    [HttpGet("{id:int}/download-docx")]
    public async Task<IActionResult> DownloadDocx(int id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var note = await _noteService.GetByIdAsync(userId, id, cancellationToken);
        if (note is null)
            return NotFound();

        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            var body = mainPart.Document.Body!;

            // Başlık
            var titleParagraph = new Paragraph(
                new Run(
                    new Text(note.Title ?? string.Empty)))
            {
                ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId { Val = "Heading1" })
            };
            body.AppendChild(titleParagraph);

            // Tarih
            body.AppendChild(new Paragraph(
                new Run(
                    new Text($"Oluşturulma: {note.CreatedAt.ToLocalTime():g}"))));

            body.AppendChild(new Paragraph(new Run(new Text(string.Empty))));

            // İçerik
            var lines = (note.Description ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
            {
                body.AppendChild(new Paragraph(new Run(new Text(line))));
            }

            mainPart.Document.Save();
        }

        var fileName = $"{Slugify(note.Title)}.docx";
        var bytes = ms.ToArray();
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "not";

        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value
            .Where(ch => !invalidChars.Contains(ch))
            .ToArray());

        cleaned = cleaned.Trim();
        if (cleaned.Length == 0)
            cleaned = "not";

        cleaned = cleaned.Replace(' ', '-');
        return cleaned.Length > 60 ? cleaned[..60] : cleaned;
    }
}

