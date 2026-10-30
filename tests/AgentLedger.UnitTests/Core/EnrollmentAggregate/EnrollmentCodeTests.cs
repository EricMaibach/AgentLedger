using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UnitTests.Core.EnrollmentAggregate;

// The one-time codes that connect a machine (ADR 0024): shown once, stored only as a hash, usable once for ten
// minutes.
public sealed class EnrollmentCodeTextGenerate
{
  [Fact]
  public void IsEightCharactersWithoutLookAlikes()
  {
    var text = EnrollmentCodeText.Generate(Enumerable.Range(0, 64).Select(i => (byte)i).ToArray());

    text.Value.Length.ShouldBe(9); // XXXX-XXXX
    text.Value[4].ShouldBe('-');
    text.Value.Replace("-", "").ShouldAllBe(c => EnrollmentCodeText.Alphabet.Contains(c));
    EnrollmentCodeText.Alphabet.ShouldNotContain('0');
    EnrollmentCodeText.Alphabet.ShouldNotContain('O');
    EnrollmentCodeText.Alphabet.ShouldNotContain('1');
    EnrollmentCodeText.Alphabet.ShouldNotContain('I');
    EnrollmentCodeText.Alphabet.ShouldNotContain('L');
  }

  [Fact]
  public void DiffersWithTheRandomBytes()
  {
    var a = EnrollmentCodeText.Generate(new byte[64]);
    var b = EnrollmentCodeText.Generate(Enumerable.Repeat((byte)7, 64).ToArray());

    a.ShouldNotBe(b);
  }

  [Fact]
  public void IsUnbiasedOverItsAlphabet()
  {
    // Rejection sampling: bytes at or above the largest multiple of the alphabet's size are skipped, so every
    // character is equally likely.
    // 249 would otherwise be the alphabet's second character (249 % 31 == 1); skipped, the zeros after it give the first.
    var tooHigh = (byte)(256 - 256 % EnrollmentCodeText.Alphabet.Length + 1);
    var bytes = Enumerable.Repeat(tooHigh, 8).Concat(Enumerable.Repeat((byte)0, 56)).ToArray();

    EnrollmentCodeText.Generate(bytes).Value.ShouldBe("AAAA-AAAA");
  }
}

public sealed class EnrollmentCodeHashOf
{
  [Theory]
  [InlineData("K7PX-4QHM")]
  [InlineData("k7px-4qhm")]
  [InlineData(" K7PX 4QHM ")]
  [InlineData("K7PX4QHM")]
  public void IgnoresCaseSpacesAndTheDash(string typed)
  {
    // People retype codes: what they meant is what counts.
    EnrollmentCodeHash.Of(typed).ShouldBe(EnrollmentCodeHash.Of("K7PX-4QHM"));
  }

  [Fact]
  public void IsASha256InHex()
  {
    EnrollmentCodeHash.Of("K7PX-4QHM").Value.Length.ShouldBe(64);
    EnrollmentCodeHash.Of("K7PX-4QHM").ShouldNotBe(EnrollmentCodeHash.Of("K7PX-4QHN"));
  }
}

public sealed class EnrollmentCodeUse
{
  private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
  private static readonly ApiKeyId Key = ApiKeyId.From(Guid.CreateVersion7());

  private static EnrollmentCode Code() =>
    new(EnrollmentCodeId.From(Guid.CreateVersion7()), UserId.From(Guid.CreateVersion7()), EnrollmentCodeHash.Of("K7PX-4QHM"), Now);

  [Fact]
  public void ExpiresTenMinutesAfterItWasCreated()
  {
    var code = Code();

    code.ExpiresAt.ShouldBe(Now.AddMinutes(10));
    code.IsUsable(Now.AddMinutes(10).AddTicks(-1)).ShouldBeTrue();
    code.IsUsable(Now.AddMinutes(10)).ShouldBeFalse();
  }

  [Fact]
  public void RecordsWhereAndForWhichKeyItWasUsed()
  {
    var code = Code();

    code.Use(Now.AddMinutes(1), "  devbox  ", Key);

    (code.UsedAt, code.UsedByHost, code.ApiKeyId).ShouldBe(((DateTimeOffset?)Now.AddMinutes(1), "devbox", (ApiKeyId?)Key));
    code.IsUsable(Now.AddMinutes(2)).ShouldBeFalse();
  }

  [Fact]
  public void CannotBeUsedTwice()
  {
    var code = Code();
    code.Use(Now, "devbox", Key);

    Assert.ThrowsAny<InvalidOperationException>(() => code.Use(Now, "other", ApiKeyId.From(Guid.CreateVersion7())));
  }

  [Fact]
  public void CannotBeUsedAfterItExpires()
  {
    Assert.ThrowsAny<InvalidOperationException>(() => Code().Use(Now.AddMinutes(11), "devbox", Key));
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void NeedsAHost(string host)
  {
    Assert.ThrowsAny<ArgumentException>(() => Code().Use(Now, host, Key));
  }

  [Fact]
  public void KeepsHostsShort()
  {
    Assert.ThrowsAny<ArgumentException>(() => Code().Use(Now, new string('h', EnrollmentCode.MaxHostLength + 1), Key));
  }
}
