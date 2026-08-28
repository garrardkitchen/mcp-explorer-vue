using System.Net;
using System.Text;
using Garrard.Mcp.Explorer.Api.Controllers.v1;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Garrard.Tests.Mcp.Explorer.Api.Controllers;

public sealed class ChatDocumentPreviewControllerTests
{
    [Fact]
    public async Task PreviewDocument_AzureBlob_ReturnsDownloadedContentInline()
    {
        var expected = Encoding.UTF8.GetBytes("pdf-content");
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(expected)
            {
                Headers = { ContentType = new("application/pdf") }
            }
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("ChatDocumentPreview"))
            .Returns(new HttpClient(handler));
        var controller = CreateController(factory.Object);

        var result = await controller.PreviewDocument(
            new DocumentPreviewRequest("https://account.blob.core.windows.net/docs/file.pdf?sig=secret"),
            CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(expected, file.FileContents);
    }

    [Theory]
    [InlineData("http://account.blob.core.windows.net/docs/file.pdf")]
    [InlineData("https://example.com/file.pdf")]
    [InlineData("https://127.0.0.1/file.pdf")]
    [InlineData("not-a-url")]
    public async Task PreviewDocument_NonAllowlistedUrl_IsRejected(string url)
    {
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var controller = CreateController(factory.Object);

        var result = await controller.PreviewDocument(
            new DocumentPreviewRequest(url),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        factory.VerifyNoOtherCalls();
    }

    private static ChatController CreateController(IHttpClientFactory factory) => new(
        Mock.Of<IAiChatService>(),
        Mock.Of<IUserPreferencesStore>(),
        Mock.Of<ISecretProtector>(),
        factory);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(handler(request));
    }
}
