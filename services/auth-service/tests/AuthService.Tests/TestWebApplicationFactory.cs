using System;
using System.Linq;
using AuthService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;

namespace AuthService.Tests
{
    /// <summary>
    /// Test WebApplicationFactory that replaces Npgsql DbContext with EF Core InMemory.
    /// Ensures isolated database per test run.
    /// </summary>
    public class TestWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseTestServer();

            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext registrations (AuthDbContext and its options)
                var descriptors = services
                    .Where(d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>) || d.ServiceType == typeof(AuthDbContext))
                    .ToList();
                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }

                // Register InMemory DbContext with a fixed name for request-to-request persistence
                services.AddDbContext<AuthDbContext>(options =>
                    options.UseInMemoryDatabase("AuthDbTests"));

                // Build service provider and ensure database is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
                db.Database.EnsureCreated();
            });
        }
    }
}
