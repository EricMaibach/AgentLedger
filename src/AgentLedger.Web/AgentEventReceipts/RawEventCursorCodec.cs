using System.Buffers.Text;
using System.Globalization;
using System.Text;
using AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

namespace AgentLedger.Web.AgentEventReceipts;

// The keyset cursor on the wire: opaque to clients (ADR 0016), who only pass back what they received.
// Encoded as base64url of "<receivedAt UTC ticks>:<receipt id>", so it's URL-safe and unambiguous.
internal static class RawEventCursorCodec
{
  public static string Encode(RawEventCursor cursor) =>
    Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
      $"{cursor.ReceivedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}:{cursor.ReceiptId:N}"));

  public static bool TryDecode(string value, out RawEventCursor cursor)
  {
    cursor = default!;
    try
    {
      var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value)).Split(':');
      if (parts.Length == 2
          && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
          && ticks <= DateTimeOffset.MaxValue.UtcTicks
          && Guid.TryParseExact(parts[1], "N", out var receiptId))
      {
        cursor = new RawEventCursor(new DateTimeOffset(ticks, TimeSpan.Zero), receiptId);
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
