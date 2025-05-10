using Amazon.CognitoIdentityProvider.Model;
using Amazon.CognitoIdentityProvider;
using Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Application.Interfaces;

namespace TaskManagement.Infrastructure.Services
{
    // This is the infrastructure-specific implementation of the authentication service
    public class AuthService : IAuthService
    {
        private readonly IAmazonCognitoIdentityProvider _cognitoClient; // Infrastructure dependency
        private readonly ILogger<AuthService> _logger;
        // Inject IOptions for configuration
        private readonly AwsCognitoOptions _cognitoOptions;
        // Define a retry policy for external service calls (Cognito)
        private readonly AsyncRetryPolicy _cognitoRetryPolicy;


        public AuthService(IAmazonCognitoIdentityProvider cognitoClient,
                           ILogger<AuthService> logger,
                           IOptions<AwsCognitoOptions> cognitoOptions) // Inject IOptions
        {
            _cognitoClient = cognitoClient;
            _logger = logger;
            _cognitoOptions = cognitoOptions.Value; // Get the configuration values

            // Define a retry policy for transient Cognito errors
            _cognitoRetryPolicy = Policy
                .Handle<AmazonCognitoIdentityProviderException>(ex => ex.StatusCode == System.Net.HttpStatusCode.InternalServerError || // Example transient errors
                                                                     ex.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                                                                     ex.Message.Contains("throttling", StringComparison.OrdinalIgnoreCase)) // Handle throttling
                .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100), // Exponential backoff with jitter
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(exception,
                           "Cognito Retry {RetryAttempt} encountered transient error. Waiting {TimeSpan} before retrying. Context: {Context}",
                           retryAttempt, timeSpan, context.OperationKey);
                    });
        }

        public async Task<AuthResponseDto> LoginAsync(string username, string password)
        {
            try
            {
                // Wrap the Cognito API call with the retry policy
                var authResponse = await _cognitoRetryPolicy.ExecuteAsync(async (ctx) =>
                {
                    _logger.LogTrace("Executing Cognito InitiateAuth for user {Username}", username);
                    var authRequest = new InitiateAuthRequest
                    {
                        AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                        ClientId = _cognitoOptions.ClientId, // Use options
                        AuthParameters = new Dictionary<string, string>
                        {
                            { "USERNAME", username },
                            { "PASSWORD", password }
                        }
                    };
                    // Add context for logging in the retry policy
                    ctx["OperationKey"] = $"CognitoLogin:{username}";
                    return await _cognitoClient.InitiateAuthAsync(authRequest, ctx.CancellationToken);
                }, Policy.NoOpAsync().CreateExecutionContext()); // Pass a simple context


                if (authResponse.AuthenticationResult != null)
                {
                    return new AuthResponseDto
                    {
                        IdToken = authResponse.AuthenticationResult.IdToken,
                        AccessToken = authResponse.AuthenticationResult.AccessToken,
                        RefreshToken = authResponse.AuthenticationResult.RefreshToken,
                        ExpiresIn = authResponse.AuthenticationResult.ExpiresIn,
                        TokenType = authResponse.AuthenticationResult.TokenType
                    };
                }
                // Handle cases like NEW_PASSWORD_REQUIRED etc. if implementing full flow
                throw new UnauthorizedAccessException("Authentication failed.");
            }
            catch (UserNotFoundException ex)
            {
                _logger.LogWarning(ex, "Login failed: User not found for username {Username}", username);
                throw new UnauthorizedAccessException("Invalid credentials.");
            }
            catch (NotAuthorizedException ex)
            {
                _logger.LogWarning(ex, "Login failed: Not authorized for username {Username}", username);
                throw new UnauthorizedAccessException("Invalid credentials.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login for username {Username}", username);
                throw new ApplicationException("An error occurred during authentication.");
            }
        }
    }
}