using AgentLedger.Core.ApiKeyAggregate;

namespace AgentLedger.UnitTests.Core.ApiKeyAggregate;

public sealed class Base62Encode
{
  [Fact]
  public void UsesOnlyLettersAndDigitsAtAFixedWidth()
  {
    // Fixed width: the token's parts have known lengths, whatever the bytes are.
    Base62.Encode(new byte[16], 22).ShouldBe(new string('0', 22));
    Base62.Encode(Enumerable.Repeat((byte)0xFF, 16).ToArray(), 22).ShouldMatch("^[0-9A-Za-z]{22}$");
  }

  [Fact]
  public void RoundTrips()
  {
    var bytes = Guid.CreateVersion7().ToByteArray();

    Base62.TryDecode(Base62.Encode(bytes, 22), 16, out var decoded).ShouldBeTrue();
    decoded.ShouldBe(bytes);
  }

  [Theory]
  [InlineData("not-base62!")]
  [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")] // too large for 16 bytes
  public void RejectsTextThatIsNotBase62OfTheRightSize(string text)
  {
    Base62.TryDecode(text, 16, out _).ShouldBeFalse();
  }
}
