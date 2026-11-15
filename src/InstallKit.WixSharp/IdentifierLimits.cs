using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace InstallKit.WixSharp
{
    /// <summary>
    /// Windows Installer identifiers (component and file ids, for example) can be at most 72 characters. WixSharp builds a
    /// component id from the file name and a hash ("Component.Microsoft.Extensions.Configuration.EnvironmentVariables.dll_1712862982"),
    /// which passes WiX but breaks that rule for files with long names, and ICE03 warns about it. This shortens such ids in the
    /// generated source and updates everything that refers to them. It only produces XML and uses no WixSharp types.
    /// </summary>
    internal static class IdentifierLimits
    {
        public const int MaxLength = 72;

        // How much of the original id is kept: the rest of the budget is "_" and eight characters of a hash of the whole id.
        const int KeptLength = MaxLength - 9;

        /// <summary>Shortens the long component and file ids of a WiX source document. Returns how many ids were changed.</summary>
        public static int Apply(XDocument document)
        {
            var longIds = document.Descendants()
                .Where(e => (e.Name.LocalName == "Component" || e.Name.LocalName == "File") && ((string?)e.Attribute("Id"))?.Length > MaxLength)
                .Select(e => (string)e.Attribute("Id")!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (longIds.Count == 0)
                return 0;

            var taken = new HashSet<string>(document.Descendants().Select(e => (string?)e.Attribute("Id")).Where(id => id != null)!, StringComparer.Ordinal);
            var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in longIds)
            {
                var shortened = Shorten(id);
                for (var attempt = 1; taken.Contains(shortened); attempt++)
                    shortened = Shorten(id + "#" + attempt);
                taken.Add(shortened);
                renamed[id] = shortened;
            }

            foreach (var element in document.Descendants())
            {
                var name = element.Name.LocalName;
                var idAttribute = element.Attribute("Id");
                if (idAttribute != null && (name == "Component" || name == "File" || name == "ComponentRef") && renamed.TryGetValue(idAttribute.Value, out var newId))
                    idAttribute.Value = newId;

                // Formatted references to a file, for example File="[#app.config_123]" or Target="[#App.exe_456]".
                foreach (var attribute in element.Attributes().Where(a => a.Value.Contains("[#")))
                {
                    var value = attribute.Value;
                    foreach (var pair in renamed)
                        value = value.Replace("[#" + pair.Key + "]", "[#" + pair.Value + "]");
                    attribute.Value = value;
                }
            }

            return renamed.Count;
        }

        /// <summary>The first 63 characters of an id, then "_" and eight hex digits of a SHA-1 of the whole id: 72 characters, stable for the same input.</summary>
        internal static string Shorten(string id)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(id));
                var suffix = string.Concat(hash.Take(4).Select(b => b.ToString("x2")));
                return id.Substring(0, KeptLength) + "_" + suffix;
            }
        }
    }
}
