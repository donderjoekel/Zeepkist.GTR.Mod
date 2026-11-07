using System;

namespace TNRD.Zeepkist.GTR.Core;

internal sealed class GtrVersionStatus
{
    public string Current { get; }
    public string Minimum { get; }
    public string Latest { get; }
    public bool IsUnsupported { get; }
    public string UpdateMessage => Latest == null ? "Update GTR" : $"Update GTR to {Latest}";

    private GtrVersionStatus(string current, string minimum, string latest, bool unsupported)
    {
        Current = current;
        Minimum = minimum;
        Latest = latest;
        IsUnsupported = unsupported;
    }

    public static GtrVersionStatus Unknown(string current) => new(current, null, null, false);

    public static GtrVersionStatus Evaluate(string current, string minimum, string latest)
    {
        if (!TryParse(current, out Version currentVersion) || !TryParse(minimum, out Version minimumVersion))
            throw new FormatException("Invalid GTR current or minimum version");

        return new GtrVersionStatus(current, minimum, TryParse(latest, out _) ? latest : null,
            currentVersion < minimumVersion);
    }

    private static bool TryParse(string value, out Version version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        string[] parts = value.Split('.');
        if (parts.Length < 1 || parts.Length > 4)
            return false;
        int[] components = new int[4];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                return false;
            foreach (char digit in parts[i])
                if (digit < '0' || digit > '9')
                    return false;
            if (!int.TryParse(parts[i], out components[i]))
                return false;
        }
        version = new Version(components[0], components[1], components[2], components[3]);
        return true;
    }
}
