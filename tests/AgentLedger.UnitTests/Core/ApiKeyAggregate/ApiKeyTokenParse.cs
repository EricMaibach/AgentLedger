using AgentLedger.Core.ApiKeyAggregate;

namespace AgentLedger.UnitTests.Core.ApiKeyAggregate;

// agl_<scope>_<key ID>_<secret> (ADR 0019).
public sealed class ApiKeyTokenParse
{
  private static readonly string Secret = new('a', ApiKeyToken.SecretLength);

  [Fact]
  public void RoundTripsThroughText()
  {
    var token = new ApiKeyToken(ApiKeyScope.Read, ApiKeyId.From(Guid.CreateVersion7()), Secret);

    ApiKeyToken.TryParse(token.Value, out var parsed).ShouldBeTrue();
    parsed.ShouldBe(token);
  }

  [Fact]
  public void StartsWithThePrefixAndTheScope()
  {
    var token = new ApiKeyToken(ApiKeyScope.Ingest, ApiKeyId.From(Guid.CreateVersion7()), Secret);

    token.Value.ShouldStartWith("agl_ingest_");
    token.Value.ShouldMatch("^agl_ingest_[0-9A-Za-z]{22}_[0-9A-Za-z]{43}$");
  }

  [Theory]
  [InlineData("")]
  [InlineData("ghp_abc")]
  [InlineData("agl_admin_0000000000000000000001_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // unknown scope
  [InlineData("agl_ingest_short_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]                 // bad key ID
  [InlineData("agl_ingest_0000000000000000000001_short")]                                     // bad secret
  [InlineData("agl_ingest_0000000000000000000001_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa_x")] // extra part
  [InlineData("agl_ingest_0000000000000000000000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // empty key ID
  public void RejectsAnythingElse(string text)
  {
    ApiKeyToken.TryParse(text, out _).ShouldBeFalse();
  }

  [Fact]
  public void DoesNotRevealTheSecretInToString()
  {
    var token = new ApiKeyToken(ApiKeyScope.Ingest, ApiKeyId.From(Guid.CreateVersion7()), Secret);

    token.ToString().ShouldNotContain(Secret);
  }
}
