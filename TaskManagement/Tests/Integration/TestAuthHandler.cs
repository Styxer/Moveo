public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(Microsoft.Extensions.Options.OptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Create a test user identity and principal
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, "test_user_id"), // Simulate Cognito sub claim
            new Claim(ClaimTypes.Name, "testuser"),
            // Add roles or other claims as needed for testing RBAC
            // new Claim("cognito:groups", "Admin"),
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}
}