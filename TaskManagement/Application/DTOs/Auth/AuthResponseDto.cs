namespace Application.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string IdToken { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; } 
        public string TokenType { get; set; }  = string.Empty;
    }
}
