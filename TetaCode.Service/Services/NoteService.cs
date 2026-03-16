using Microsoft.EntityFrameworkCore;
using TetaCode.Core.Entities;
using TetaCode.Data;
using TetaCode.Service.Dtos;

namespace TetaCode.Service.Services;

public class NoteService : INoteService
{
    private readonly AppDbContext _context;

    public NoteService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<NoteDto> AddNoteFromPdfAsync(int userId, string title, Stream pdfStream, string webRootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var uploadsFolder = Path.Combine(webRootPath, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}.pdf";
        var fullPath = Path.Combine(uploadsFolder, fileName);

        if (pdfStream.CanSeek)
            pdfStream.Position = 0;
        await using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await pdfStream.CopyToAsync(fileStream, cancellationToken);
        }

        var relativeFilePath = Path.Combine("uploads", fileName).Replace("\\", "/");

        var extractedText = PdfExtractionService.ExtractText(pdfStream);
        var description = string.IsNullOrWhiteSpace(extractedText)
            ? "(PDF içeriği okunamadı veya boş.)"
            : extractedText;

        var note = new Note
        {
            Title = title,
            Description = description,
            UserId = userId,
            FilePath = relativeFilePath,
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

    public async Task<NoteDto> AddNoteAsync(int userId, CreateNoteDto dto, string webRootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        string? relativeFilePath = null;

        if (dto.File is not null && dto.File.Length > 0)
        {
            var uploadsFolder = Path.Combine(webRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(dto.File.FileName);
            var fileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await dto.File.CopyToAsync(stream, cancellationToken);

            relativeFilePath = Path.Combine("uploads", fileName).Replace("\\", "/");
        }

        var note = new Note
        {
            Title = dto.Title,
            Description = dto.Description,
            Tags = dto.Tags,
            Category = dto.Category,
            UserId = userId,
            FilePath = relativeFilePath,
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
            Tags = note.Tags,
            Category = note.Category,
            FilePath = note.FilePath,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt
        };
    }

    public async Task<NoteDto> AddNoteWithPdfAsync(int userId, string title, string description, byte[] pdfBytes, string webRootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var uploadsFolder = Path.Combine(webRootPath, "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}.pdf";
        var fullPath = Path.Combine(uploadsFolder, fileName);
        await File.WriteAllBytesAsync(fullPath, pdfBytes, cancellationToken);

        var relativeFilePath = Path.Combine("uploads", fileName).Replace("\\", "/");

        var note = new Note
        {
            Title = title,
            Description = description,
            Tags = null,
            Category = null,
            UserId = userId,
            FilePath = relativeFilePath,
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
            Tags = note.Tags,
            Category = note.Category,
            FilePath = note.FilePath,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt
        };
    }

    public async Task<IReadOnlyList<NoteDto>> GetAllNotesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context
            .Notes
            .Where(n => n.UserId == userId)
            .AsNoTracking()
            .Select(n => new NoteDto
            {
                Id = n.Id,
                Title = n.Title,
                Description = n.Description,
                Tags = n.Tags,
                Category = n.Category,
                FilePath = n.FilePath,
                UserId = n.UserId,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<NoteDto?> UpdateNoteAsync(int userId, int id, UpdateNoteDto dto, string webRootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var note = await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (note is null)
        {
            return null;
        }

        note.Title = dto.Title;
        note.Description = dto.Description;
        note.Tags = dto.Tags;
        note.Category = dto.Category;
        note.UpdatedAt = DateTime.UtcNow;

        if (dto.File is not null && dto.File.Length > 0)
        {
            if (!string.IsNullOrWhiteSpace(note.FilePath))
            {
                var oldPath = Path.Combine(webRootPath, note.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(oldPath))
                {
                    File.Delete(oldPath);
                }
            }

            var uploadsFolder = Path.Combine(webRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            var extension = Path.GetExtension(dto.File.FileName);
            var fileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await dto.File.CopyToAsync(stream, cancellationToken);

            note.FilePath = Path.Combine("uploads", fileName).Replace("\\", "/");
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new NoteDto
        {
            Id = note.Id,
            Title = note.Title,
            Description = note.Description,
            Tags = note.Tags,
            Category = note.Category,
            FilePath = note.FilePath,
            UserId = note.UserId,
            CreatedAt = note.CreatedAt
        };
    }

    public async Task<bool> SoftDeleteAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var note = await _context.Notes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (note is null)
        {
            return false;
        }

        if (!note.IsDeleted)
        {
            note.IsDeleted = true;
            note.DeletedAt = DateTime.UtcNow;
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> HardDeleteAsync(int userId, int id, string webRootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        var note = await _context.Notes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (note is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(note.FilePath))
        {
            var filePath = Path.Combine(webRootPath, note.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        _context.Notes.Remove(note);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<NoteDto>> GetArchivedNotesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notes
            .IgnoreQueryFilters()
            .Where(n => n.IsDeleted && n.UserId == userId)
            .AsNoTracking()
            .Select(n => new NoteDto
            {
                Id = n.Id,
                Title = n.Title,
                Description = n.Description,
                Tags = n.Tags,
                Category = n.Category,
                FilePath = n.FilePath,
                UserId = n.UserId,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RestoreAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var note = await _context.Notes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

        if (note is null)
        {
            return false;
        }

        if (note.IsDeleted)
        {
            note.IsDeleted = false;
            note.DeletedAt = null;
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<IReadOnlyList<NoteDto>> SearchNotesAsync(int userId, string? searchTerm, string? category, CancellationToken cancellationToken = default)
    {
        var query = _context
            .Notes
            .Where(n => n.UserId == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(n => n.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(n =>
                n.Title.Contains(term) ||
                n.Description.Contains(term) ||
                (n.Tags != null && n.Tags.Contains(term)));
        }

        return await query
            .AsNoTracking()
            .Select(n => new NoteDto
            {
                Id = n.Id,
                Title = n.Title,
                Description = n.Description,
                Tags = n.Tags,
                Category = n.Category,
                FilePath = n.FilePath,
                UserId = n.UserId,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<NoteDto?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        return await _context.Notes
            .Where(n => n.Id == id && n.UserId == userId)
            .AsNoTracking()
            .Select(n => new NoteDto
            {
                Id = n.Id,
                Title = n.Title,
                Description = n.Description,
                Tags = n.Tags,
                Category = n.Category,
                FilePath = n.FilePath,
                UserId = n.UserId,
                CreatedAt = n.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}

