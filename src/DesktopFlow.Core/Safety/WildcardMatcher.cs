using System.Text.RegularExpressions;

namespace DesktopFlow.Core.Safety;

public static class WildcardMatcher
{
    public static bool IsMatch(string value, string pattern)
    {
        if (pattern == "*")
        {
            return true;
        }

        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
