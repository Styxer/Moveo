using Application.DTOs.Auth;

namespace Application.Interfaces
{
    // Interface defining the authentication use cases
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(string username, string password);
        // TODO: Add other auth methods (e.g., SignUp, ConfirmSignUp, RefreshToken)
    }
}