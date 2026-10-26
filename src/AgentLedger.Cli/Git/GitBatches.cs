using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AgentLedger.Cli.Git;

/// <summary>A repository's observed commits and tag changes in the server's format for POST /git/observations (ADR 0021).</summary>
internal static class GitBatches
{
  // Not for HTML: only the ledger reads this. Leaving non-ASCII unescaped keeps batches small.
  private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

  public static string ToJson(string repository, IReadOnlyList<ObservedCommit> commits, IReadOnlyList<ObservedTag> tags)
  {
    using var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer, Options))
    {
      writer.WriteStartObject();
      writer.WriteString("repository", repository);
      writer.WriteStartArray("commits");
      foreach (var commit in commits)
      {
        writer.WriteStartObject();
        writer.WriteString("hash", commit.Hash);
        writer.WriteStartArray("parents");
        foreach (var parent in commit.Parents)
        {
          writer.WriteStringValue(parent);
        }

        writer.WriteEndArray();
        writer.WriteString("authoredAt", commit.AuthoredAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("committedAt", commit.CommittedAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("message", commit.Message);
        writer.WriteStartArray("files");
        foreach (var file in commit.Files)
        {
          writer.WriteStartObject();
          writer.WriteString("path", file.Path);
          writer.WriteString("change", file.Change);
          writer.WriteString("previousPath", file.PreviousPath);
          writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteBoolean("filesTruncated", commit.FilesTruncated);
        writer.WriteEndObject();
      }

      writer.WriteEndArray();
      writer.WriteStartArray("tags");
      foreach (var tag in tags)
      {
        writer.WriteStartObject();
        writer.WriteString("id", tag.Id);
        writer.WriteString("name", tag.Name);
        writer.WriteString("change", tag.Change);
        writer.WriteString("commit", tag.Commit);
        writer.WriteString("previousCommit", tag.PreviousCommit);
        writer.WriteString("observedAt", tag.ObservedAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteEndObject();
      }

      writer.WriteEndArray();
      writer.WriteEndObject();
    }

    return Encoding.UTF8.GetString(buffer.ToArray());
  }
}
