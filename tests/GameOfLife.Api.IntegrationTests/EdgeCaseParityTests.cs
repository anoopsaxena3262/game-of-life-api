using System.Net;
using System.Net.Http.Headers;
using static GameOfLife.Api.IntegrationTests.ApiJson;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>
/// The edges of the HTTP contract: lenient JSON, path and query conversion, methods, route
/// matching, and content negotiation. Expected values are the reference service's answers.
/// </summary>
public sealed class EdgeCaseParityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("2", true)]
    [InlineData("-1", true)]
    [InlineData("\"true\"", true)]
    [InlineData("\"False\"", false)]
    [InlineData("\"TRUE\"", true)]
    [InlineData("\" true\"", true)]
    public async Task A_cell_coerces_from_an_integer_or_a_true_false_string(string cell, bool expected)
    {
        var response = await _client.PostAsync(Boards, Json($$"""{"width":1,"height":1,"cells":[[{{cell}}]]}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal([[expected]], Cells(await ReadAsync(response)));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("0.5")]
    [InlineData("\"1\"")]
    [InlineData("\"yes\"")]
    [InlineData("\"\"")]
    [InlineData("[true]")]
    [InlineData("null")]
    public async Task Any_other_cell_value_is_a_bad_request(string cell)
    {
        var response = await _client.PostAsync(Boards, Json($$"""{"width":1,"height":1,"cells":[[{{cell}}]]}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Bad Request", Title(await ReadAsync(response)));
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("\" 1\"")]
    [InlineData("\"+1\"")]
    [InlineData("1.0")]
    [InlineData("1.9")]
    [InlineData("1e0")]
    public async Task A_width_coerces_from_a_string_or_a_truncated_number(string width)
    {
        var response = await _client.PostAsync(Boards, Json($$"""{"width":{{width}},"height":1,"cells":[[true]]}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, (await ReadAsync(response)).GetProperty("width").GetInt32());
    }

    [Theory]
    [InlineData("\"1.5\"")]
    [InlineData("\"0x1\"")]
    [InlineData("2147483648")]
    [InlineData("1e10")]
    [InlineData("true")]
    [InlineData("\"\"")]
    [InlineData("null")]
    public async Task Any_other_width_is_a_bad_request(string width)
    {
        var response = await _client.PostAsync(Boards, Json($$"""{"width":{{width}},"height":1,"cells":[[true]]}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Bad Request", Title(await ReadAsync(response)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"Width":1,"height":1,"cells":[[true]]}""")]
    [InlineData("""{"width":1,"cells":[[true]]}""")]
    public async Task A_missing_width_or_height_is_a_bad_request_not_a_validation_failure(string body)
    {
        var response = await _client.PostAsync(Boards, Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Bad Request", Title(await ReadAsync(response)));
    }

    [Fact]
    public async Task A_fraction_truncated_below_1_is_a_validation_failure()
    {
        var response = await _client.PostAsync(Boards, Json("""{"width":-0.5,"height":1,"cells":[[true]]}"""));

        Assert.Equal("Validation failed", Title(await ReadAsync(response)));
    }

    [Theory]
    [InlineData("""{"width":1,"height":1,"cells":[[true]]} x""")]
    [InlineData("""{"width":1,"height":1,"cells":[[true]]}{}""")]
    public async Task Content_after_the_json_object_is_ignored(string body)
    {
        var response = await _client.PostAsync(Boards, Json(body));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("generations/%202", 2)]
    [InlineData("generations/0x2", 2)]
    [InlineData("generations/%232", 2)]
    [InlineData("generations/+2", 2)]
    [InlineData("generations/010", 10)]
    public async Task A_generation_index_converts_like_the_reference(string suffix, int generation)
    {
        var id = await CreateBlinkerAsync(_client);

        var body = await ReadAsync(await _client.GetAsync($"{Boards}/{id}/{suffix}"));

        Assert.Equal(generation, body.GetProperty("generation").GetInt32());
    }

    [Theory]
    [InlineData("?maxGenerations=+5", 5)]
    [InlineData("?maxGenerations=0x10", 16)]
    [InlineData("?maxGenerations=%20", 1000)]
    [InlineData("?maxGenerations", 1000)]
    [InlineData("?maxgenerations=1", 1000)]
    [InlineData("?maxGenerations=&maxGenerations=1", 1000)]
    public async Task Max_generations_converts_like_the_reference(string query, int limit)
    {
        var id = await CreateBlinkerAsync(_client);

        var body = await ReadAsync(await _client.GetAsync($"{Boards}/{id}/final{query}"));

        Assert.Equal(limit, body.GetProperty("generationsLimit").GetInt32());
    }

    [Fact]
    public async Task The_first_max_generations_value_wins()
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id}/final?maxGenerations=1&maxGenerations=50");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_short_form_uuid_is_expanded_before_the_lookup()
    {
        var response = await _client.GetAsync($"{Boards}/1-1-1-1-1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "No board with id 00000001-0001-0001-0001-000000000001",
            (await ReadAsync(response)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Head_answers_like_get_and_options_lists_the_methods()
    {
        var id = await CreateBlinkerAsync(_client);

        var head = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"{Boards}/{id}"));
        var optionsBoard = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Options, $"{Boards}/{id}"));
        var optionsUpload = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Options, Boards));

        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(HttpStatusCode.OK, optionsBoard.StatusCode);
        Assert.Equal("GET,HEAD,OPTIONS", string.Join(',', optionsBoard.Content.Headers.Allow));
        Assert.Equal("POST,OPTIONS", string.Join(',', optionsUpload.Content.Headers.Allow));
    }

    [Fact]
    public async Task A_wrong_method_lists_only_the_mapped_methods()
    {
        var response = await _client.DeleteAsync($"{Boards}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(["GET"], response.Content.Headers.Allow);
        Assert.Equal("Method 'DELETE' is not supported.", (await ReadAsync(response)).GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("/api/v1/boards/{0}/")]
    [InlineData("/API/v1/boards/{0}")]
    [InlineData("/api/v1/Boards/{0}")]
    [InlineData("/api/v1/boards/{0}/NEXT")]
    [InlineData("/api/v1/boards/{0}/generations/1/")]
    public async Task A_trailing_slash_or_a_different_case_matches_nothing(string pattern)
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync(string.Format(pattern, id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not Found", Title(await ReadAsync(response)));
    }

    [Fact]
    public async Task A_matrix_parameter_is_removed_from_the_path()
    {
        var id = await CreateBlinkerAsync(_client);

        var response = await _client.GetAsync($"{Boards}/{id};x=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_route_names_the_path()
    {
        var response = await _client.GetAsync("/api/v1//nothing-here");

        Assert.Equal("No static resource api/v1/nothing-here.", (await ReadAsync(response)).GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("application/xml")]
    [InlineData("text/plain")]
    public async Task An_accept_header_without_json_is_406_for_a_result(string accept)
    {
        var id = await CreateBlinkerAsync(_client);
        var request = new HttpRequestMessage(HttpMethod.Get, $"{Boards}/{id}");
        request.Headers.Accept.ParseAdd(accept);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Not Acceptable", Title(body));
        Assert.Equal("Acceptable representations: [application/json, application/*+json].", body.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("application/*", "application/json")]
    [InlineData("*/*", "application/json")]
    [InlineData("text/html, */*;q=0.1", "application/json")]
    [InlineData("application/problem+json", "application/problem+json")]
    public async Task A_result_is_written_as_the_json_type_the_client_accepts(string accept, string contentType)
    {
        var id = await CreateBlinkerAsync(_client);
        var request = new HttpRequestMessage(HttpMethod.Get, $"{Boards}/{id}");
        request.Headers.Accept.ParseAdd(accept);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.ToString());
    }

    [Theory]
    [InlineData("/api/v1/boards/00000000-0000-0000-0000-000000000000", HttpStatusCode.NotFound, "Board not found")]
    [InlineData("/api/v1/boards/not-a-uuid", HttpStatusCode.BadRequest, "Bad Request")]
    public async Task An_error_is_a_problem_document_whatever_the_accept_header(string path, HttpStatusCode status, string title)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await _client.SendAsync(request);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(title, Title(await ReadAsync(response)));
    }

    [Theory]
    [InlineData("text/plain", "Content-Type 'text/plain;charset=UTF-8' is not supported.")]
    [InlineData("text/plain; charset=utf-8", "Content-Type 'text/plain;charset=utf-8' is not supported.")]
    public async Task An_unsupported_content_type_is_named_the_way_the_reference_names_it(string contentType, string detail)
    {
        var content = new StringContent("{}");
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var response = await _client.PostAsync(Boards, content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(detail, (await ReadAsync(response)).GetProperty("detail").GetString());
    }
}
