using AgentLedger.Cli.Git;

namespace AgentLedger.Cli.Tests.Git;

// Parses `git log -z --name-status` with the CLI's own format (ADR 0021), shaped exactly as git 2.55 writes it.
public sealed class GitLogParserParse
{
  private const string A = "8bba2c597ec600b61bd7f88c9b37f708fb96de1f";
  private const string B = "87a3abb1ca0df9b6a63a441b80055244616d5de8";
  private const string C = "fdc107f5a527652ef0493fb0d4858f6cab976f20";

  private static string Record(string hash, string parents, string message, string files) =>
    $"\u001e{hash}\0{parents}\0" + "2026-10-26T01:55:44Z\0" + "2026-10-26T01:56:00+02:00\0" + $"{message}\0\0\n{files}";

  [Fact]
  public void ReadsEachCommitsFields()
  {
    var output = Record(A, B, "second\n\nbody line 1\nbody line 2\n", "A\0my file.txt\0");

    var commit = GitLogParser.Parse(output).ShouldHaveSingleItem();

    commit.Hash.ShouldBe(A);
    commit.Parents.ShouldBe([B]);
    commit.AuthoredAt.ShouldBe(new DateTimeOffset(2026, 10, 26, 1, 55, 44, TimeSpan.Zero));
    commit.CommittedAt.ShouldBe(new DateTimeOffset(2026, 10, 26, 1, 56, 0, TimeSpan.FromHours(2)));
    commit.Message.ShouldBe("second\n\nbody line 1\nbody line 2\n");
    commit.Files.ShouldBe([new ObservedFile("my file.txt", "added", null)]);
    commit.FilesTruncated.ShouldBeFalse();
  }

  [Fact]
  public void ReadsEveryKindOfChangeIncludingRenamesAndCopies()
  {
    var output = Record(A, B, "x\n", "M\0m.txt\0D\0d.txt\0T\0t.txt\0R100\0old.txt\0new.txt\0C075\0src.txt\0copy.txt\0");

    GitLogParser.Parse(output).Single().Files.ShouldBe([
      new ObservedFile("m.txt", "modified", null),
      new ObservedFile("d.txt", "deleted", null),
      new ObservedFile("t.txt", "typeChanged", null),
      new ObservedFile("new.txt", "renamed", "old.txt"),
      new ObservedFile("copy.txt", "copied", "src.txt")]);
  }

  [Fact]
  public void ReadsSeveralCommitsInOrderIncludingAMergeWithNoFiles()
  {
    var output = Record(A, B, "first\n", "A\0a.txt\0") + Record(C, $"{A} {B}", "Merge branch 'side'\n", "");

    var commits = GitLogParser.Parse(output);

    commits.Select(c => c.Hash).ShouldBe([A, C]);
    commits[1].Parents.ShouldBe([A, B]);
    commits[1].Files.ShouldBeEmpty();
  }

  [Fact]
  public void ReadsARootCommitWithNoParents()
  {
    GitLogParser.Parse(Record(A, "", "root\n", "A\0a.txt\0")).Single().Parents.ShouldBeEmpty();
  }

  [Fact]
  public void ListsAtMostTheFileLimitAndSaysSo()
  {
    var files = string.Concat(Enumerable.Range(0, GitLogParser.MaxFiles + 5).Select(i => $"A\0f{i}.txt\0"));

    var commit = GitLogParser.Parse(Record(A, B, "big\n", files)).Single();

    commit.Files.Count.ShouldBe(GitLogParser.MaxFiles);
    commit.FilesTruncated.ShouldBeTrue();
  }

  [Fact]
  public void ReadsNothingFromNothing()
  {
    GitLogParser.Parse("").ShouldBeEmpty();
  }
}
