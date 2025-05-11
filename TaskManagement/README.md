# C# Task Management System

This project is a task management system built with ASP.NET Core 8, Entity Framework Core, and integrated with AWS Cognito for authentication.
It follows Clean Architecture principles, implement the CQRS pattern using MediatR, integrate message-based communication using MassTransit and RabbitMQ with the Outbox pattern, use AutoMapper*for object mapping, and utilize Respawn for efficient database resetting in integration tests.
Additionally, it includes a separate YARP (Yet Another Reverse Proxy) project to sit in front of the backend API.
Dependency injection registration is handled using Scrutor for assembly scanning. 
A custom exception hierarchy is introduced for better error handling.
MediatR Pipeline Behaviors for Validation, Logging, and Transaction Management are added. 
Rate Limiting and Load Balancing are configured in the YARP proxy.
The Options Pattern is used for structured configuration, with validation using Data Annotations.
Distributed caching using Redis is implemented. Docker Compose and Kubernetes manifests are updated to include Redis. 
It is designed to be containerized and deployed to Kubernetes.

## Project Structure

The solution is composed of the following projects:

* `TaskManagement.Domain`: Core business entities and value objects. Has no dependencies on other projects.
* `TaskManagement.Application`: Contains application-specific logic, use case interfaces (`IAuthService`, `IRepository`), DTOs, common extensions (like pagination, filtering, sorting), FluentValidation validators, MediatR Commands and Queries, Message Contracts (Events), Custom Exception classes, MediatR Pipeline Behaviors (ValidationBehavior, LoggingBehavior, TransactionBehavior), and Configuration Option classes (with Data Annotations for validation, including RedisOptions). Depends on `TaskManagement.Domain`.
* `TaskManagement.Infrastructure`: Contains implementations of interfaces defined in the Application layer (repositories, external service integrations like Cognito, database context), MediatR Command and Query Handlers, and MassTransit Consumers. Uses the **AutoMapper** implementations. Handlers throw custom exceptions defined in the Application layer. Services/Handlers inject `IOptions<T>` for configuration and `IDistributedCache` for caching. Depends on `TaskManagement.Application`.
* `TaskManagement.Api`: The ASP.NET Core Web API project. Contains controllers, middleware, and the application startup logic (dependency injection configured with Scrutor, MediatR configuration, MassTransit/RabbitMQ/Outbox configuration, **AutoMapper** DI configuration). Dispatches commands and queries via the mediator. The global error handling middleware is updated to recognize and handle the custom exceptions. Registers the MediatR Validation, Logging, and Transaction Behaviors. Configures and registers Configuration Option classes and enables Data Annotation validation for them. Configures and registers Redis distributed caching. Depends on `TaskManagement.Application` and `TaskManagement.Infrastructure`.
* `TaskManagement.Proxy`: A separate ASP.NET Core project configured as a YARP reverse proxy.
  Forwards requests to the `TaskManagement.Api` service.
  Implements Rate Limiting and Load Balancing.
  Depends only on YARP and configuration.
* `TaskManagement.Frontend`: ASP.NET Core Razor Pages application.
  Consumes the backend API via the YARP proxy.
  Includes an API client and Razor Pages for CRUD operations.
* `TaskManagement.Tests`: Unit and Integration test project. Tests handlers directly (unit) or interact with the API endpoints (integration). May include tests for consumers (unit or integration). Mocks **AutoMapper's IMapper**. Uses Testcontainers for database and Respawn for resetting. Depends on `TaskManagement.Application`, `TaskManagement.Domain`, `TaskManagement.Infrastructure`, and `TaskManagement.Api` (via `Microsoft.AspNetCore.Mvc.Testing`).

## Features

* User Authentication via AWS Cognito
* Project Management (CRUD implemented via Commands and Queries)
* Task Management (CRUD implemented via Commands and Queries within projects)
* Basic Logging and Error Handling
* Pagination, Filtering, and Sorting on list endpoints (handled by Queries)
* Role-Based Access Control (Admin vs. Owner - enforced in Handlers)
* Input Validation (FluentValidation and Business Rules)
* Retry Mechanism for Transient Faults (using Polly)
* Unit Tests
* Integration Tests (using Microsoft.AspNetCore.Mvc.Testing and Testcontainers-Dotnet, with database reset using Respawn)
* Health Checks (for application and dependencies)
* Dockerfile for containerization
* Basic Kubernetes manifests
* Message-based communication (MassTransit and RabbitMQ)
* Outbox Pattern for reliable message publishing
* Object mapping using AutoMapper
* Reverse Proxy using YARP
* Automated Dependency Injection Registration using Scrutor
* Custom Exception Hierarchy
* MediatR Validation, Logging, and Transaction Behaviors
* Rate Limiting (in YARP proxy)
* Load Balancing (in YARP proxy)
* Structured Configuration using the Options Pattern with Data Annotation Validation
* Distributed Caching using Redis
* Docker Compose setup including Redis
* Kubernetes manifests including Redis Deployment and Service
* Frontend Razor Pages application for CRUD operations

## Technology Stack

* **Backend API:** ASP.NET Core 8, C#, PostgreSQL, Entity Framework Core, AWS Cognito, Polly, MediatR, MassTransit, RabbitMQ, MassTransit.EntityFrameworkCore (Outbox), AutoMapper, FluentValidation, Scrutor, Microsoft.Extensions.Options, Microsoft.Extensions.Options.DataAnnotations, Microsoft.Extensions.Caching.StackExchangeRedis
* **Frontend:** ASP.NET Core 8, C#, Razor Pages, HttpClient
* **Reverse Proxy:** ASP.NET Core 8, C#, YARP, Microsoft.AspNetCore.RateLimiting
* **Testing:** xUnit, Moq, Microsoft.AspNetCore.Mvc.Testing, Testcontainers-Dotnet, Respawn
* **Containerization:** Docker, Docker Compose
* **Orchestration:** Kubernetes

## Setup and Running Locally (with Docker Compose)

1.  **Prerequisites:**
    * .NET 8 SDK installed.
    * Docker Desktop installed (required for Testcontainers and RabbitMQ).
    * Access to an AWS account to set up Cognito.
    * A PostgreSQL database instance (for local development/migrations, Testcontainers handles it for integration tests).
    * **A running RabbitMQ instance.** You can run one using Docker: `docker run -d --hostname my-rabbit --name some-rabbit -p 15672:15672 -p 5672:5672 rabbitmq:3-management`
    * **A running Redis instance.** You can run one using Docker: `docker run -d --name some-redis -p 6379:6379 redis`
    * Optional: A tool like Postman or curl to test the API endpoints.

2.  **AWS Cognito Setup:**
    * Go to the AWS Management Console and navigate to Cognito.
    * Create a new User Pool and App Client. Note down the User Pool ID and App Client ID.
    * Manually create users with different roles/groups (e.g., `Admin`) for testing RBAC(Role-based access control).

3.  **Configure `appsettings.json` files:**
    * **TaskManagement.Api:** Update database connection string, AWS Cognito settings, RabbitMQ connection details, and Redis configuration.
    * **TaskManagement.Frontend:** Update `ApiBaseUrl` to point to the YARP proxy service name in Docker Compose (`http://taskmanagement-proxy:80/`).
    * **TaskManagement.Proxy:** Update `ReverseProxy:Clusters:taskmanagementCluster:Destinations:backend:Address` to point to the backend API service name in Docker Compose (`http://taskmanagement-backend:80`). Review and configure Rate Limiting and Load Balancing policies.

4.  **Run Database Migrations (using Docker Compose DB):**
    * Start the database container: `docker-compose up -d db`
    * Wait for the database to be healthy (`docker-compose ps` and `docker-compose logs db`).
    * Open a terminal in the `TaskManagement.Api` directory.
    * Ensure your local environment has access to the Docker Compose DB (e.g., connection string points to `localhost:5432`).
    * Install Entity Framework Core tools: `dotnet tool install --global dotnet-ef`
    * Add migrations: `dotnet ef migrations add InitialCreate` (if not already done), `dotnet ef migrations add AddMassTransitOutbox`.
    * Apply migrations: `dotnet ef database update`
    * Stop the DB container: `docker-compose down db`

5.  **Run the Applications (with Docker Compose):**
    * Open a terminal in the root directory of the solution (containing `docker-compose.yml`).
    * Build and run all services: `docker-compose up --build`
    * The Frontend application should be accessible on `http://localhost:5002`. The YARP proxy on `http://localhost:5000`.

6.  **Test the Application:**
    * Open your browser to the frontend application address (e.g., `http://localhost:5002`).
    * Use the Login page to authenticate via the backend API/Cognito. The frontend will need to handle storing and sending the JWT token.
    * Navigate through the Project and Task pages to perform CRUD operations.
    * Observe how the frontend interacts with the backend API via the YARP proxy.
    * Check RabbitMQ management UI (http://localhost:15672) to see messages.
    * Check the health check endpoints via YARP (e.g., `http://localhost:5000/healthz`).

## Running Tests

1.  Ensure Docker Desktop is running (for integration tests).
2.  Ensure a RabbitMQ container is running if not using Testcontainers for it.
3.  Ensure a Redis container is running if not using Testcontainers for it.
4.  Open a terminal in the root directory of the solution (containing the `.sln` file).
5.  Run all tests (unit and integration): `dotnet test`
    * Integration tests will use Testcontainers to spin up *separate* DB and Redis containers for each test run.
    * Unit tests for handlers will need to mock `IPublishEndpoint`, `IDistributedCache`, and use the mocked `IMapper`.

## Containerization (Docker)

1.  Ensure Docker Desktop is running.
2.  Ensure a RabbitMQ container and Redis container are running or configure your Kubernetes deployment to include them.
3.  Open a terminal in the root directory of the solution (containing the `.sln` file).
4.  Create a Dockerfile for the YARP proxy project (similar to the API Dockerfile, but targeting the proxy project). Update the base image to `mcr.microsoft.com/dotnet/sdk:8.0` and `mcr.microsoft.com/dotnet/aspnet:8.0`.
5.  Build the API Docker image: `docker build -t your-dockerhub-username/taskmanagement-backend:latest -f TaskManagement.Api/Dockerfile .`
6.  Build the YARP proxy Docker image: `docker build -t your-dockerhub-username/taskmanagement-proxy:latest -f TaskManagement.Proxy/Dockerfile .`
7.  Build the Frontend Docker image: `docker build -t your-dockerhub-username/taskmanagement-frontend:latest -f TaskManagement.Frontend/Dockerfile .`
8.  Push all images to a registry (e.g., Docker Hub, AWS ECR): `docker push your-dockerhub-username/taskmanagement-backend:latest`, `docker push your-dockerhub-username/taskmanagement-proxy:latest`, and `docker push your-dockerhub-username/taskmanagement-frontend:latest`.

## Kubernetes Deployment Suggestion (for 10k Users/Day + Client-Side)

The Kubernetes manifests now include deployments and services for PostgreSQL, RabbitMQ, Redis, the backend API, the YARP proxy, and the frontend application.

For a production deployment handling 10k users in Kubernetes, you would:

* Deploy the provided Kubernetes YAML manifests.
* Ensure database connection strings, RabbitMQ, Redis, and AWS credentials are managed securely using Kubernetes Secrets.
* Implement health checks for all components.
* Configure Horizontal Pod Autoscaling (HPA) for the backend, YARP, and frontend.
* Consider using managed services for PostgreSQL, RabbitMQ, and Redis in your cloud provider.
* Expose the Frontend Service externally using a LoadBalancer or Ingress. The YARP proxy Service remains internal (`ClusterIP`) unless it's also the public entry point.
* Configure YARP Rate Limiting and Load Balancing policies via Kubernetes ConfigMaps or environment variables.
* Consider deploying a separate Outbox dispatcher process.
* Set up comprehensive monitoring and alerting.

## Notes and Design Decisions

* **.NET 8:** The project currently targets .NET 8. Upgrading to .NET 9/C# 14 would involve updating the `TargetFramework` in the `.csproj` files and potentially the Dockerfile base images.
* **Architecture:** Clean Architecture with CQRS, MediatR, Messaging, Outbox, AutoMapper, FluentValidation, Scrutor, Options Pattern, Redis Cache, YARP.
* **Custom Exception Hierarchy:** Provides clear error intent.
* **Options Pattern & Validation:** Structured configuration with Data Annotation validation.
* **Distributed Cache (Redis):** Integrated using `IDistributedCache` for caching query results and invalidated by command handlers.
* **YARP:** Separate reverse proxy handling Rate Limiting and Load Balancing.
* **Docker Compose:** Simplifies local development setup.
* **Kubernetes Manifests:** Provides a deployment strategy for all components.
* **Health Checks:** Configured for the API, DB, RabbitMQ, and Redis.
* **MediatR Behaviors:** Validation, Logging, and Transaction behaviors in the backend pipeline.
* **Retry Mechanism (Polly):** Handles transient failures.
* **Error Handling:** Global middleware in the API layer handles custom exceptions and validation errors. Frontend API client handles API response status codes.
* **Data Access:** Repository and DbContext.
* **Integration Tests:** Use Testcontainers/Respawn for isolated and efficient testing.
* **Mapping:** AutoMapper is used for mapping.
* **Logging:** Standard `ILogger` is used.
* **Frontend (Razor Pages):** Provides a server-rendered web UI, consumes the backend API via YARP.

## Areas for Improvement / Further Work

* Implement the Login and Logout functionality and JWT token storage/handling (session or cookies) in the frontend.
* Complete the Task CRUD Razor Pages (Edit, Delete).
* Add more robust error display on the frontend pages based on API error responses.
* Add more comprehensive integration tests for the frontend.
* Implementing integration tests specifically for retry policies and the Outbox pattern.
* Implement distributed tracing.
* Configure YARP authentication/authorization.
* Implement soft deletes where needed.
* Add more features.
* Consider a separate Outbox dispatcher process.
