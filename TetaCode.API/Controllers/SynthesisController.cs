using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TetaCode.Service.Dtos;
using TetaCode.Service.Services;

namespace TetaCode.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SynthesisController : ControllerBase
{
    private readonly ISynthesisService _synthesisService;

    public SynthesisController(ISynthesisService synthesisService)
    {
        _synthesisService = synthesisService;
    }

    public class SynthesisRequest
    {
        public List<int> NoteIds { get; set; } = new();
        public string? Title { get; set; }
    }

    [HttpPost("combine")]
    public async Task<ActionResult<NoteDto>> Combine([FromBody] SynthesisRequest request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (request.NoteIds == null || request.NoteIds.Count < 2)
        {
            return BadRequest("En az iki not seçmeniz gerekiyor.");
        }

        var note = await _synthesisService.SynthesizeNotesAsync(userId, request.NoteIds, request.Title, cancellationToken);
        return CreatedAtAction("GetById", "Notes", new { id = note.Id }, note);
    }
}

