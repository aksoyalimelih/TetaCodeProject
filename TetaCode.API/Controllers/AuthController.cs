using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TetaCode.Service.Dtos.Auth;
using TetaCode.Service.Services;

namespace TetaCode.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken cancellationToken)
    {
        var success = await _authService.RegisterAsync(dto, cancellationToken);
        if (!success)
        {
            return BadRequest("Bu e-posta ile zaten bir kullanıcı kayıtlı.");
        }

        return Ok();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(dto, cancellationToken);
        if (result is null)
        {
            return Unauthorized("Geçersiz e-posta veya şifre.");
        }

        return Ok(result);
    }
}

