public class TestAuthHandler(
    Microsoft.Extensions.Options.OptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    ISystemClock clock) //TODO:CHeck alternative 
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder, clock)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, "test_user_id"), 
            new Claim(ClaimTypes.Name, "testuser"),
             new Claim("cognito:groups", "Admin"),
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}
