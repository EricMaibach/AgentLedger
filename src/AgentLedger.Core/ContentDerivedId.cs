using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AgentLedger.Core;

// IDs computed from what identifies a thing (ADR 0020, ADR 0021), so anyone observing the same thing gets the same ID.
// SHA-256 over the kind and length-prefixed fields (so fields can't run into each other), first 16 bytes,
// formatted as a version 8 UUID (the version for application-defined IDs). Stored IDs depend on this:
// never change it.
internal static class ContentDerivedId
{
  public static Guid Derive(string kind, params string[] fields)
  {
    var canonical = kind + "|" + string.Join("|", fields.Select(f => $"{f.Length.ToString(CultureInfo.InvariantCulture)}:{f}"));
    Span<byte> bytes = stackalloc byte[32];
    SHA256.HashData(Encoding.UTF8.GetBytes(canonical), bytes);
    bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // version 8
    bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
    return new Guid(bytes[..16], bigEndian: true);
  }

  public static string Sha256Hex(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
