using System.Net;
using FluentAssertions;
using GaiaSkyline.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GaiaSkyline.Web.Tests;

/// <summary>The map-tile proxy returns the upstream PNG for valid coordinates and refuses bad ones. Stage 7 §5.</summary>
public sealed class MapTilesControllerTests
{
    [Fact]
    public async Task Returns_the_proxied_png_with_a_long_cache_for_valid_coordinates()
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var controller = CreateController(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(png),
        });

        var result = await controller.Tile(13, 4000, 3000, CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Which;
        file.ContentType.Should().Be("image/png");
        file.FileContents.Should().Equal(png);
        controller.Response.Headers.CacheControl.ToString().Should().Contain("max-age=604800");
    }

    [Theory]
    [InlineData(20, 0, 0)] // zoom above the max
    [InlineData(-1, 0, 0)] // negative zoom
    [InlineData(1, 2, 0)]  // x out of range (zoom 1 → valid indices 0..1)
    [InlineData(1, 0, 5)]  // y out of range
    public async Task Rejects_out_of_range_coordinates(int z, int x, int y)
    {
        var controller = CreateController(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([]),
        });

        (await controller.Tile(z, x, y, CancellationToken.None)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Returns_502_when_the_upstream_tile_fails()
    {
        var controller = CreateController(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await controller.Tile(13, 4000, 3000, CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    private static MapTilesController CreateController(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var client = new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("https://tile.openstreetmap.org/") };
        return new MapTilesController(new SingleClientFactory(client), NullLogger<MapTilesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
