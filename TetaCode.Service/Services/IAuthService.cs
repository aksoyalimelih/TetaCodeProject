using TetaCode.Service.Dtos.Auth;

namespace TetaCode.Service.Services;

public interface IAuthService
{
    Task<bool> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default);
    Task<AuthResponseDto?> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
}

