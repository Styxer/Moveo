using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


//Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();


// Add YARP to the services collection
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// --- Add Rate Limiting Services ---
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    // Configure Rate Limiting policies from configuration
    rateLimiterOptions.AddPolicy("fixedWindowPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(), // Partition by user or host
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:FixedWindow:PermitLimit"),
                Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<double>("RateLimiting:FixedWindow:WindowSeconds"))
            }));

    rateLimiterOptions.AddPolicy("slidingWindowPolicy", httpContext =>
       RateLimitPartition.GetSlidingWindowLimiter(
           partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(), // Partition by user or host
           factory: _ => new SlidingWindowRateLimiterOptions
           {
               PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:SlidingWindow:PermitLimit"),
               Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<double>("RateLimiting:SlidingWindow:WindowSeconds")),
               SegmentsPerWindow = builder.Configuration.GetValue<int>("RateLimiting:SlidingWindow:SegmentsPerWindow")
           }));

    rateLimiterOptions.AddPolicy("tokenBucketPolicy", httpContext =>
       RateLimitPartition.GetTokenBucketLimiter(
           partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(), // Partition by user or host
           factory: _ => new TokenBucketRateLimiterOptions
           {
               ReplenishmentPeriod = TimeSpan.FromSeconds(builder.Configuration.GetValue<double>("RateLimiting:TokenBucket:ReplenishmentPeriodSeconds")),
               TokenLimit = builder.Configuration.GetValue<int>("RateLimiting:TokenBucket:TokenLimit"),
               TokensPerPeriod = builder.Configuration.GetValue<int>("RateLimiting:TokenBucket:TokensPerPeriod"),
               AutoReplenishment = builder.Configuration.GetValue<bool>("RateLimiting:TokenBucket:AutoReplenishment")
            }));

// Add a global rate limit that applies if no specific policy is matched
rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:Global:PermitLimit"),
            Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<double>("RateLimiting:Global:WindowSeconds"))
        }));

// Configure the response for rejected requests (e.g., return 429 Too Many Requests)
rateLimiterOptions.RejectionStatusCode = 429;
});

// Add Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole(); // TODO: add more logging

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // TODO: Add development-specific middleware
}

app.UseRateLimiter();
app.MapReverseProxy();

app.Run();
