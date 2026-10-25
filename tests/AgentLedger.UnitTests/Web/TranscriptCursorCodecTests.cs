using AgentLedger.UseCases.Transcripts;
using AgentLedger.Web.Transcripts;

namespace AgentLedger.UnitTests.Web;

// The transcript paging cursor on the wire: opaque to clients (ADR 0016).
public sealed class TranscriptCursorCodecTests
{
  [Fact]
  public void RoundTrips()
  {
    var cursor = new TranscriptLineCursor(1_234_567, Guid.CreateVersion7());

    TranscriptCursorCodec.TryDecode(TranscriptCursorCodec.Encode(cursor), out var decoded).ShouldBeTrue();
    decoded.ShouldBe(cursor);
  }

  [Fact]
  public void IsUrlSafe()
  {
    TranscriptCursorCodec.Encode(new TranscriptLineCursor(long.MaxValue, Guid.CreateVersion7())).ShouldMatch("^[A-Za-z0-9_-]+$");
  }

  [Theory]
  [InlineData("not-a-cursor")]
  [InlineData("")]
  [InlineData("LTE6MDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDA")] // "-1:000…": a negative offset
  public void RejectsAnythingElse(string value)
  {
    TranscriptCursorCodec.TryDecode(value, out _).ShouldBeFalse();
  }
}
