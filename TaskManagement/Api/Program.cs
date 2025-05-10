using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens; 
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Amazon.CognitoIdentityProvider; 
using TaskManagement.Infrastructure.Services;
using System.Text.Json;
using Application.Commands.Projects;
using Application.Interfaces;
using Application.Mapping;
using Application.Options; 
using FluentValidation; 
using FluentValidation.AspNetCore;
using Infrastructure.Handlers.Projects;
using MediatR; 
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Add Configuration Options and Validation ---
builder.Services.AddOptions<AwsCognitoOptions>()
    .Bind(builder.Configuration.GetSection(AwsCognitoOptions.AwsCognito))
    .ValidateDataAnnotations();

builder.Services.AddOptions<AwsCognitoOptions>()
    .Bind(builder.Configuration.GetSection(AwsCognitoOptions.AwsCognito))
    .ValidateDataAnnotations();

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.RabbitMq))
    .ValidateDataAnnotations();

builder.Services.AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection(RedisOptions.Redis))
    .ValidateDataAnnotations();


// --- Services ---


builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>();
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());

// Add database context (Infrastructure concern)
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(dbConnectionString, npgsqlOptions =>
    {
        // Configure connection resilience (retries) for transient errors
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
    });
});

// --- Add Repositories using Scrutor ---
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(Repository<>).Assembly) // Scan the Infrastructure assembly
    .AddClasses(classes => classes.AssignableTo(typeof(IRepository<>))) // Find classes implementing IRepository<>
    .AsMatchingInterface() // Register them against their matching interface (e.g., Repository<T> as IRepository<T>)
    .WithScopedLifetime()); // Register as Scoped

// Manual registration for specific services if needed (like AuthService due to constructor parameters)
builder.Services.AddScoped<IAuthService>(provider =>
{
    var cognitoClient = provider.GetRequiredService<IAmazonCognitoIdentityProvider>();
    var logger = provider.GetRequiredService<ILogger<AuthService>>();
    // Inject IOptions<AwsCognitoOptions> instead of reading directly from IConfiguration
    var cognitoOptions = provider.GetRequiredService<IOptions<AwsCognitoOptions>>();
    return new Infrastructure.Services.AuthService(cognitoClient, logger, cognitoOptions);
});

// --- Add MediatR Handlers using Scrutor ---
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(CreateProjectCommandHandler).Assembly) // Scan the Infrastructure assembly for handlers
    .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)) // Find classes implementing IRequestHandler<,>
                                .OrAssignableTo(typeof(IRequestHandler<>))) // Find classes implementing IRequestHandler<>
    .AsImplementedInterfaces() // Register them against all implemented interfaces (IRequestHandler<,> or IRequestHandler<>)
    .WithScopedLifetime()); // Register as Scoped

// --- Add AutoMapper ---
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly); // Scan the Application assembly for AutoMapper profiles


// --- Add FluentValidation Validators using Scrutor ---
builder.Services.AddFluentValidationAutoValidation() // Enables automatic validation (integrates with MVC)
                .AddFluentValidationClientsideAdapters(); // Optional: Add client-side adapters
// Register validators from the Application assembly using Scrutor
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(CreateProjectRequestDtoValidator).Assembly) // Scan the Application assembly for validators
    .AddClasses(classes => classes.AssignableTo(typeof(IValidator<>))) // Find classes implementing IValidator<>
    .AsImplementedInterfaces() // Register them against all implemented interfaces
    .WithTransientLifetime()); // Validators are typically Transient


// --- Add MediatR ---
builder.Services.AddMediatR(cfg =>
{
    // Scan the Application assembly for commands/queries (needed for MediatR to know about them)
    cfg.RegisterServicesFromAssemblyContaining<CreateProjectCommand>();

    // Register MediatR Pipeline Behaviors in the order they should execute
    // Order matters: Logging -> Validation -> Transaction -> Handler
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>)); // Add Logging Behavior
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)); // Add Validation Behavior
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>)); // Add Transaction Behavior

    // Note: Handlers are registered separately using Scrutor above
});


// --- Add MassTransit and RabbitMQ with Outbox ---
builder.Services.AddMassTransit(x =>
{
    // Add consumers from the Infrastructure assembly using Scrutor
    x.AddConsumersFromNamespaceContaining<ProjectCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        // Inject IOptions<RabbitMqOptions> instead of reading directly from IConfiguration
        var rabbitMqOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        cfg.Host(rabbitMqOptions.Host, h =>
        {
            h.Username(rabbitMqOptions.Username);
            h.Password(rabbitMqOptions.Password);
        });

        cfg.ConfigureEndpoints(context);
        cfg.UseNewtonsoftJsonSerializer();
        cfg.UseNewtonsoftJsonDeserializer();
    });

    // Configure the Entity Framework Core Outbox
    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.QueryLimit = 10;
        o.QueryDelay = TimeSpan.FromSeconds(10);
        o.InboxCheckInterval = TimeSpan.FromSeconds(10);
        o.OutboxCleanupInterval = TimeSpan.FromMinutes(1);
        o.TableCleanupQueryDelay = TimeSpan.FromMinutes(5);
        o.UseIsolationLevel(System.Transactions.IsolationLevel.ReadCommitted);
        // o.UseBusOutbox(); // Use this if you want the Outbox to use the bus's transaction instead of the DB transaction (less common with EF Core)
    });
});

// --- Add Distributed Cache (Redis) ---
builder.Services.AddStackExchangeRedisCache(options =>
{
    // Inject IOptions<RedisOptions> to get Redis configuration
    var redisOptions = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<RedisOptions>>().Value;
    options.Configuration = redisOptions.Configuration;
    // Optional: options.InstanceName = redisOptions.InstanceName;
});


// Add Controllers (API Layer)
builder.Services.AddControllers();

// Add Swagger/OpenAPI for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Authentication (AWS Cognito JWT) --- (API/Infrastructure concern)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    // Inject IOptions<AwsCognitoOptions> to get UserPoolId for Authority and ValidIssuer
    var cognitoOptions = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<AwsCognitoOptions>>().Value;

    // Configure JWT validation for AWS Cognito
    options.Authority = $"https://cognito-idp.{builder.Configuration["AWS:Region"]}.amazonaws.com/{cognitoOptions.UserPoolId}";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateAudience = false, // Cognito JWTs don't have a standard 'aud' claim for the client app
        ValidateLifetime = true,
        ValidIssuer = $"https://cognito-idp.{builder.Configuration["AWS:Region"]}.amazonaws.com/{cognitoOptions.UserPoolId}",
        // You might need to configure the Audience if you set up an App Client with a specific audience
        // ValidAudience = cognitoOptions.ClientId, // Or a different audience if configured
        ClockSkew = TimeSpan.Zero // No clock skew tolerance
    };

    // Optional: Configure event handlers for logging/debugging
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "Authentication failed.");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            // Log claims or user info if needed
            // logger.LogInformation("Token validated for user: {Name}", context.Principal.Identity.Name);
            return Task.CompletedTask;
        }
    };
});

// --- Authorization (Policies if needed, otherwise [Authorize(Roles="Admin")] is enough) --- (API/Application concern)
builder.Services.AddAuthorization(options =>
{
    // Example policy if needed, but [Authorize(Roles="Admin")] is simpler for this case
    // options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    // options.AddPolicy("OwnerOrAdmin", policy => policy.RequireAssertion(context =>
    // {
    //     // Custom logic to check if user is owner OR admin
    //     // This is more complex and might be better handled in services
    //     return false; // Placeholder
    // }));
});

// --- Health Checks ---
builder.Services.AddHealthChecks()
    // Add a health check for the PostgreSQL database
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"),
               name: "PostgreSQL Database", // Name for the health check
               failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, // Degraded if fails
               tags: new[] { "db", "ready" }) // Tags for filtering checks
                                              // Add a health check for the RabbitMQ bus
    .AddRabbitMQ(rabbitMqConnectionString: $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}",
                 name: "RabbitMQ Bus",
                 failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                 tags: new[] { "messaging", "ready" })
    // Add a health check for the Redis cache
    .AddRedis(redisConnectionString: builder.Configuration["Redis:Configuration"],
              name: "Redis Cache",
              failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
              tags: new[] { "cache", "ready" });


// Add Logging (default ASP.NET Core logging is configured via appsettings) (Infrastructure/Cross-cutting concern)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(); // Log to console

// --- AutoMapper Profile --- (Application/Cross-cutting concern)
// This profile should ideally live in the Application layer
// It's defined here for completeness, but place it in TaskManagement.Application
/*
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<CreateProjectRequestDto, Project>();
        CreateMap<UpdateProjectRequestDto, Project>();
        CreateMap<Project, ProjectDto>();

        CreateMap<CreateTaskRequestDto, Task>();
        CreateMap<UpdateTaskRequestDto, Task>();
        CreateMap<Task, TaskDto>();
    }
}
*/


// --- App Building and Middleware Configuration --- (API Layer)
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Shows detailed errors in dev
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Use custom error handling middleware in production
    app.UseMiddleware<ErrorHandlingMiddleware>();
    // app.UseExceptionHandler("/Error"); // Alternative built-in handler
    app.UseHsts(); // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
}

// app.UseHttpsRedirection(); // Redirect HTTP to HTTPS (recommended in production)

app.UseRouting(); // Needed for endpoint routing

app.UseAuthentication(); // Must be before UseAuthorization
app.UseAuthorization();


// --- Health Check Endpoints ---
// Basic health check endpoint
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    // Optional: Configure a custom response writer for more detailed output
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await context.Response.WriteAsync(json);
    }
});

// Health check endpoint specifically for readiness probes (e.g., checks DB ready)
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready"), // Only include checks tagged with "ready"
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await context.Response.WriteAsync(json);
    }
});


app.MapControllers(); // Maps controller routes

// Optional: Apply database migrations on startup (careful with this in production)
// using (var scope = app.Services.CreateScope())
// {
//     var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//     dbContext.Database.Migrate(); // Run EF Core migrations, including Outbox tables
// }

app.Run();
