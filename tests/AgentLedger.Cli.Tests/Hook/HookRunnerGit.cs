using System.Text.Json;
using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Git;
using AgentLedger.Cli.Hook;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;
using AgentLedger.Cli.Tests.Git;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.Cli.Tests.Hook;

// A hook also records new commits in the project's repository, after its event (ADR 0021).
public sealed class HookRunnerGit : IDisposable
{
  private readonly TempGitRepository _repo = new();
  private readonly TempDirectory _data = new();
  private readonly FakeSender _eventSender = new();
  private readonly FakeSender _gitSender = new();
  private readonly FakeLog _log = new();
  private readonly EventSpool _gitSpool;

  public HookRunnerGit() => _gitSpool = new EventSpool(Path.Combine(_data.Path, "spool", "git"), SpoolLimits.Default);

  public void Dispose()
  {
    _repo.Dispose();
    _data.Dispose();
  }

  private Task RunAsync()
  {
    var clock = new FakeTimeProvider(DateTimeOffset.UtcNow.AddMinutes(-1));
    var git = new GitPipeline(
      new GitCommitCatchUp(new ProcessGitRunner(), new GitRepositoryStateStore(Path.Combine(_data.Path, "git")), _gitSpool, _log, clock, "devbox"),
      new SpoolDrainer(_gitSpool, _gitSender, _log, itemName: "git batch"));
    var runner = new HookRunner(
      new EnvelopeBuilder(new FakeHostEnvironment { CurrentDirectory = _repo.Path }, clock),
      new EventSpool(Path.Combine(_data.Path, "spool"), SpoolLimits.Default), _eventSender, _log, resendLimit: 5, transcripts: null, git: git,
      projectDirectory: _repo.Path);
    return runner.RunAsync("claude-code", """{"session_id":"s1","hook_event_name":"Stop"}""", CancellationToken.None);
  }

  [Fact]
  public async Task SendsTheEventAndNewCommits()
  {
    _repo.Commit("before");
    await RunAsync(); // baseline
    var commit = _repo.Commit("Add login", date: DateTimeOffset.UtcNow);

    await RunAsync();

    _eventSender.Sent.Count.ShouldBe(2);
    var batch = JsonDocument.Parse(_gitSender.Sent.ShouldHaveSingleItem()).RootElement;
    batch.GetProperty("commits")[0].GetProperty("hash").GetString().ShouldBe(commit);
    _gitSpool.List().ShouldBeEmpty();
  }

  [Fact]
  public async Task KeepsGitBatchesWhenTheApiIsUnavailable()
  {
    _repo.Commit("before");
    await RunAsync();
    _repo.Commit("Add login", date: DateTimeOffset.UtcNow);
    _eventSender.Respond = _ => new SendOutcome(SendResult.Failed, "Connection refused");

    await RunAsync();

    _gitSender.Sent.ShouldBeEmpty();
    _gitSpool.List().ShouldHaveSingleItem();
  }
}
