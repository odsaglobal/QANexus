using System;
using System.Collections.Generic;
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

    [Fact]
    public async Task Retries_without_response_format_when_local_server_rejects_it()
    {
        // Simulates an OpenAI-compatible local server (older llama.cpp/LM Studio build) that 400s on an
        // unrecognized "response_format" field. The client must drop it and retry as a plain completion
        // instead of failing the whole call.
        var handler = new SequencedHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"message":"Unrecognized request argument supplied: response_format"}}""",
                    Encoding.UTF8, "application/json"),
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "choices": [
                        { "message": { "role": "assistant", "content": "{\"summary\":\"ok\"}" } }
                      ]
                    }
                    """, Encoding.UTF8, "application/json"),
            });

        var factory = new StubHttpClientFactory(handler);
        var options = Options.Create(new LlmOptions
        {
            Provider = "openai",
            BaseUrl = "http://localhost:1234/v1",
            Model = "local-model",
            TimeoutMs = 10000,
        });

        var client = new LlmClient(factory, options, NullLogger<LlmClient>.Instance);

        var result = await client.CompleteAsync("system", "user");

        result.Should().Be("{\"summary\":\"ok\"}");
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task Drops_temperature_for_models_that_reject_it_and_remembers()
    {
        var handler = new SequencedHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"message":"Unsupported value: 'temperature' does not support 0.4 with this model. Only the default (1) value is supported."}}""",
                    Encoding.UTF8, "application/json"),
            },
            Ok("""{"choices":[{"message":{"content":"{\"a\":1}"}}]}"""),
            Ok("""{"choices":[{"message":{"content":"{\"a\":2}"}}]}"""));

        var client = new LlmClient(new StubHttpClientFactory(handler), OpenAi($"reasoning-{Guid.NewGuid():N}"), NullLogger<LlmClient>.Instance);

        (await client.CompleteAsync("s", "u")).Should().Be("{\"a\":1}");
        (await client.CompleteAsync("s", "u")).Should().Be("{\"a\":2}");

        handler.Bodies.Should().HaveCount(3);
        handler.Bodies[0].Should().Contain("\"temperature\"");
        handler.Bodies[1].Should().NotContain("\"temperature\"");
        handler.Bodies[2].Should().NotContain("\"temperature\"");
    }

    [Fact]
    public async Task Reads_content_given_as_an_array_of_text_parts()
    {
        var handler = new SequencedHttpMessageHandler(
            Ok("""{"choices":[{"message":{"content":[{"type":"text","text":"{\"a\":"},{"type":"text","text":"1}"}]}}]}"""));

        var client = new LlmClient(new StubHttpClientFactory(handler), OpenAi($"parts-{Guid.NewGuid():N}"), NullLogger<LlmClient>.Instance);

        (await client.CompleteAsync("s", "u")).Should().Be("{\"a\":1}");
    }

    [Fact]
    public async Task Falls_back_to_reasoning_content_when_content_is_empty()
    {
        var handler = new SequencedHttpMessageHandler(
            Ok("""{"choices":[{"message":{"content":"","reasoning_content":"thinking... {\"action\":\"click\"}"}}]}"""));

        var client = new LlmClient(new StubHttpClientFactory(handler), OpenAi($"r1-{Guid.NewGuid():N}"), NullLogger<LlmClient>.Instance);

        (await client.CompleteAsync("s", "u")).Should().Contain("{\"action\":\"click\"}");
    }

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static IOptions<LlmOptions> OpenAi(string model) => Options.Create(new LlmOptions
    {
        Provider = "openai",
        BaseUrl = "http://localhost:1234/v1",
        Model = model,
        TimeoutMs = 10000,
    });

    private sealed class SequencedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public int RequestCount { get; private set; }

        public List<string> Bodies { get; } = new();

        public SequencedHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
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
