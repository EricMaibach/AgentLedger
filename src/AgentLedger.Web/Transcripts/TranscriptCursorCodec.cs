using System.Buffers.Text;
using System.Globalization;
using System.Text;
using AgentLedger.UseCases.Transcripts;

namespace AgentLedger.Web.Transcripts;

/// <summary>
/// The transcript paging cursor on the wire: opaque to clients (ADR 0016), who only pass back what they received.
/// Base64url of "&lt;received UTC ticks&gt;:&lt;byte offset&gt;:&lt;line id&gt;".
/// </summary>
public static class TranscriptCursorCodec
{
  /// <summary>Encodes a cursor for a URL.</summary>
  public static string Encode(TranscriptLineCursor cursor) =>
    Base64Url.EncodeToString(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture,
      $"{cursor.ReceivedAt.UtcTicks}:{cursor.ByteOffset}:{cursor.LineId:N}")));

  /// <summary>Decodes a cursor; false for anything this didn't produce.</summary>
  public static bool TryDecode(string value, out TranscriptLineCursor cursor)
  {
    cursor = default!; // only read when this returns true
    try
    {
      var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value)).Split(':');
      if (parts.Length == 3
          && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
          && ticks <= DateTimeOffset.MaxValue.UtcTicks
          && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
          && Guid.TryParseExact(parts[2], "N", out var lineId))
      {
        cursor = new TranscriptLineCursor(new DateTimeOffset(ticks, TimeSpan.Zero), offset, lineId);
        return true;
      }
    }
    catch (FormatException)
    {
      // not base64url
    }

    return false;
  }
}
