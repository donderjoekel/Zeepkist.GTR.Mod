using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

internal static class SurfaceKeyNormalizer
{
	public static string NormalizeSurfaceKey(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return null;

		name = name.Replace("(Instance)", string.Empty).Trim().ToLowerInvariant();
		List<char> chars = new();
		foreach (char c in name)
		{
			if (char.IsLetterOrDigit(c))
				chars.Add(c);
			else if (chars.Count > 0 && chars[chars.Count - 1] != '_')
				chars.Add('_');
		}

		while (chars.Count > 0 && chars[chars.Count - 1] == '_')
			chars.RemoveAt(chars.Count - 1);

		return chars.Count == 0 ? null : new string(chars.ToArray());
	}

	public static string ChooseMostCommonSurfaceKey(IEnumerable<string> surfaceNames)
	{
		if (surfaceNames == null)
			return null;

		Dictionary<string, int> counts = new();
		foreach (string surfaceName in surfaceNames)
		{
			string key = NormalizeSurfaceKey(surfaceName);
			if (key == null)
				continue;

			if (!counts.ContainsKey(key))
				counts[key] = 0;
			counts[key]++;
		}

		return counts
			.OrderByDescending(x => x.Value)
			.Select(x => x.Key)
			.FirstOrDefault();
	}
}
