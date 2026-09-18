namespace ATIP.Domain.Enums;

/// <summary>
/// The system under test that a step drives. A scenario is not restricted to one platform: a
/// single flow can place an order in the browser, assert the REST response of the order API and
/// then verify the row that landed in the database, so the platform is carried per step (and per
/// action) rather than per scenario.
/// </summary>
public enum TestPlatform
{
    /// <summary>Browser automation (Playwright). The default for an unqualified step.</summary>
    Web = 0,

    /// <summary>HTTP/REST service calls.</summary>
    Api = 1,

    /// <summary>Native mobile automation over the W3C WebDriver protocol (Appium).</summary>
    Mobile = 2,

    /// <summary>Relational database queries used as setup, teardown or mid-flow validation.</summary>
    Database = 3
}
