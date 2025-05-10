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
global using Microsoft.Extensions.Caching.Distributed; 
global using Testcontainers.Redis; 
global using Moq;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using Xunit; 
global using Microsoft.Extensions.Logging; 
global using AutoMapper;
global using System.Linq.Expressions; 
global using MockQueryable.Moq; 
global using Microsoft.EntityFrameworkCore; 
global using MediatR; 
global using MassTransit; 
global using Microsoft.Extensions.Caching.Distributed;
global using System;
global using System.Net;
global using System.Net.Http;
global using System.Threading.Tasks;
global using Xunit;
global using Microsoft.AspNetCore.Mvc.Testing;
global using System.Text.Json;
global using System.Text;
global using Microsoft.Extensions.DependencyInjection;
global using System.Linq; 
global using Microsoft.Extensions.Caching.Distributed; 
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.AspNetCore.TestHost;