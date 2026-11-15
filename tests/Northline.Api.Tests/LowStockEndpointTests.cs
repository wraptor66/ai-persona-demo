using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Northline.Api.Data;

namespace Northline.Api.Tests;

/// <summary>
/// KAN-4: GET /api/products/low-stock through the real HTTP pipeline (routing, binding, authentication),
/// hosted in memory. The database is the EF Core InMemory provider with the standard seed; no SQL Server.
/// </summary>
[TestClass]
public class LowStockEndpointTests
{
    private const string Route = "/api/products/low-stock";
    private const string Issuer = "kan4-test-issuer";
    private const string Audience = "northline-api";

    // A fresh signing key per test run; never stored anywhere.
    private static readonly SymmetricSecurityKey SigningKey = new(RandomNumberGenerator.GetBytes(32));

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"kan4-http-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<NorthlineDbContext>>();
                services.AddDbContext<NorthlineDbContext>(o => o.UseInMemoryDatabase(_dbName));

                // Tokens are validated against a local key instead of a real identity provider.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                {
                    o.Authority = null;
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = Issuer,
                        ValidAudience = Audience,
                        IssuerSigningKey = SigningKey,
                    };
                });
            });
        }
    }

    private static string CreateToken(SecurityKey key) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = "kan4-test-user" },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });

    private static HttpClient CreateClient(ApiFactory factory, string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static async Task<JsonElement[]> ReadArrayAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.ValueKind);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToArray();
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC1_NoThresholdParameter_UsesDefaultOf10()
    {
        using var factory = new ApiFactory();
        using var client = CreateClient(factory, CreateToken(SigningKey));

        var response = await client.GetAsync(Route);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        // Seed (SeedData.cs): active products below 10 are ids 2 (6), 4 (3), 6 (0), 7 (9); id 8 is inactive.
        var ids = (await ReadArrayAsync(response)).Select(e => e.GetProperty("id").GetInt32()).ToArray();
        CollectionAssert.AreEqual(new[] { 2, 4, 6, 7 }, ids);
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC1_ThresholdParameter_IsApplied()
    {
        using var factory = new ApiFactory();
        using var client = CreateClient(factory, CreateToken(SigningKey));

        var response = await client.GetAsync($"{Route}?threshold=5");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var ids = (await ReadArrayAsync(response)).Select(e => e.GetProperty("id").GetInt32()).ToArray();
        CollectionAssert.AreEqual(new[] { 4, 6 }, ids);
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC2_EachItem_HasExactlyIdNameQuantityUnitPrice()
    {
        using var factory = new ApiFactory();
        using var client = CreateClient(factory, CreateToken(SigningKey));

        // Threshold 1 isolates seed product 6 (Market Tote, quantity 0, price 22.00).
        var response = await client.GetAsync($"{Route}?threshold=1");

        var items = await ReadArrayAsync(response);
        Assert.AreEqual(1, items.Length);
        var item = items[0];
        CollectionAssert.AreEquivalent(
            new[] { "id", "name", "quantity", "unitPrice" },
            item.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.AreEqual(6, item.GetProperty("id").GetInt32());
        Assert.AreEqual("Market Tote", item.GetProperty("name").GetString());
        Assert.AreEqual(0, item.GetProperty("quantity").GetInt32());
        Assert.AreEqual(22.00m, item.GetProperty("unitPrice").GetDecimal());
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC3_NoneBelowThreshold_Returns200WithEmptyArray()
    {
        using var factory = new ApiFactory();
        using var client = CreateClient(factory, CreateToken(SigningKey));

        var response = await client.GetAsync($"{Route}?threshold=0");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, (await ReadArrayAsync(response)).Length);
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC4_NoToken_Returns401()
    {
        using var factory = new ApiFactory();
        using var client = CreateClient(factory, token: null);

        var response = await client.GetAsync(Route);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC4_TokenSignedWithWrongKey_Returns401()
    {
        using var factory = new ApiFactory();
        var wrongKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        using var client = CreateClient(factory, CreateToken(wrongKey));

        var response = await client.GetAsync(Route);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task AC4_OtherProductEndpoints_StayAnonymous()
    {
        // D-KAN-4-2: [Authorize] is on the low-stock action only.
        using var factory = new ApiFactory();
        using var client = CreateClient(factory, token: null);

        var response = await client.GetAsync("/api/products");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    [TestCategory("TICKET-KAN-4")]
    public async Task R7b_NonNumericThreshold_Returns400()
    {
        // Proposed answer to review prompt R7 (bad threshold values), comment 10000 on KAN-4.
        using var factory = new ApiFactory();
        using var client = CreateClient(factory, CreateToken(SigningKey));

        var response = await client.GetAsync($"{Route}?threshold=abc");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
