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

  [Theory]
  [InlineData("2376609a3f019662cf2f5324c09cdb4b61e3be1e")]                          // SHA-1
  [InlineData("2376609a3f019662cf2f5324c09cdb4b61e3be1e2376609a3f019662cf2f5324")]  // SHA-256
  public void KeepsTheCheckedOutCommit(string commit)
  {
    // The commit the work was based on (ADR 0021).
    new CaptureContext("devbox", "eric", "/workspace", null, "main", null, commit).GitCommit.ShouldBe(commit);
  }

  [Fact]
  public void AllowsNoCommit()
  {
    new CaptureContext("devbox", "eric", "/workspace", null, null, null).GitCommit.ShouldBeNull();
  }

  [Theory]
  [InlineData("main")]
  [InlineData("2376609")]                                    // abbreviated
  [InlineData("2376609A3F019662CF2F5324C09CDB4B61E3BE1E")]   // git writes lowercase
  public void RejectsSomethingThatIsNotACommitHash(string commit)
  {
    var ex = Assert.ThrowsAny<ArgumentException>(() => new CaptureContext("devbox", "eric", "/workspace", null, "main", null, commit));

    ex.ParamName.ShouldBe("gitCommit");
  }
}
