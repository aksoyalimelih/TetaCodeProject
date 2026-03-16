using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TetaCode.Data;
using TetaCode.Service.Dtos;
using TetaCode.Service.Services;

namespace TetaCode.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IAIService _aiService;

    public AIController(AppDbContext context, IAIService aiService)
    {
        _context = context;
        _aiService = aiService;
    }

    [HttpGet("{noteId:int}/summary")]
    public async Task<ActionResult<object>> GetSummary(int noteId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var note = await _context.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId, cancellationToken);

        if (note is null)
            return NotFound();

        var summary = await _aiService.SummarizeNoteAsync(note.Description ?? string.Empty, cancellationToken);
        return Ok(new { summary });
    }

    [HttpGet("{noteId:int}/quiz")]
    public async Task<ActionResult<IReadOnlyList<QuizQuestionDto>>> GetQuiz(int noteId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var note = await _context.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == noteId && n.UserId == userId, cancellationToken);

        if (note is null)
            return NotFound();

        var quiz = await _aiService.GenerateQuizAsync(note.Description ?? string.Empty, cancellationToken);
        return Ok(quiz);
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out userId);
    }
}

