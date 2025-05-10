using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens; 
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Amazon.CognitoIdentityProvider;
using System.Text.Json;
using Api.Middleware;
using Application.Behaviors;
using Application.Commands.Projects;
using Application.Interfaces;
using Application.Mapping;
using Application.Options;
using Application.Validators.Projects;
using FluentValidation; 
using FluentValidation.AspNetCore;
using Infrastructure.Consumers;
using Infrastructure.Data;
using Infrastructure.Handlers.Projects;
using Infrastructure.Repository;
using Infrastructure.Services;
using MediatR; 
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TaskManagement.Application.MediatR;
using TaskManagement.Infrastructure.Services;

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
    .FromAssemblies(typeof(Repository<>).Assembly) 
    .AddClasses(classes => classes.AssignableTo(typeof(IRepository<>))) 
    .AsMatchingInterface() 
    .WithScopedLifetime()); 

// Manual registration for specific services if needed (like AuthService due to constructor parameters)
builder.Services.AddScoped<IAuthService>(provider =>
{
    var cognitoClient = provider.GetRequiredService<IAmazonCognitoIdentityProvider>();
    var logger = provider.GetRequiredService<ILogger<AuthService>>();
 
     var cognitoOptions = provider.GetRequiredService<IOptions<AwsCognitoOptions>>();
    return new AuthService(cognitoClient, logger, cognitoOptions);
});

// --- Add MediatR Handlers using Scrutor ---
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(CreateProjectCommandHandler).Assembly) 
    .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)))
    .AsImplementedInterfaces() // Register them against all implemented interfaces (IRequestHandler<,> or IRequestHandler<>)
    .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<>)))
    .AsImplementedInterfaces() 
    .WithScopedLifetime()); 

// --- Add AutoMapper ---
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly); 


// --- Add FluentValidation Validators using Scrutor ---
builder.Services.AddFluentValidationAutoValidation() 
                .AddFluentValidationClientsideAdapters(); 

// Register validators from the Application assembly using Scrutor
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(CreateProjectRequestDtoValidator).Assembly) 
    .AddClasses(classes => classes.AssignableTo(typeof(IValidator<>)))
    .AsImplementedInterfaces() 
    .WithTransientLifetime()); 


// --- Add MediatR ---
builder.Services.AddMediatR(cfg =>
{
    // Scan the Application assembly for commands/queries (needed for MediatR to know about them)
    cfg.RegisterServicesFromAssemblyContaining<CreateProjectCommand>();

    // Register MediatR Pipeline Behaviors in the order they should execute
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)); 
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>)); 
});


// --- Add MassTransit and RabbitMQ with Outbox ---
builder.Services.AddMassTransit(x =>
{
    // Add consumers from the Infrastructure assembly using Scrutor
    x.AddConsumersFromNamespaceContaining<ProjectCreatedConsumer>();
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
        o.QueryMessageLimit = 10;
        o.QueryDelay = TimeSpan.FromSeconds(10);
        o.QueryTimeout = TimeSpan.FromSeconds(10);
        o.IsolationLevel = IsolationLevel.ReadCommitted;
    });
});

// --- Add Distributed Cache (Redis) ---
builder.Services.AddStackExchangeRedisCache(options =>
{
   
    var redisOptions = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<RedisOptions>>().Value;
    options.Configuration = redisOptions?.Configuration;
   
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

    var cognitoOptions = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<AwsCognitoOptions>>().Value;

    // Configure JWT validation for AWS Cognito
    options.Authority = $"https://cognito-idp.{builder.Configuration["AWS:Region"]}.amazonaws.com/{cognitoOptions.UserPoolId}";
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateAudience = false, 
        ValidateLifetime = true,
        ValidIssuer = $"https://cognito-idp.{builder.Configuration["AWS:Region"]}.amazonaws.com/{cognitoOptions.UserPoolId}",

        ClockSkew = TimeSpan.Zero 
    };

    //Configure event handlers for logging/debugging
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
           
             logger.LogInformation("Token validated for user: {Name}", context.Principal.Identity.Name);
            return Task.CompletedTask;
        }
    };
});

// --- Authorization (Policies if needed, otherwise [Authorize(Roles="Admin")] is enough) --- (API/Application concern)
builder.Services.AddAuthorization(options =>
{
    // Example policy if needed, but [Authorize(Roles="Admin")] is simpler for this case
     options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
     options.AddPolicy("OwnerOrAdmin", policy => policy.RequireAssertion(context => false)); //TODO: remove place holdedr
});

// --- Health Checks ---
builder.Services.AddHealthChecks()
    // Add a health check for the PostgreSQL database
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
               name: "PostgreSQL Database", // Name for the health check
               failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, 
               tags: ["db", "ready"])
       // Add a health check for the RabbitMQ bus

    .AddRabbitMQ()
    // Add a health check for the Redis cache
    .AddRedis(redisConnectionString: builder.Configuration["Redis:Configuration"] ?? string.Empty,
              name: "Redis Cache",
              failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
              tags: ["cache", "ready"]);


// Add Logging 
builder.Logging.ClearProviders();
builder.Logging.AddConsole(); 


// --- App Building and Middleware Configuration 
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
    app.UseHsts();
    app.UseHttpsRedirection();
}

  

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


// --- Health Check Endpoints ---
// Basic health check endpoint
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await context.Response.WriteAsync(json);
    }
});


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



app.Run();
