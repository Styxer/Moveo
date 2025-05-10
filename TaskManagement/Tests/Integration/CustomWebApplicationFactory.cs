using Microsoft.Extensions.Logging;
using System;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;


namespace TaskManagement.Tests
{
    // Custom WebApplicationFactory to configure the test environment
    // Implements IAsyncLifetime to manage the container lifecycle
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        // Testcontainer for PostgreSQL
        private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15") 
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("mypassword")
            .Build();

        // Testcontainer for Redis
        private readonly RedisContainer _redisContainer = new RedisBuilder()
            .WithImage("redis:latest") // Use the latest Redis image
            .Build();


        // Respawn Checkpoint for database resetting
        private Respawner _respawner;

        // Implement IAsyncLifetime to manage the container lifecycle and Respawn setup
        public async Task InitializeAsync()
        {
            // Start the containers concurrently
            await Task.WhenAll(_dbContainer.StartAsync(), _redisContainer.StartAsync());

            // Apply migrations after starting the DB container
            using (var scope = Services.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var dbContext = scopedServices.GetRequiredService<AppDbContext>();
                var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

                try
                {
                    logger.LogInformation("Applying migrations for integration tests...");
                    // Ensure the database exists and apply any pending migrations
                    dbContext.Database.Migrate();
                    logger.LogInformation("Migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred applying migrations for integration tests. Error: {Message}", ex.Message);
                    throw; // Re-throw to fail the test setup
                }
            }

            // Initialize Respawn with the DB container's connection string
            _respawner = await Respawner.CreateAsync(_dbContainer.GetConnectionString(), new RespawnerOptions
            {
                // Configure tables to ignore if needed (e.g., MassTransit Outbox tables)
                // MassTransit Outbox tables are typically: InboxState, OutboxMessage, OutboxState
                TablesToIgnore = new Respawn.Graph.Table[] { "__EFMigrationsHistory", "InboxState", "OutboxMessage", "OutboxState" },
                DbAdapter = Respawn.DbAdapter.Postgres // Specify the database adapter
            });
        }

        // Dispose the containers after all tests using this factory are done
        public new async Task DisposeAsync()
        {
            await Task.WhenAll(_dbContainer.DisposeAsync(), _redisContainer.DisposeAsync());
        }

        // Override ConfigureWebHost to replace services for testing
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove the existing DbContext registration
                services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
                services.RemoveAll(typeof(AppDbContext));

                // Register AppDbContext using the Testcontainers database connection string
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseNpgsql(_dbContainer.GetConnectionString());
                });

                // --- Mock AWS Cognito Authentication ---
                // Replace the real JWT Bearer authentication with a test scheme
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                });

                services.AddAuthentication("TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });

                // In a real scenario, you might mock the IAmazonCognitoIdentityProvider
                // services.RemoveAll(typeof(IAmazonCognitoIdentityProvider));
                // services.AddSingleton(Mock.Of<IAmazonCognitoIdentityProvider>());
                // Or configure it to use a localstack container if needed.
                // For this test, we are bypassing Cognito auth entirely using TestAuthHandler.

                // --- Configure Distributed Cache (Redis) for tests ---
                // Remove the real Redis cache configuration
                services.RemoveAll(typeof(IDistributedCache));
                services.RemoveAll(typeof(IConfigureOptions<RedisCacheOptions>));

                // Register StackExchangeRedisCache using the Testcontainers Redis connection string
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = _redisContainer.GetConnectionString();
                    // Optional: options.InstanceName = "TestCacheInstance";
                });


                // --- Configure AutoMapper (if still used elsewhere) ---
                // Ensure AutoMapper is configured correctly for the test host
                // services.AddAutoMapper(typeof(TaskManagement.Application.DTOs.Projects.ProjectDto).Assembly);

                // --- Configure Mapperly ---
                // Register the generated Mapperly mapper interfaces
                services.AddSingleton<TaskManagement.Application.Mapping.IProjectMapper, TaskManagement.Application.Mapping.ProjectMapper>();
                services.AddSingleton<TaskManagement.Application.Mapping.ITaskMapper, TaskManagement.Application.Mapping.TaskMapper>();

                // --- Mock MassTransit/RabbitMQ ---
                // For typical integration tests focusing on the API and DB, you might mock MassTransit
                // to prevent messages from actually being sent to RabbitMQ.
                services.RemoveAll(typeof(IPublishEndpoint));
                services.RemoveAll(typeof(ISendEndpointProvider));
                services.AddSingleton(Mock.Of<IPublishEndpoint>());
                services.AddSingleton(Mock.Of<ISendEndpointProvider>());

                // If you want to test the Outbox pattern specifically, you would configure MassTransit
                // to use the InMemory transport or a Testcontainers RabbitMQ instance, and potentially
                // write tests that poll the Outbox tables or use test consumers.

                // --- Mock Options (if needed, but often better to use real config for integration tests) ---
                // services.AddSingleton(Options.Create(new AwsCognitoOptions { ... }));
                // services.AddSingleton(Options.Create(new RabbitMqOptions { ... }));
                // services.AddSingleton(Options.Create(new RedisOptions { ... }));


            });
        }

        // Method to reset the database using Respawn
        public async Task ResetDatabaseAsync()
        {
            // Use the Respawn checkpoint to clean the database
            await _respawner.ResetAsync(_dbContainer.GetConnectionString());
        }

        // Optional: Method to seed test data (can be called after ResetDatabaseAsync)
        public void SeedData(AppDbContext context)
        {
            var user1Id = "test_user_id"; // Matches the user ID in TestAuthHandler
            var user2Id = "another_user_id";

            var project1 = new Project
            {
                Id = Guid.NewGuid(),
                Name = "User 1 Project A",
                Description = "Description A",
                OwnerId = user1Id
            };
            var project2 = new Project
            {
                Id = Guid.NewGuid(),
                Name = "User 1 Project B",
                Description = "Description B",
                OwnerId = user1Id
            };
            var project3 = new Project
            {
                Id = Guid.NewGuid(),
                Name = "User 2 Project C",
                Description = "Description C",
                OwnerId = user2Id
            };

            context.Projects.AddRange(project1, project2, project3);

            var task1 = new Task
            {
                Id = Guid.NewGuid(),
                Title = "Task 1A",
                Description = "Task 1A Desc",
                Status = TaskStatus.Todo,
                ProjectId = project1.Id
            };
            var task2 = new Task
            {
                Id = Guid.NewGuid(),
                Title = "Task 1B",
                Description = "Task 1B Desc",
                Status = TaskStatus.InProgress,
                ProjectId = project1.Id
            };
            var task3 = new Task
            {
                Id = Guid.NewGuid(),
                Title = "Task 2C",
                Description = "Task 2C Desc",
                Status = TaskStatus.Done,
                ProjectId = project3.Id
            };

            context.Tasks.AddRange(task1, task2, task3);

            context.SaveChanges();
        }
    }
