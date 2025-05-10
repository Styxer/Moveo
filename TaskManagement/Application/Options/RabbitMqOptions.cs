using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    // Configuration class for RabbitMQ settings
    public class RabbitMqOptions
    {
        public const string RabbitMq = "RabbitMQ";

        [Required] 
        public string Host { get; set; }
        [Required] 
        public string Username { get; set; } = string.Empty;

        [Required] 
        public string Password { get; set; } = string.Empty;
        // TODO other RabbitMQ related settings here -
    }
}