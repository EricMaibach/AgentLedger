using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using static AgentLedger.UnitTests.UseCases.Projections.ClaudeCode.ClaudeCodeSession;

namespace AgentLedger.UnitTests.UseCases.Projections.ClaudeCode;

// How the Claude Code adapter builds steps from transcript blocks and hook events (ADR 0022).
public sealed class ClaudeCodeAdapterSteps
{
  [Fact]
  public void StepsInTimeOrder()
  {
    var turn = new ClaudeCodeSession()
      .PromptLine("p-1", "Go", 0)
      .Thinking("msg_1", 1).ToolUse("msg_1", 2, "toolu_1").ToolResult("p-1", "toolu_1", 3)
      .Text("msg_2", 4, "Done.")
      .Project().Turns.Single();

    turn.Steps.Select(s => s.Kind).ShouldBe([StepKind.Prompt, StepKind.Thinking, StepKind.ToolCall, StepKind.Response]);
  }

  [Fact]
  public void ThinkingWithoutTextIsPresent()
  {
    var s = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Thinking("msg_1", 1);
    var thinking = s.Project().Turns.Single().Steps[1];

    thinking.Kind.ShouldBe(StepKind.Thinking);
    thinking.Text.ShouldBeNull();
    thinking.Id.ShouldBe(StepIdFor(StepKind.Thinking, $"{s.Lines[1].Id.Value}#0"));
    thinking.ResponseId.ShouldBe(ModelResponseId.For(AgentKind.ClaudeCode, Id, "msg_1"));
  }

  [Fact]
  public void ThinkingSummaryIsKept()
  {
    new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Thinking("msg_1", 1, "Checking the cart first.")
      .Project().Turns.Single().Steps[1].Text.ShouldBe("Checking the cart first.");
  }

  [Fact]
  public void TextIsAResponse()
  {
    var response = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Text("msg_1", 1, "Done.").Project().Turns.Single().Steps[1];

    (response.Kind, response.Text, response.StartedAt).ShouldBe((StepKind.Response, "Done.", At(1)));
  }

  [Fact]
  public void EmptyTextIsNoStep()
  {
    new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Text("msg_1", 1, "").Project().Turns.Single().Steps.Count.ShouldBe(1);
  }

  [Fact]
  public void ToolUseAndResultAreOneToolCall()
  {
    var s = new ClaudeCodeSession().PromptLine("p-1", "Go", 0)
      .ToolUse("msg_1", 1, "toolu_1", "Read", """{"file_path":"/a.txt"}""").ToolResult("p-1", "toolu_1", 4, "line one");
    var call = s.Project().Turns.Single().Steps[1];

    call.Id.ShouldBe(StepIdFor(StepKind.ToolCall, "toolu_1"));
    (call.ToolName, call.ToolUseId, call.ToolInput, call.ToolOutput, call.Succeeded)
      .ShouldBe(("Read", "toolu_1", """{"file_path":"/a.txt"}""", "line one", (bool?)true));
    (call.StartedAt, call.EndedAt).ShouldBe((At(1), (DateTimeOffset?)At(4)));
    call.Lineage.LineIds.ShouldBe([s.Lines[1].Id, s.Lines[2].Id]);
  }

  [Theory]
  [InlineData("Bash", """{"command":"dotnet test\ndotnet format","description":"Run tests"}""", "dotnet test")]
  [InlineData("Read", """{"file_path":"/workspace/src/Cart.cs","limit":20}""", "/workspace/src/Cart.cs")]
  [InlineData("Edit", """{"file_path":"/workspace/src/Cart.cs","old_string":"a","new_string":"b"}""", "/workspace/src/Cart.cs")]
  [InlineData("Write", """{"file_path":"/workspace/docs/a.md","content":"x"}""", "/workspace/docs/a.md")]
  [InlineData("NotebookEdit", """{"notebook_path":"/w/a.ipynb","new_source":"x"}""", "/w/a.ipynb")]
  [InlineData("Grep", """{"pattern":"TODO","path":"src"}""", "TODO")]
  [InlineData("Glob", """{"pattern":"**/*.cs"}""", "**/*.cs")]
  [InlineData("WebFetch", """{"url":"https://example.com","prompt":"Summarize"}""", "https://example.com")]
  [InlineData("WebSearch", """{"query":"blazor bunit"}""", "blazor bunit")]
  [InlineData("Agent", """{"description":"Research tracing","prompt":"…","subagent_type":"general-purpose"}""", "Research tracing")]
  [InlineData("Skill", """{"skill":"web-ui"}""", "web-ui")]
  [InlineData("mcp__docs__search", """{"limit":5,"q":"pricing"}""", "pricing")]
  [InlineData("SomethingNew", """{"count":3,"flag":true}""", null)]
  public void ToolCallsAreSummarizedByTheirKeyArgument(string tool, string input, string? summary)
  {
    new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 1, "toolu_1", tool, input)
      .Project().Turns.Single().Steps[1].Summary.ShouldBe(summary);
  }

  [Fact]
  public void ErrorResultMeansFailed()
  {
    new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 1, "toolu_1").ToolResult("p-1", "toolu_1", 2, "boom", isError: true)
      .Project().Turns.Single().Steps[1].Succeeded.ShouldBe(false);
  }

  [Fact]
  public void ResultWithoutErrorFlagSucceeded()
  {
    new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 1, "toolu_1").ToolResult("p-1", "toolu_1", 2, isError: null)
      .Project().Turns.Single().Steps[1].Succeeded.ShouldBe(true);
  }

  [Fact]
  public void ResultBlocksBecomeTheirText()
  {
    new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 1, "toolu_1")
      .ToolResult("p-1", "toolu_1", 2, "Found 3 tools", contentAsBlocks: true)
      .Project().Turns.Single().Steps[1].ToolOutput.ShouldBe("Found 3 tools");
  }

  [Fact]
  public void HooksAddTimingAndEvidence()
  {
    // The transcript holds what the model saw; hooks say when the tool actually ran.
    var s = new ClaudeCodeSession().PromptLine("p-1", "Go", 0)
      .ToolUse("msg_1", 1, "toolu_1").PreToolUse("p-1", "toolu_1", 2).PostToolUse("p-1", "toolu_1", 5)
      .ToolResult("p-1", "toolu_1", 5.1);
    var call = s.Project().Turns.Single().Steps[1];

    (call.StartedAt, call.EndedAt, call.ToolOutput).ShouldBe((At(1), (DateTimeOffset?)At(5), "a.txt"));
    call.Lineage.EventIds.ShouldBe([s.Receipts[0].EventId, s.Receipts[1].EventId]);
  }

  [Fact]
  public void FailureEventMeansFailed()
  {
    new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 1, "toolu_1").PostToolUseFailure("p-1", "toolu_1", 2)
      .Project().Turns.Single().Steps[1].Succeeded.ShouldBe(false);
  }

  [Fact]
  public void ToolCallFromHooksAlone()
  {
    var call = new ClaudeCodeSession().UserPromptSubmit("p-1", "Go", 0)
      .PreToolUse("p-1", "toolu_1", 1, "Grep", """{"pattern":"cart"}""").PostToolUse("p-1", "toolu_1", 2, "Grep", """{"numFiles":2}""")
      .Project().Turns.Single().Steps[1];

    (call.ToolName, call.ToolInput, call.ToolOutput, call.Succeeded, call.StartedAt, call.EndedAt)
      .ShouldBe(("Grep", """{"pattern":"cart"}""", """{"numFiles":2}""", (bool?)true, At(1), (DateTimeOffset?)At(2)));
  }

  [Fact]
  public void FailedToolCallFromHooksAloneHasTheError()
  {
    var call = new ClaudeCodeSession().UserPromptSubmit("p-1", "Go", 0)
      .PreToolUse("p-1", "toolu_1", 1).PostToolUseFailure("p-1", "toolu_1", 2, "Exit code 2")
      .Project().Turns.Single().Steps[1];

    (call.ToolOutput, call.Succeeded).ShouldBe(("Exit code 2", (bool?)false));
  }

  [Fact]
  public void ToolCallWithoutAResultHasNone()
  {
    var call = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 1, "toolu_1").Project().Turns.Single().Steps[1];

    (call.ToolOutput, call.Succeeded, call.EndedAt).ShouldBe((null, null, null));
  }

  [Fact]
  public void ResentEventsCountOnce()
  {
    var s = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 1, "toolu_1")
      .PreToolUse("p-1", "toolu_1", 2).ResendLastEvent();
    var call = s.Project().Turns.Single().Steps[1];

    call.Lineage.EventIds.ShouldBe([s.Receipts[0].EventId]);
  }

  [Fact]
  public void ToolEventsWithoutAPromptIdMatchByToolUseId()
  {
    // Events from older Claude Code versions carry no prompt_id.
    var s = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).ToolUse("msg_1", 1, "toolu_1");
    s.Hook("PostToolUse", 4, new System.Text.Json.Nodes.JsonObject
    {
      ["tool_use_id"] = "toolu_1",
      ["tool_name"] = "Bash",
      ["tool_response"] = new System.Text.Json.Nodes.JsonObject(),
    });
    var call = s.Project().Turns.Single().Steps[1];

    (call.EndedAt, call.Succeeded).ShouldBe(((DateTimeOffset?)At(4), (bool?)true));
    call.Lineage.EventIds.ShouldBe([s.Receipts[0].EventId]);
  }

  [Fact]
  public void SubagentToolEventsAreNotStepsOfTheTurn()
  {
    // A subagent's events carry the launching turn's prompt_id plus its agent_id; they become the subagent's own
    // nested steps (build step 3), not tool calls of the turn.
    var s = new ClaudeCodeSession().PromptLine("p-1", "Go", 0);
    s.Hook("PreToolUse", 2, new System.Text.Json.Nodes.JsonObject
    {
      ["prompt_id"] = "p-1",
      ["agent_id"] = "a1",
      ["tool_use_id"] = "toolu_sub",
      ["tool_name"] = "Read",
    });

    s.Project().Turns.Single().Steps.Count.ShouldBe(1);
  }

  [Fact]
  public void IgnoresWhatStepOneDoesNotProject()
  {
    // Subagent files, lines that aren't JSON, and line types without steps yet (build step 3).
    var turn = new ClaudeCodeSession().PromptLine("p-1", "Go", 0)
      .RawLine("not json")
      .RawLine("""{"type":"attachment","timestamp":"2026-10-26T14:00:01.000Z","attachment":{"type":"date"}}""")
      .RawLine("""{"type":"user","promptId":"p-9","timestamp":"2026-10-26T14:00:02.000Z","message":{"role":"user","content":"Sub task"}}""",
        file: "subagents/agent-a1")
      .Project().Turns.Single();

    turn.Steps.Count.ShouldBe(1);
  }
}

// Token usage: one model response, counted once (ADR 0022).
public sealed class ClaudeCodeAdapterResponses
{
  [Fact]
  public void OneResponsePerMessageIdWithItsTokens()
  {
    var usage = new Usage(2, 836, 0, 56, 746_742, 81, WebSearches: 1);
    var s = new ClaudeCodeSession().PromptLine("p-1", "Go", 0)
      .Thinking("msg_1", 1, usage: usage).ToolUse("msg_1", 2, "toolu_1", usage: usage);
    var response = s.Project().Turns.Single().Responses.Single();

    (response.MessageId, response.Model, response.RespondedAt).ShouldBe(("msg_1", "claude-opus-5-5", At(1)));
    response.Usage.ShouldBe(usage.ToTokenUsage());
    response.Lineage.LineIds.ShouldBe([s.Lines[1].Id, s.Lines[2].Id]);
  }

  [Fact]
  public void TokensComeFromTheResponsesLastLine()
  {
    // A line written while the response was still streaming can carry lower counts (seen once in 1,184 responses).
    var response = new ClaudeCodeSession().PromptLine("p-1", "Go", 0)
      .Thinking("msg_1", 1, usage: Usage.Default with { Output = 6 })
      .Text("msg_1", 2, "Done.", Usage.Default with { Output = 443 })
      .Project().Turns.Single().Responses.Single();

    response.Usage.Output.ShouldBe(443);
  }

  [Fact]
  public void SyntheticMessagesAreNotModelResponses()
  {
    // Claude Code writes some messages itself (model "<synthetic>"): text, but no API call or tokens.
    var turn = new ClaudeCodeSession().PromptLine("p-1", "Go", 0).Text("msg_1", 1, "No response requested.", model: "<synthetic>")
      .Project().Turns.Single();

    turn.Responses.ShouldBeEmpty();
    turn.Steps[1].ResponseId.ShouldBeNull();
  }
}
