using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Application.DTOs.Auth;
using Application.Interfaces;
using Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Infrastructure.Services {
  
    public class AuthService : IAuthService
    {
        private readonly IAmazonCognitoIdentityProvider _cognitoClient; 
        private readonly ILogger<AuthService> _logger;
        private readonly AwsCognitoOptions _cognitoOptions;
        private readonly AsyncRetryPolicy _cognitoRetryPolicy;

        public AuthService(IAmazonCognitoIdentityProvider cognitoClient,
                           ILogger<AuthService> logger,
                           IOptions<AwsCognitoOptions> cognitoOptions) 
        {
            _cognitoClient = cognitoClient;
            _logger = logger;
            _cognitoOptions = cognitoOptions.Value; 

       
            _cognitoRetryPolicy = Policy
                .Handle<AmazonCognitoIdentityProviderException>(ex => ex.StatusCode == System.Net.HttpStatusCode.InternalServerError || 
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
               
                var authResponse = await _cognitoRetryPolicy.ExecuteAsync(async () =>
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

                    return await _cognitoClient.InitiateAuthAsync(authRequest);
                });


                if (authResponse.AuthenticationResult != null)
                {
                    return new AuthResponseDto
                    {
                        IdToken = authResponse.AuthenticationResult.IdToken,
                        AccessToken = authResponse.AuthenticationResult.AccessToken,
                        RefreshToken = authResponse.AuthenticationResult.RefreshToken,
                        ExpiresIn = authResponse.AuthenticationResult.ExpiresIn ?? int.MaxValue,
                        TokenType = authResponse.AuthenticationResult.TokenType
                    };
                }
              
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