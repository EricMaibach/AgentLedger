using System.Numerics;

namespace AgentLedger.Core.ApiKeyAggregate;

// Letters and digits only, so a key survives copy-paste, shells and URLs, and double-clicking selects
// the whole of each part. Fixed width, so each part of a token has a known length.
public static class Base62
{
  private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

  public static string Encode(byte[] bytes, int width)
  {
    var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    var chars = new char[width];
    for (var i = width - 1; i >= 0; i--)
    {
      value = BigInteger.DivRem(value, 62, out var digit);
      chars[i] = Alphabet[(int)digit];
    }

    if (!value.IsZero)
    {
      throw new ArgumentException($"{bytes.Length} bytes don't fit in {width} base62 characters.", nameof(width));
    }

    return new string(chars);
  }

  public static bool TryDecode(string text, int byteCount, out byte[] bytes)
  {
    bytes = [];
    var value = BigInteger.Zero;
    foreach (var c in text)
    {
      var digit = Alphabet.IndexOf(c, StringComparison.Ordinal);
      if (digit < 0)
      {
        return false;
      }

      value = value * 62 + digit;
    }

    var raw = value.ToByteArray(isUnsigned: true, isBigEndian: true);
    if (value.IsZero)
    {
      raw = [];
    }

    if (raw.Length > byteCount)
    {
      return false;
    }

    bytes = new byte[byteCount];
    raw.CopyTo(bytes, byteCount - raw.Length);
    return true;
  }
}
