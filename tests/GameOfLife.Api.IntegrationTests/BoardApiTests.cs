using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using static GameOfLife.Api.IntegrationTests.ApiJson;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>
/// End-to-end coverage of every endpoint and the error paths exercised over HTTP.
/// Cell values for the blinker are asserted here; the rules suite owns the other
/// patterns. Uses its own database file.
/// </summary>
public sealed class BoardApiTests : IClassFixture<ApiFactory>
{
    /// <summary>Small enough to cross in a test, large enough that the cap trips inside <c>cells</c>.</summary>
    private const int MaxRequestBytes = 12_000;

    private readonly HttpClient _client;

    public BoardApiTests(ApiFactory factory)
    {
        _client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("GameOfLife:MaxRequestBytes", MaxRequestBytes.ToString()))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task Post_boards_returns_201_with_an_id_and_a_location_header()
    {
        var response = await _client.PostAsync(Boards, Upload(3, 3, Blinker));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(3, body.GetProperty("width").GetInt32());
        Assert.Equal(3, body.GetProperty("height").GetInt32());
        Assert.Equal(0, body.GetProperty("generation").GetInt32());
        Assert.Equal(Blinker, Cells(body));
        Assert.Equal($"/api/v1/boards/{id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Get_next_returns_the_following_generation()
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id}/next");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal(1, body.GetProperty("generation").GetInt32());
        Assert.Equal(VerticalBlinker, Cells(body));
    }

    [Fact]
    public async Task Get_board_returns_the_uploaded_board()
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(0, body.GetProperty("generation").GetInt32());
        Assert.Equal(Blinker, Cells(body));
    }

    [Fact]
    public async Task Calling_next_twice_returns_the_same_generation_both_times()
    {
        var id = await CreateBlinkerAsync(_client);

        var first = await _client.GetStringAsync($"{Boards}/{id}/next");
        var second = await _client.GetStringAsync($"{Boards}/{id}/next");

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Get_generations_n_returns_the_expected_state()
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id}/generations/2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal(2, body.GetProperty("generation").GetInt32());
        Assert.Equal(Blinker, Cells(body));
    }

    [Fact]
    public async Task Get_final_returns_the_state_and_termination_metadata()
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id}/final");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(id, body.GetProperty("id").GetGuid());
        Assert.Equal("CYCLE", body.GetProperty("terminationKind").GetString());
        Assert.Equal(0, body.GetProperty("firstOccurrenceGeneration").GetInt32());
        Assert.Equal(2, body.GetProperty("period").GetInt32());
        Assert.Equal(2, body.GetProperty("generationsComputed").GetInt32());
        Assert.Equal(1000, body.GetProperty("generationsLimit").GetInt32());
        Assert.Equal(Blinker, Cells(body));
    }

    [Fact]
    public async Task Get_final_returns_422_when_the_generation_limit_is_exceeded()
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id}/final?maxGenerations=1");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("No conclusion", text);
        Assert.Contains("\"generationsAttempted\":1", text);
    }

    [Fact]
    public async Task Unknown_board_id_returns_404_with_a_problem_body()
    {
        var response = await _client.GetAsync($"{Boards}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Board not found", Title(await ReadAsync(response)));
    }

    [Fact]
    public async Task Malformed_upload_returns_400_validation_failed_naming_the_field()
    {
        var response = await _client.PostAsync(Boards, Upload(0, 3, [new bool[3], new bool[3], new bool[3]]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Validation failed", Title(body));
        Assert.Contains("width", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_a_bad_request()
    {
        var response = await _client.PostAsync(Boards, Json("{"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Bad Request", Title(await ReadAsync(response)));
    }

    [Fact]
    public async Task A_missing_cells_field_is_a_validation_failure()
    {
        var response = await _client.PostAsync(Boards, Json("""{"width":1,"height":1}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Validation failed", Title(body));
        Assert.Equal("cells: must not be null", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_null_cell_is_rejected_instead_of_stored_as_dead()
    {
        var response = await _client.PostAsync(Boards, Json("""{"width":1,"height":1,"cells":[[null]]}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_body_over_the_request_size_limit_is_rejected()
    {
        var response = await _client.PostAsync(
            Boards, Json("""{"width":1,"height":1,"cells":[[""" + new string(' ', MaxRequestBytes) + "]]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Request too large", Title(await ReadAsync(response)));
    }

    [Fact]
    public async Task A_chunked_body_that_crosses_the_cap_inside_cells_is_request_too_large()
    {
        var content = Chunked("""{"width":1,"height":1,"cells":[[""" + new string(' ', MaxRequestBytes) + "]]}");
        Assert.Null(content.Headers.ContentLength);

        var response = await _client.PostAsync(Boards, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Request too large", Title(await ReadAsync(response)));
    }

    [Fact]
    public async Task A_negative_generation_index_returns_400()
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id}/generations/-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid board", Title(await ReadAsync(response)));
    }
}
