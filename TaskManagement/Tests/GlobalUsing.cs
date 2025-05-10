global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.AspNetCore.TestHost;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Configuration;
global using System.Collections.Generic;
global using System.Linq;
global using TaskManagement.Infrastructure.Data; // Reference Infrastructure DbContext
global using TaskManagement.Domain.Models; // Reference Domain models
global using Testcontainers.PostgreSql; // Testcontainers for PostgreSQL
global using System.Threading.Tasks;
global using System;
global using Microsoft.IdentityModel.Tokens; // Needed for JwtBearer
global using Microsoft.AspNetCore.Authentication; // Needed for AuthenticationBuilder
global using System.Security.Claims; // Needed for ClaimsIdentity
global using Microsoft.AspNetCore.Authentication.JwtBearer; // Needed for JwtBearerDefaults
global using AutoMapper; // Needed for AutoMapper setup (Still needed if other parts use it)
global using Respawn; // Added for Respawn
global using Npgsql; // Needed for NpgsqlConnection
global using Microsoft.Extensions.Logging; // Needed for logging in factory
global using Microsoft.Extensions.Options; // Needed for IOptions
global using TaskManagement.Application.Options; // Reference the Options classes
global using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache
global using Testcontainers.Redis; // Added for Redis Testcontainer
global using Moq;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using Xunit; // Using xUnit
global using Microsoft.Extensions.Logging; // Mocking logger
global using AutoMapper; // Use AutoMapper
global using TaskManagement.Application.Interfaces; // Reference Application interface
global using TaskManagement.Domain.Models; // Reference Domain models
global using TaskManagement.Infrastructure.Handlers.Projects; // Test the Infrastructure handlers
global using TaskManagement.Application.DTOs.Projects;
global using TaskManagement.Application.DTOs.Pagination;
global using System.Linq.Expressions; // Needed for repository mock setup
global using MockQueryable.Moq; // Helper for mocking IQueryable
global using Microsoft.EntityFrameworkCore; // Needed for mocking async EF methods
global using MediatR; // Needed for MediatR Unit
global using MassTransit; // Needed for IPublishEndpoint mock
global using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using System.Text;
using TaskManagement.Application.DTOs.Projects; // Reference Application DTOs
using TaskManagement.Infrastructure.Data; // Reference Infrastructure DbContext
using Microsoft.Extensions.DependencyInjection; // Needed to get DbContext from factory
using System.Linq; // Needed for LINQ operations on DbContext
using TaskManagement.Domain.Models; // Reference Domain models
using TaskManagement.Application.DTOs.Pagination; // Reference Pagination DTOs
using Microsoft.Extensions.Caching.Distributed; // Needed for IDistributedCache