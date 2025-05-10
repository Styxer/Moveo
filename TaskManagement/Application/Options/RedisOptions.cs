using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    // Configuration class for Redis settings
    public class RedisOptions
    {
        public const string Redis = "Redis";

        [Required]
        public string Configuration { get; set; } = string.Empty;
       
         public string InstanceName { get; set; } = string.Empty;
    }
}