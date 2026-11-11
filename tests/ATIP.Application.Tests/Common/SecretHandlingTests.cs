using System.Security.Cryptography;
using ATIP.Application.Common.Exceptions;
using ATIP.Application.Common.Interfaces;
using ATIP.Application.Common.Security;
using ATIP.Application.Features.Environments.Dtos;
using ATIP.Infrastructure.Configuration;
using ATIP.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace ATIP.Application.Tests.Common;

public class SecretHandlingTests
{
    private static AesGcmSecretProtector NewProtector() =>
        new(Options.Create(new EncryptionOptions
        {
            ActiveKeyId = "k1",
            Keys = new() { ["k1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
        }));

    [Fact]
    public void Redact_masks_every_occurrence_with_the_key_name()
    {
        var redactor = new SecretRedactor();
        redactor.Add("admin.password", "S3cret!pw");

        redactor.Redact("typed S3cret!pw then S3cret!pw again")
            .Should().Be("typed [secret:admin.password] then [secret:admin.password] again");
    }

    [Fact]
    public void Redact_masks_the_json_escaped_form_too()
    {
        var redactor = new SecretRedactor();
        redactor.Add("pw", "a\"b\\c");

        redactor.Redact("{\"value\":\"a\\\"b\\\\c\"}").Should().NotContain("a\\\"b");
    }

    [Fact]
    public void Redact_ignores_values_too_short_to_mask_safely()
    {
        var redactor = new SecretRedactor();
        redactor.Add("pin", "12");

        redactor.Redact("Showing 12 results").Should().Be("Showing 12 results");
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("Users.password", true)]
    [InlineData("apiKey", true)]
    [InlineData("otp", true)]
    [InlineData("username", false)]
    [InlineData("pincode", false)]
    public void LooksSecret_flags_credential_names(string key, bool expected)
    {
        SecretRedactor.LooksSecret(key).Should().Be(expected);
    }

    [Fact]
    public async Task Secret_variables_are_stored_encrypted_and_never_returned()
    {
        var protector = NewProtector();
        var json = EnvironmentVariableSerialization.SerializeStored(
            [
                new StoredEnvironmentVariable("username", "text", "alice", null),
                new StoredEnvironmentVariable("password", "secret", "hunter22", null),
            ],
            protector);

        json.Should().NotContain("hunter22");

        var display = EnvironmentVariableSerialization.Parse(json);
        display.Single(v => v.Key == "password").Should().Match<EnvironmentVariableDto>(v => v.Value == null && v.HasValue);
        display.Single(v => v.Key == "username").Value.Should().Be("alice");

        var secretKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var values = await EnvironmentVariableSerialization.ResolveValuesAsync(json, protector, null, Guid.NewGuid(), secretKeys);
        values["password"].Should().Be("hunter22");
        secretKeys.Should().BeEquivalentTo(["password"]);
    }

    [Fact]
    public async Task Legacy_plaintext_secret_is_encrypted_on_next_save()
    {
        var protector = NewProtector();
        const string legacy = """{"password":{"value":"hunter22","type":"secret"}}""";

        var stored = EnvironmentVariableSerialization.ParseStored(legacy);
        var json = EnvironmentVariableSerialization.SerializeStored(stored, protector);

        json.Should().NotContain("hunter22");
        (await EnvironmentVariableSerialization.ResolveValuesAsync(json, protector, null, Guid.NewGuid()))["password"].Should().Be("hunter22");
    }

    [Theory]
    [InlineData("flipkart/admin#password", "flipkart/admin", "password")]
    [InlineData("/flipkart/admin/", "flipkart/admin", "value")]
    [InlineData("a.b/c-d_e#f.g", "a.b/c-d_e", "f.g")]
    public void SecretReference_parses_path_and_field(string text, string path, string field)
    {
        SecretReference.TryParse(text, out var reference).Should().BeTrue();
        reference!.Path.Should().Be(path);
        reference.Field.Should().Be(field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../other-tenant/admin#password")]
    [InlineData("a//b#c")]
    [InlineData("a b#c")]
    [InlineData("a#")]
    public void SecretReference_rejects_unsafe_or_malformed_input(string text)
    {
        SecretReference.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Vault_variables_resolve_through_the_store_and_count_as_secrets()
    {
        var tenantId = Guid.NewGuid();
        var store = new Mock<ISecretStore>();
        store.SetupGet(s => s.IsConfigured).Returns(true);
        store.Setup(s => s.GetSecretAsync(tenantId, "app/admin#password", It.IsAny<CancellationToken>())).ReturnsAsync("from-vault");
        store.Setup(s => s.GetSecretAsync(tenantId, "app/missing#password", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SecretStoreException("not found"));

        const string json = """{"pw":{"value":"app/admin#password","type":"vault"},"gone":{"value":"app/missing#password","type":"vault"}}""";
        var secretKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<string>();

        var values = await EnvironmentVariableSerialization.ResolveValuesAsync(
            json, NewProtector(), store.Object, tenantId, secretKeys, (key, _) => unresolved.Add(key));

        values["pw"].Should().Be("from-vault");
        values["gone"].Should().BeNull();
        unresolved.Should().BeEquivalentTo(["gone"]);
        secretKeys.Should().BeEquivalentTo(["pw", "gone"]);
    }
}
