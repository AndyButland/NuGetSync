namespace NuGetSync.Console
{
    public static class StringExtensions
    {
        public static string Truncate(this string s, int maxLength)
        {
            const string suffix = "...";

            if (s == null || s.Length <= maxLength)
            {
                return s;
            }

            int length = maxLength - suffix.Length;
            return s.Substring(0, length) + suffix;
        }
    }
}
