using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    // Configuration class for AWS Cognito settings
    public class AwsCognitoOptions
    {
       
        public const string AwsCognito = "AWS:Cognito";

        [Required] 
        public string UserPoolId { get; set; } = string.Empty;
        [Required] 
        public string ClientId { get; set; } = string.Empty;
        // TODO: other Cognito related settings 
    }
}