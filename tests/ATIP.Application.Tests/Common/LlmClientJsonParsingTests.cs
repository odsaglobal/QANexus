using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ATIP.Infrastructure.Ai;
using ATIP.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ATIP.Application.Tests.Common;

public class LlmClientJsonParsingTests
{
    [Fact]
    public async Task Extracts_content_from_azure_result_payload_without_recursing_into_result()
    {
        var payload = """
        {
          "id": "chatcmpl-123",
          "object": "chat.completion",
          "created": 0,
          "model": "gpt-4o",
          "result": {
            "id": "chatcmpl-456",
            "object": "chat.completion",
            "result": {
              "id": "chatcmpl-789",
              "object": "chat.completion",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": "{\"summary\":\"ok\"}"
                  },
                  "finish_reason": "stop"
                }
              ]
            }
          },
          "choices": [
            {
              "index": 0,
              "message": {
                "role": "assistant",
                "content": "{\"summary\":\"ok\"}"
              },
              "finish_reason": "stop"
            }
          ]
        }
        """;

        using var httpHandler = new StubHttpMessageHandler(payload);
        var factory = new StubHttpClientFactory(httpHandler);
        var options = Options.Create(new LlmOptions
        {
            Provider = "azure",
            Endpoint = "https://example.test",
            ApiKey = "test-key",
            Model = "gpt-4o",
            TimeoutMs = 10000,
        });

        var client = new LlmClient(factory, options, NullLogger<LlmClient>.Instance);

        var result = await client.CompleteAsync("system", "user");

        result.Should().Be("{\"summary\":\"ok\"}");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://example.test/")
            };
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _payload;

        public StubHttpMessageHandler(string payload)
        {
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
