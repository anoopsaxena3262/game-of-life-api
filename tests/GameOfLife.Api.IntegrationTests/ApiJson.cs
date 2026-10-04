using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>Request and response helpers that read JSON the way a client would.</summary>
internal static class ApiJson
{
    public const string Boards = "/api/v1/boards";

    public static readonly bool[][] Blinker =
    [
        [false, false, false],
        [true, true, true],
        [false, false, false],
    ];

    public static readonly bool[][] VerticalBlinker =
    [
        [false, true, false],
        [false, true, false],
        [false, true, false],
    ];

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    public static StringContent Upload(int width, int height, bool[][] cells) =>
        Json(JsonSerializer.Serialize(new { width, height, cells }, Web));

    public static async Task<Guid> CreateBlinkerAsync(HttpClient client)
    {
        var response = await client.PostAsync(Boards, Upload(3, 3, Blinker));
        response.EnsureSuccessStatusCode();
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static bool[][] Cells(JsonElement body) =>
        body.GetProperty("cells").Deserialize<bool[][]>(Web)!;

    public static string? Title(JsonElement body) => body.GetProperty("title").GetString();

    /// <summary>Content with no length, so it is sent chunked and only a counting read can stop it.</summary>
    public static StreamContent Chunked(string json)
    {
        var content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(json)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
    }
}
