using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.UnitTests.Core.AgentEventReceiptAggregate;

public sealed class CaptureContextConstructor
{
  // ParamName is the camelCase request field, which the ingest handler reports as the error's identifier.
  [Theory]
  [InlineData(null, "eric", "/workspace", "host")]
  [InlineData(" ", "eric", "/workspace", "host")]
  [InlineData("devbox", null, "/workspace", "user")]
  [InlineData("devbox", "", "/workspace", "user")]
  [InlineData("devbox", "eric", null, "projectDir")]
  [InlineData("devbox", "eric", "", "projectDir")]
  public void RejectsMissingRequiredValues(string? host, string? user, string? projectDir, string expectedParam)
  {
    var ex = Assert.ThrowsAny<ArgumentException>(() => new CaptureContext(host!, user!, projectDir!, null, null, null));

    ex.ParamName.ShouldBe(expectedParam);
  }
}
