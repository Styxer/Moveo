using Domain.Models;
using Infrastructure.Data;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Task = System.Threading.Tasks.Task;
using TaskStatus = System.Threading.Tasks.TaskStatus;


namespace TaskManagement.Tests
{

    public class CustomWebApplicationFactory(Respawner respawner) : WebApplicationFactory<Program>, IAsyncLifetime
    {
        //TODO:read real variables
        private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("mypassword")
            .Build();

        //TODO:read real variables
        private readonly RedisContainer _redisContainer = new RedisBuilder()
            .WithImage("redis:latest")
            .Build();



        private Respawner _respawner = respawner;


        public async Task InitializeAsync()
        {

            await Task.WhenAll(_dbContainer.StartAsync(), _redisContainer.StartAsync());


            using (var scope = Services.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var dbContext = scopedServices.GetRequiredService<AppDbContext>();
                var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

                try
                {
                    logger.LogInformation("Applying migrations for integration tests...");

                    // dbContext.Database.Migrate();
                    logger.LogInformation("Migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred applying migrations for integration tests. Error: {Message}",
                        ex.Message);
                    throw;
                }
            }


            _respawner = await Respawner.CreateAsync(_dbContainer.GetConnectionString(), new RespawnerOptions
            {
                TablesToIgnore = ["__EFMigrationsHistory", "InboxState", "OutboxMessage", "OutboxState"],
                DbAdapter = DbAdapter.Postgres // Specify the database adapter
            });
        }


        public new async Task DisposeAsync()
        {
            await _dbContainer.DisposeAsync();
            await _redisContainer.DisposeAsync();
        }


        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {

                services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
                services.RemoveAll(typeof(AppDbContext));


                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseNpgsql(_dbContainer.GetConnectionString()); //TODO: ADD NUGET
                });


                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                });

                services.AddAuthentication("TestScheme")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });


                services.RemoveAll(typeof(IDistributedCache));
                services.RemoveAll(typeof(IConfigureOptions<RedisCacheOptions>)); //TODO: ADD NUGET

                //TODO: ADD NUGET
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = _redisContainer.GetConnectionString();
                });


                //TODO: ADD NUGET
                services.AddSingleton<Application.Mapping.IProjectMapper, Application.Mapping.ProjectMapper>();
                services.AddSingleton<Application.Mapping.ITaskMapper, Application.Mapping.TaskMapper>();


                services.RemoveAll(typeof(IPublishEndpoint));
                services.RemoveAll(typeof(ISendEndpointProvider));
                services.AddSingleton(Mock.Of<IPublishEndpoint>());
                services.AddSingleton(Mock.Of<ISendEndpointProvider>());


            });
        }

        // Method to reset the database using Respawn
        public async Task ResetDatabaseAsync()
        {

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

            var task1 = new Domain.Models.Task()
            {
                Id = Guid.NewGuid(),
                Title = "Task 1A",
                Description = "Task 1A Desc",
                Status = Domain.Models.TaskStatus.Todo,
                ProjectId = project1.Id
            };
            var task2 = new Domain.Models.Task
            {
                Id = Guid.NewGuid(),
                Title = "Task 1B",
                Description = "Task 1B Desc",
                Status = Domain.Models.TaskStatus.InProgress,
                ProjectId = project1.Id
            };
            var task3 = new Domain.Models.Task
            {
                Id = Guid.NewGuid(),
                Title = "Task 2C",
                Description = "Task 2C Desc",
                Status = Domain.Models.TaskStatus.Done,
                ProjectId = project3.Id
            };

            context.Tasks.AddRange(task1, task2, task3);

            context.SaveChanges();
        }
    }
}
