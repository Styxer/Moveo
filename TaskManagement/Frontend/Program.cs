// TaskManagement.Frontend/Program.cs
using TaskManagement.Frontend.Services; // Reference API Client
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Add HttpClient and configure it to use the API Client
builder.Services.AddHttpClient<ApiClient>(client =>
{
    // Configure the base address for the API client
    // This should point to your YARP proxy address
    // Example: http://localhost:5000/
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? throw new InvalidOperationException("ApiBaseUrl configuration is missing."));
    // Optional: Add default headers etc.
    // client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add HttpContextAccessor to allow access to HttpContext in services (for session/cookies)
builder.Services.AddHttpContextAccessor();

// Add session state (for storing JWT token)
builder.Services.AddDistributedMemoryCache(); // Use in-memory cache for session storage (for dev)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
    options.Cookie.HttpOnly = true; // Make the session cookie HTTP-only
    options.Cookie.IsEssential = true; // Make the session cookie essential
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Use session middleware (must be before UseAuthorization)
app.UseSession();

app.UseAuthentication(); // If you add client-side authentication logic
app.UseAuthorization();

app.MapRazorPages();

app.Run();
