using System.Globalization;

namespace UOTinker;

public static class Loc
{
    private static readonly Dictionary<string, string> En = new();

    public static bool English { get; private set; }

    public static List<string> Missing { get; } = new();

    static Loc()
    {
        foreach (var pairs in new[] { LocEn1.Pairs, LocEn2.Pairs, LocEn3.Pairs, LocEn4.Pairs, LocEn5.Pairs })
        {
            foreach (var (de, en) in pairs)
            {
                En[de] = en;
            }
        }
    }

    public static void Init(string? language)
    {
        string? env = Environment.GetEnvironmentVariable("UOTINKER_LANG");
        string lang = !string.IsNullOrEmpty(env) ? env : language ?? "";
        if (lang.Length == 0)
        {
            lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }
        English = !lang.StartsWith("de", StringComparison.OrdinalIgnoreCase);
    }

    public static string T(string de)
    {
        if (!English)
        {
            return de;
        }
        if (En.TryGetValue(de, out var en))
        {
            return en;
        }
        lock (Missing)
        {
            if (!Missing.Contains(de))
            {
                Missing.Add(de);
            }
        }
        return de;
    }

    public static string F(string de, params object?[] args) => string.Format(CultureInfo.CurrentCulture, T(de), args);
}
