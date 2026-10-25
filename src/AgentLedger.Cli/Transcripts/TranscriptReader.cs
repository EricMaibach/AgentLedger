using System.Security.Cryptography;
using System.Text;
using AgentLedger.Cli.Logging;

namespace AgentLedger.Cli.Transcripts;

/// <summary>A line as read: where it starts in the file (in bytes) and its exact text, without the newline.</summary>
internal sealed record TranscriptLineRead(long ByteOffset, string Text);

/// <summary>
/// How far a transcript has been read, and a fingerprint of its first bytes (SHA-256 of the first
/// <paramref name="FingerprintLength"/> bytes) to notice the file being replaced.
/// </summary>
internal sealed record TranscriptFileState(string Path, long Offset, int FingerprintLength, string Fingerprint);

internal sealed record TranscriptChunk(IReadOnlyList<TranscriptLineRead> Lines, TranscriptFileState NewState);

/// <summary>
/// Reads a transcript incrementally (ADR 0020): from the saved offset to the last complete line, at most
/// <paramref name="maxChunkBytes"/> per run. A file that shrank or whose start changed is read again from the
/// beginning; the ledger recognizes lines it already has. A line longer than <paramref name="maxLineBytes"/>
/// could never be sent, so it is skipped and logged.
/// </summary>
internal sealed class TranscriptReader(int maxChunkBytes, int maxLineBytes, ICliLog log)
{
  public const int DefaultMaxChunkBytes = 1024 * 1024;
  public const int DefaultMaxLineBytes = TranscriptBatches.MaxTextBytes;

  private const int FingerprintBytes = 4096;

  public TranscriptChunk Read(TranscriptFileRef file, TranscriptFileState? state)
  {
    FileStream stream;
    try
    {
      // The agent keeps writing while we read.
      stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }
    catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
    {
      return new TranscriptChunk([], state ?? new TranscriptFileState(file.Path, 0, 0, ""));
    }

    using (stream)
    {
      var length = stream.Length;
      var start = state is not null && state.Offset <= length && Matches(stream, state) ? state.Offset : 0;
      var (fingerprintLength, fingerprint) = Fingerprint(stream, length);
      TranscriptChunk Result(IReadOnlyList<TranscriptLineRead> lines, long offset) =>
        new(lines, new TranscriptFileState(file.Path, offset, fingerprintLength, fingerprint));

      var buffer = ReadAt(stream, start, (int)Math.Min(maxChunkBytes, length - start));
      var lastNewline = Array.LastIndexOf(buffer, (byte)'\n');
      if (lastNewline >= 0)
      {
        return Result(Split(buffer, lastNewline, start), start + lastNewline + 1);
      }

      if (length - start <= buffer.Length)
      {
        return Result([], start); // only a partly written line so far
      }

      // A line longer than a chunk: look for its end, up to the longest line that can be sent.
      var longLine = ReadAt(stream, start, (int)Math.Min(maxLineBytes, length - start));
      var end = Array.IndexOf(longLine, (byte)'\n');
      if (end >= 0)
      {
        return Result(Split(longLine, end, start), start + end + 1);
      }

      if (length - start <= maxLineBytes)
      {
        return Result([], start); // still being written
      }

      // Too long to send: skip to its end, once it has one.
      if (FindNewline(stream, start + maxLineBytes) is { } newline)
      {
        log.Error($"Skipped a transcript line longer than {maxLineBytes} bytes at offset {start} in {file.Path}.");
        return Result([], newline + 1);
      }

      return Result([], start);
    }
  }

  private static List<TranscriptLineRead> Split(byte[] buffer, int lastNewline, long start)
  {
    var lines = new List<TranscriptLineRead>();
    var lineStart = 0;
    for (var i = 0; i <= lastNewline; i++)
    {
      if (buffer[i] == (byte)'\n')
      {
        lines.Add(new TranscriptLineRead(start + lineStart, Encoding.UTF8.GetString(buffer, lineStart, i - lineStart)));
        lineStart = i + 1;
      }
    }

    return lines;
  }

  private static byte[] ReadAt(FileStream stream, long offset, int count)
  {
    var buffer = new byte[count];
    stream.Position = offset;
    stream.ReadExactly(buffer);
    return buffer;
  }

  private static long? FindNewline(FileStream stream, long from)
  {
    stream.Position = from;
    var block = new byte[64 * 1024];
    for (var position = from; ; position += block.Length)
    {
      var read = stream.Read(block);
      if (read == 0)
      {
        return null;
      }

      var index = Array.IndexOf(block, (byte)'\n', 0, read);
      if (index >= 0)
      {
        return position + index;
      }
    }
  }

  private static bool Matches(FileStream stream, TranscriptFileState state) =>
    state.FingerprintLength <= stream.Length && Hash(stream, state.FingerprintLength) == state.Fingerprint;

  private static (int Length, string Hash) Fingerprint(FileStream stream, long length)
  {
    var count = (int)Math.Min(FingerprintBytes, length);
    return (count, Hash(stream, count));
  }

  private static string Hash(FileStream stream, int count) => Convert.ToHexStringLower(SHA256.HashData(ReadAt(stream, 0, count)));
}
