using System;

namespace InstallKit
{
    /// <summary>
    /// Turns build versions (<c>1.2.3</c>, <c>1.2.3-beta.4+sha</c>, <c>2024.11.14.3</c>) into versions MSI accepts.
    /// </summary>
    public static class ProductVersion
    {
        /// <summary>
        /// The pre-release or build metadata suffix of a version (<c>-rc1</c>, <c>+sha</c>) that <see cref="TryParse"/> drops,
        /// or <c>null</c> when there is none.
        /// </summary>
        public static string? GetDroppedSuffix(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var text = value!.Trim();
            var cut = text.IndexOfAny(new[] { '-', '+' });
            return cut >= 0 && cut < text.Length - 1 ? text.Substring(cut) : null;
        }

        /// <summary>
        /// Parses and validates a version. Pre-release and build metadata suffixes are dropped.
        /// MSI only compares the first three fields: major and minor must be at most 255, build at most 65535.
        /// </summary>
        public static bool TryParse(string? value, out Version version, out string? error)
        {
            version = new Version(1, 0, 0);
            error = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                error = "No version was given. Pass one with -p:Version=1.2.3.";
                return false;
            }

            var core = value!.Trim();
            if (core.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                core = core.Substring(1);

            var cut = core.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0)
                core = core.Substring(0, cut);

            if (!Version.TryParse(core.Contains(".") ? core : core + ".0", out var parsed))
            {
                error = $"'{value}' is not a valid version. Use major.minor.build, for example 1.2.3.";
                return false;
            }

            var build = parsed.Build < 0 ? 0 : parsed.Build;
            var revision = parsed.Revision < 0 ? 0 : parsed.Revision;

            if (parsed.Major > 255 || parsed.Minor > 255 || build > 65535)
            {
                error = $"'{value}' cannot be used as an MSI version: major and minor must be at most 255 and build at most 65535. " +
                        "Date-based build numbers such as 20241114 do not fit; map them into the build field instead.";
                return false;
            }

            version = new Version(parsed.Major, parsed.Minor, build, revision);
            return true;
        }
    }
}
