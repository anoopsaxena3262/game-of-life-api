using System.Net;
using static GameOfLife.Api.IntegrationTests.ApiJson;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>
/// Pins the HTTP contract the demo scripts check: status codes, problem titles, the
/// problem document shape, and values that do not convert.
/// </summary>
public sealed class ApiParityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Problem_documents_have_the_rfc_7807_shape_without_a_trace_id()
    {
        var id = Guid.NewGuid();

        var response = await _client.GetAsync($"{Boards}/{id}");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadAsync(response);
        Assert.Equal(
            ["type", "title", "status", "detail", "instance"],
            body.EnumerateObject().Select(property => property.Name));
        Assert.Equal("about:blank", body.GetProperty("type").GetString());
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Equal($"No board with id {id}", body.GetProperty("detail").GetString());
        Assert.Equal($"/api/v1/boards/{id}", body.GetProperty("instance").GetString());
    }

    [Theory]
    [InlineData("/api/v1/boards/not-a-uuid", "Failed to convert 'id' with value: 'not-a-uuid'")]
    [InlineData("/api/v1/boards/00000000000000000000000000000000", "Failed to convert 'id' with value: '00000000000000000000000000000000'")]
    [InlineData("/api/v1/boards/00000000-0000-0000-0000-000000000000/generations/abc", "Failed to convert 'n' with value: 'abc'")]
    [InlineData("/api/v1/boards/00000000-0000-0000-0000-000000000000/generations/99999999999", "Failed to convert 'n' with value: '99999999999'")]
    [InlineData("/api/v1/boards/00000000-0000-0000-0000-000000000000/final?maxGenerations=ten", "Failed to convert 'maxGenerations' with value: 'ten'")]
    public async Task A_path_or_query_value_that_does_not_convert_is_a_bad_request(string path, string detail)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Bad Request", Title(body));
        Assert.Equal(detail, body.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("generations/10001", "Generation index exceeds ceiling of 10000")]
    [InlineData("final?maxGenerations=0", "maxGenerations must be at least 1")]
    [InlineData("final?maxGenerations=-3", "maxGenerations must be at least 1")]
    public async Task A_value_outside_the_limits_is_an_invalid_board(string suffix, string detail)
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id}/{suffix}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Invalid board", Title(body));
        Assert.Equal(detail, body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_limit_above_the_ceiling_is_clamped_and_still_concludes()
    {
        var id = await CreateBlinkerAsync(_client);

        var body = await ReadAsync(await _client.GetAsync($"{Boards}/{id}/final?maxGenerations=100000"));

        Assert.Equal("CYCLE", body.GetProperty("terminationKind").GetString());
        Assert.Equal(10000, body.GetProperty("generationsLimit").GetInt32());
    }

    [Fact]
    public async Task An_empty_max_generations_uses_the_default()
    {
        var id = await CreateBlinkerAsync(_client);

        var body = await ReadAsync(await _client.GetAsync($"{Boards}/{id}/final?maxGenerations="));

        Assert.Equal(1000, body.GetProperty("generationsLimit").GetInt32());
    }

    [Fact]
    public async Task A_still_life_reports_fixed_point()
    {
        bool[][] block = [[true, true], [true, true]];
        var created = await ReadAsync(await _client.PostAsync(Boards, Upload(2, 2, block)));

        var body = await ReadAsync(await _client.GetAsync($"{Boards}/{created.GetProperty("id").GetGuid()}/final"));

        Assert.Equal("FIXED_POINT", body.GetProperty("terminationKind").GetString());
        Assert.Equal(1, body.GetProperty("period").GetInt32());
    }

    [Fact]
    public async Task Rows_that_do_not_match_the_declared_size_are_an_invalid_board()
    {
        var response = await _client.PostAsync(
            Boards, Json("""{"width":2,"height":2,"cells":[[false,false,false],[false,false,false]]}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Invalid board", Title(body));
        Assert.Equal("Grid width does not match declared width", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_board_over_max_cells_is_an_invalid_board()
    {
        var row = new bool[301];
        var cells = Enumerable.Repeat(row, 301).ToArray();

        var response = await _client.PostAsync(Boards, Upload(301, 301, cells));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Invalid board", Title(body));
        Assert.Equal("Board exceeds maximum cell count of 90000", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Width_and_height_below_1_are_both_listed()
    {
        var response = await _client.PostAsync(Boards, Json("""{"width":0,"height":0,"cells":[]}"""));

        var body = await ReadAsync(response);
        Assert.Equal("Validation failed", Title(body));
        Assert.Equal(
            "width: must be greater than or equal to 1; height: must be greater than or equal to 1",
            body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_body_that_is_not_json_content_is_unsupported_media_type()
    {
        var response = await _client.PostAsync(Boards, new StringContent("{}", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("Unsupported Media Type", Title(await ReadAsync(response)));
    }

    [Fact]
    public async Task An_unknown_route_and_a_wrong_method_are_problem_documents()
    {
        var notFound = await _client.GetAsync("/api/v1/nothing-here");
        var wrongMethod = await _client.DeleteAsync($"{Boards}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("Not Found", Title(await ReadAsync(notFound)));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.Equal("Method Not Allowed", Title(await ReadAsync(wrongMethod)));
    }

    [Fact]
    public async Task Generation_past_the_cell_generation_budget_is_rejected()
    {
        var cells = Enumerable.Range(0, 300).Select(_ => new bool[300]).ToArray();
        cells[0][1] = cells[1][2] = cells[2][0] = cells[2][1] = cells[2][2] = true;
        var created = await ReadAsync(await _client.PostAsync(Boards, Upload(300, 300, cells)));

        var response = await _client.GetAsync($"{Boards}/{created.GetProperty("id").GetGuid()}/generations/56");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "Generation 56 on a board of 90000 cells exceeds the cell-generation budget of 5000000",
            (await ReadAsync(response)).GetProperty("detail").GetString());
    }
}
