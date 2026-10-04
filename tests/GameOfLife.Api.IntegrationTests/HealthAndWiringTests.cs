using System.Net;
using System.Net.Http.Json;

namespace GameOfLife.Api.IntegrationTests;

public sealed class HealthAndWiringTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Later = "Skeleton. The assertion is written with the requirement it covers.";

    [Fact]
    public async Task Live_returns_ok()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_returns_ok_once_the_database_opens()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_is_wired_and_not_implemented()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/boards", new
        {
            width = 1,
            height = 1,
            cells = new[] { "0" }
        });

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Theory(Skip = Later)]
    [InlineData("FR-1.1")]
    [InlineData("FR-2.1")]
    [InlineData("FR-3.1")]
    [InlineData("FR-4.1")]
    [InlineData("FR-4.4")]
    [InlineData("FR-4.17")]
    [InlineData("FR-5.1")]
    public void Requirement_has_a_named_test(string requirement)
    {
        Assert.False(string.IsNullOrWhiteSpace(requirement));
    }
}
