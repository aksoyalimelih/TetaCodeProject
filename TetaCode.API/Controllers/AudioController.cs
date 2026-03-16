using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TetaCode.Service.Dtos;
using TetaCode.Service.Services;

namespace TetaCode.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AudioController : ControllerBase
{
    private readonly IAudioService _audioService;

    public AudioController(IAudioService audioService)
    {
        _audioService = audioService;
    }

    [HttpPost("note-from-audio")]
    public async Task<ActionResult<NoteDto>> CreateNoteFromAudio(
        [FromForm] IFormFile? file,
        [FromForm] string? title,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("Lütfen geçerli bir ses dosyası gönderin.");
        }

        await using var stream = file.OpenReadStream();
        var note = await _audioService.CreateNoteFromAudioAsync(
            userId,
            stream,
            file.FileName,
            title,
            cancellationToken);

        return CreatedAtAction("GetById", "Notes", new { id = note.Id }, note);
    }
}

