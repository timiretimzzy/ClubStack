using System.Text;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// Reads and updates the FiscalStackZW device configuration file in place.
///
/// The engine owns this file and rewrites it through its own save routine, so updates
/// here are surgical: only the requested keys are replaced and every other line -
/// including DPAPI-protected values and keys ClubStack does not model - is preserved
/// byte for byte. Rewriting the file wholesale would risk discarding protected values,
/// so a whole-file serialiser is deliberately not used.
///
/// This is how a device is bound to an FDMS environment: the engine derives its
/// environment, tax identifiers and endpoint behaviour from the stored server values,
/// so persisting them here is the supported selection point. It is ClubStack code
/// performing the change as part of provisioning, not an operator editing a file.
/// </summary>
public sealed class FiscalStackDeviceConfigFile
{
    private readonly object _lock = new();

    public FiscalStackDeviceConfigFile(string? path = null) => Path = path ?? ResolveDefaultPath();

    /// <summary>Resolved configuration file path.</summary>
    public string Path { get; }

    /// <summary>
    /// Mirrors the engine's own precedence: the per-machine configuration wins, falling
    /// back to the configuration beside the application.
    /// </summary>
    public static string ResolveDefaultPath()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var machineConfig = System.IO.Path.Combine(programData, "FiscalStackZW", "config.ini");
        if (File.Exists(machineConfig))
        {
            return machineConfig;
        }

        return System.IO.Path.Combine(AppContext.BaseDirectory, "config.ini");
    }

    /// <summary>Reads a value, or <c>null</c> when the key is absent or blank.</summary>
    public string? Read(string key)
    {
        lock (_lock)
        {
            var values = ReadAll();
            return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
        }
    }

    /// <summary>
    /// Replaces the given keys' values, adding any that are absent. Keys not supplied are
    /// left untouched.
    /// </summary>
    public void SetValues(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        lock (_lock)
        {
            if (!File.Exists(Path))
            {
                throw new InvalidOperationException(
                    $"The fiscal device configuration file was not found at '{Path}'. " +
                    "ClubStack cannot bind a device to an environment without it.");
            }

            var text = File.ReadAllText(Path);
            var encoding = DetectEncoding(text, out var hasBom);
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();

            foreach (var (key, value) in values)
            {
                var assigned = false;
                for (var i = 0; i < lines.Count; i++)
                {
                    if (!TryParseKey(lines[i], out var existingKey))
                    {
                        continue;
                    }

                    if (!string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    lines[i] = $"{key} = {value}";
                    assigned = true;
                    break;
                }

                if (!assigned)
                {
                    InsertKey(lines, key, value);
                }
            }

            while (lines.Count > 0 && lines[^1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            var output = string.Join(newline, lines) + newline;
            var bytes = encoding.GetBytes(output);
            if (hasBom)
            {
                bytes = Concat(encoding.GetPreamble().ToArray(), bytes);
            }

            File.WriteAllBytes(Path, bytes);
        }
    }

    private static byte[] Concat(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }

    private static void InsertKey(List<string> lines, string key, string? value)
    {
        var sectionEnd = lines.Count;
        while (sectionEnd > 0 && lines[sectionEnd - 1].Length == 0)
        {
            sectionEnd--;
        }

        lines.Insert(sectionEnd, $"{key} = {value}");
    }

    private static bool TryParseKey(string line, out string key)
    {
        key = string.Empty;
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#') || trimmed.StartsWith('['))
        {
            return false;
        }

        var separator = trimmed.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        key = trimmed[..separator].Trim();
        return key.Length > 0;
    }

    private Dictionary<string, string> ReadAll()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(Path))
        {
            return result;
        }

        foreach (var line in File.ReadAllLines(Path))
        {
            if (!TryParseKey(line, out var key))
            {
                continue;
            }

            var separator = line.Trim().IndexOf('=');
            result[key] = line.Trim()[(separator + 1)..].Trim();
        }

        return result;
    }

    private static Encoding DetectEncoding(string text, out bool hasBom)
    {
        hasBom = text.Length > 0 && text[0] == '﻿';
        return hasBom ? new UTF8Encoding(true) : new UTF8Encoding(false);
    }
}
