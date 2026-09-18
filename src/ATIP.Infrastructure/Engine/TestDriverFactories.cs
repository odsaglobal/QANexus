using ATIP.Application.Common.Interfaces;
using ATIP.Application.Engine.Contracts;
using ATIP.Domain.Enums;
using ATIP.Infrastructure.Engine.Api;
using ATIP.Infrastructure.Engine.Database;
using ATIP.Infrastructure.Engine.Mobile;
using ATIP.Infrastructure.Engine.Web;
using ATIP.Infrastructure.Exploration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATIP.Infrastructure.Engine;

/// <summary>Creates the browser driver, reusing an already-open browser when one was handed over.</summary>
public sealed class WebDriverFactory : ITestDriverFactory
{
    private readonly WebBrowserSession _session;
    private readonly ILocatorResolver _resolver;
    private readonly ILoggerFactory _loggers;

    public WebDriverFactory(WebBrowserSession session, ILocatorResolver resolver, ILoggerFactory loggers)
    {
        _session = session;
        _resolver = resolver;
        _loggers = loggers;
    }

    public TestPlatform Platform => TestPlatform.Web;

    public ITestDriver Create()
    {
        var logger = _loggers.CreateLogger<PlaywrightWebDriver>();

        return _session.Browser is { } existing
            // Borrowed: the explorer still owns its lifetime, so the driver must not dispose it.
            ? new PlaywrightWebDriver(existing, _resolver, logger, ownsBrowser: false)
            : new PlaywrightWebDriver(new PlaywrightBrowserService(), _resolver, logger, ownsBrowser: true);
    }
}

public sealed class ApiDriverFactory : ITestDriverFactory
{
    private readonly IHttpClientFactory _http;
    private readonly ILoggerFactory _loggers;

    public ApiDriverFactory(IHttpClientFactory http, ILoggerFactory loggers)
    {
        _http = http;
        _loggers = loggers;
    }

    public TestPlatform Platform => TestPlatform.Api;

    public ITestDriver Create() =>
        new HttpApiDriver(_http.CreateClient("atip-test-api"), _loggers.CreateLogger<HttpApiDriver>());
}

public sealed class DatabaseDriverFactory : ITestDriverFactory
{
    private readonly IApplicationDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ILoggerFactory _loggers;

    public DatabaseDriverFactory(IApplicationDbContext db, ISecretProtector protector, ILoggerFactory loggers)
    {
        _db = db;
        _protector = protector;
        _loggers = loggers;
    }

    public TestPlatform Platform => TestPlatform.Database;

    public ITestDriver Create() =>
        new SqlDataDriver(_db, _protector, _loggers.CreateLogger<SqlDataDriver>());
}

public sealed class MobileDriverFactory : ITestDriverFactory
{
    private readonly IHttpClientFactory _http;
    private readonly IOptions<MobileDriverOptions> _options;
    private readonly ILoggerFactory _loggers;

    public MobileDriverFactory(IHttpClientFactory http, IOptions<MobileDriverOptions> options, ILoggerFactory loggers)
    {
        _http = http;
        _options = options;
        _loggers = loggers;
    }

    public TestPlatform Platform => TestPlatform.Mobile;

    public ITestDriver Create() =>
        new AppiumMobileDriver(_http.CreateClient("atip-appium"), _options, _loggers.CreateLogger<AppiumMobileDriver>());
}
