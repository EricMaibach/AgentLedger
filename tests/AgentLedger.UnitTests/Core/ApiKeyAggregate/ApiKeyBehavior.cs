using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UnitTests.Core.ApiKeyAggregate;

public sealed class ApiKeyMatches
{
  [Fact]
  public void AcceptsItsOwnToken()
  {
    var builder = new ApiKeyBuilder();

    builder.Build().Matches(builder.Token).ShouldBeTrue();
  }

  [Fact]
  public void RejectsAWrongSecret()
  {
    var builder = new ApiKeyBuilder();
    var wrong = builder.Token with { Secret = new string('x', ApiKeyToken.SecretLength) };

    builder.Build().Matches(wrong).ShouldBeFalse();
  }

  [Fact]
  public void RejectsATokenForAnotherKey()
  {
    var builder = new ApiKeyBuilder();
    var other = builder.Token with { KeyId = ApiKeyId.From(Guid.CreateVersion7()) };

    builder.Build().Matches(other).ShouldBeFalse();
  }

  [Fact]
  public void RejectsATokenClaimingAnotherScope()
  {
    // The stored scope is what counts; a token edited to say "read" is not a read key.
    var builder = new ApiKeyBuilder().WithScope(ApiKeyScope.Ingest);
    var edited = builder.Token with { Scope = ApiKeyScope.Read };

    builder.Build().Matches(edited).ShouldBeFalse();
  }
}

public sealed class ApiKeyIsActive
{
  [Fact]
  public void IsActiveWithoutExpiryUntilRevoked()
  {
    var key = new ApiKeyBuilder().Build();

    key.IsActive(ApiKeyBuilder.Now.AddYears(10)).ShouldBeTrue();
  }

  [Fact]
  public void StopsAtItsExpiry()
  {
    var expiry = ApiKeyBuilder.Now.AddDays(30);
    var key = new ApiKeyBuilder().WithExpiresAt(expiry).Build();

    key.IsActive(expiry.AddTicks(-1)).ShouldBeTrue();
    key.IsActive(expiry).ShouldBeFalse();
  }

  [Fact]
  public void StopsWhenRevoked()
  {
    var key = new ApiKeyBuilder().Build();

    key.Revoke(UserId.From(Guid.CreateVersion7()), ApiKeyBuilder.Now);

    key.IsActive(ApiKeyBuilder.Now).ShouldBeFalse();
  }
}

public sealed class ApiKeyRevoke
{
  [Fact]
  public void RecordsWhoRevokedItAndWhen()
  {
    var key = new ApiKeyBuilder().Build();
    var admin = UserId.From(Guid.CreateVersion7());

    key.Revoke(admin, ApiKeyBuilder.Now.AddHours(1));

    key.RevokedBy.ShouldBe(admin);
    key.RevokedAt.ShouldBe(ApiKeyBuilder.Now.AddHours(1));
  }

  [Fact]
  public void KeepsTheFirstRevocation()
  {
    var key = new ApiKeyBuilder().Build();
    var first = UserId.From(Guid.CreateVersion7());
    key.Revoke(first, ApiKeyBuilder.Now.AddHours(1));

    key.Revoke(UserId.From(Guid.CreateVersion7()), ApiKeyBuilder.Now.AddHours(2));

    key.RevokedBy.ShouldBe(first);
    key.RevokedAt.ShouldBe(ApiKeyBuilder.Now.AddHours(1));
  }
}

// "Last used" is recorded at most once an hour (ADR 0019), so ingest doesn't write the key on every event.
public sealed class ApiKeyRecordUse
{
  [Fact]
  public void RecordsTheFirstUse()
  {
    var key = new ApiKeyBuilder().Build();

    key.RecordUse(ApiKeyBuilder.Now).ShouldBeTrue();
    key.LastUsedAt.ShouldBe(ApiKeyBuilder.Now);
  }

  [Fact]
  public void SkipsUsesWithinAnHour()
  {
    var key = new ApiKeyBuilder().Build();
    key.RecordUse(ApiKeyBuilder.Now);

    key.RecordUse(ApiKeyBuilder.Now.AddMinutes(59)).ShouldBeFalse();
    key.LastUsedAt.ShouldBe(ApiKeyBuilder.Now);
  }

  [Fact]
  public void RecordsAgainAfterAnHour()
  {
    var key = new ApiKeyBuilder().Build();
    key.RecordUse(ApiKeyBuilder.Now);

    key.RecordUse(ApiKeyBuilder.Now.AddHours(1)).ShouldBeTrue();
    key.LastUsedAt.ShouldBe(ApiKeyBuilder.Now.AddHours(1));
  }
}

// The CLI version a key's machine runs (ADR 0024), recorded with "last used": at once when it changes, so an update
// shows on the machines page without waiting an hour.
public sealed class ApiKeyRecordUseWithClientVersion
{
  [Fact]
  public void RecordsTheVersion()
  {
    var key = new ApiKeyBuilder().Build();

    key.RecordUse(ApiKeyBuilder.Now, "0.6.0").ShouldBeTrue();
    key.LastClientVersion.ShouldBe("0.6.0");
  }

  [Fact]
  public void RecordsANewVersionWithinTheHour()
  {
    var key = new ApiKeyBuilder().Build();
    key.RecordUse(ApiKeyBuilder.Now, "0.6.0");

    key.RecordUse(ApiKeyBuilder.Now.AddMinutes(5), "0.6.1").ShouldBeTrue();
    (key.LastClientVersion, key.LastUsedAt).ShouldBe(("0.6.1", (DateTimeOffset?)ApiKeyBuilder.Now.AddMinutes(5)));
  }

  [Fact]
  public void KeepsTheVersionWhenARequestHasNone()
  {
    // Scripts using a read key send no CLI version: that doesn't erase what the CLI reported.
    var key = new ApiKeyBuilder().Build();
    key.RecordUse(ApiKeyBuilder.Now, "0.6.0");

    key.RecordUse(ApiKeyBuilder.Now.AddHours(2), null).ShouldBeTrue();
    key.LastClientVersion.ShouldBe("0.6.0");
  }

  [Fact]
  public void SkipsTheSameVersionWithinTheHour()
  {
    var key = new ApiKeyBuilder().Build();
    key.RecordUse(ApiKeyBuilder.Now, "0.6.0");

    key.RecordUse(ApiKeyBuilder.Now.AddMinutes(5), "0.6.0").ShouldBeFalse();
  }

  [Fact]
  public void ShortensAnOverlongVersion()
  {
    var key = new ApiKeyBuilder().Build();

    key.RecordUse(ApiKeyBuilder.Now, new string('9', 200));

    key.LastClientVersion!.Length.ShouldBe(ApiKey.MaxClientVersionLength);
  }
}

public sealed class ApiKeySecretHashOf
{
  [Fact]
  public void IsAStableSha256InHex()
  {
    ApiKeySecretHash.Of("abc").Value.ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
  }

  [Fact]
  public void NeverContainsTheSecret()
  {
    ApiKeySecretHash.Of("my-secret").Value.ShouldNotContain("my-secret");
  }
}
