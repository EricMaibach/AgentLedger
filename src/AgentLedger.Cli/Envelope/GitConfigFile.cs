namespace AgentLedger.Cli.Envelope;

// Reads the handful of values we need from a git config file (.git/config, ~/.gitconfig).
// Deliberately minimal: sections, subsections and key = value lines, with git's value rules
// (quotes, backslash escapes, trailing comments). No includes, no multi-line values.
internal static class GitConfigFile
{
  /// <summary>Returns the value of <paramref name="key"/> in the given section, or null.</summary>
  /// <param name="section">E.g. <c>user</c>, or <c>remote "origin"</c> for a subsection.</param>
  public static string? Read(string path, string section, string key)
  {
    if (!File.Exists(path))
    {
      return null;
    }

    var inSection = false;
    foreach (var rawLine in File.ReadLines(path))
    {
      var line = rawLine.Trim();
      if (line.Length == 0 || line[0] is '#' or ';')
      {
        continue;
      }

      if (line[0] == '[')
      {
        inSection = string.Equals(line.TrimStart('[').TrimEnd(']').Trim(), section, StringComparison.OrdinalIgnoreCase);
        continue;
      }

      var equals = line.IndexOf('=');
      if (inSection && equals > 0 && string.Equals(line[..equals].Trim(), key, StringComparison.OrdinalIgnoreCase))
      {
        return ParseValue(line[(equals + 1)..]);
      }
    }

    return null;
  }

  // Git strips quotes (keeping whitespace inside them), unescapes \" \\ \n \t, ends the value at an
  // unquoted ; or #, and trims unquoted whitespace at either end.
  private static string ParseValue(string raw)
  {
    var value = new System.Text.StringBuilder();
    var keepLength = 0; // length up to the last character that must be kept
    var inQuotes = false;

    for (var i = 0; i < raw.Length; i++)
    {
      var c = raw[i];
      if (c == '"')
      {
        inQuotes = !inQuotes;
        keepLength = value.Length;
        continue;
      }

      if (c == '\\' && i + 1 < raw.Length)
      {
        var escaped = raw[++i];
        value.Append(escaped switch { 'n' => '\n', 't' => '\t', _ => escaped });
        keepLength = value.Length;
        continue;
      }

      if (!inQuotes && c is ';' or '#')
      {
        break;
      }

      if (!inQuotes && char.IsWhiteSpace(c) && keepLength == 0 && value.Length == 0)
      {
        continue; // leading unquoted whitespace
      }

      value.Append(c);
      if (inQuotes || !char.IsWhiteSpace(c))
      {
        keepLength = value.Length;
      }
    }

    return value.ToString(0, keepLength);
  }
}
