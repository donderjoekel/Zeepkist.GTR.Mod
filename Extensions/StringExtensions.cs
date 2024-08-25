using System;
using System.Linq;

namespace TNRD.Zeepkist.GTR.Extensions;

public static class StringExtensions
{
    public static bool EqualsAny(this string str, params string[] values)
    {
        return values.Any(x => x.Equals(str));
    }

    public static bool EqualsAny(this string str, StringComparison comparison, params string[] values)
    {
        return values.Any(x => x.Equals(str, comparison));
    }
}
