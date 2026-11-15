using System;
using System.Xml.XPath;

namespace InstallKit
{
    /// <summary>
    /// Sets one attribute or element value in an installed XML file (an <c>app.config</c>, for example) while
    /// the product is installed. The value can use MSI properties such as <c>[SERVER]</c>, so a value the
    /// user passes on the command line or enters in a dialog ends up in the file without a custom action.
    /// </summary>
    public sealed class XmlValue
    {
        public XmlValue(string file, string xpath, string value)
        {
            File = file;
            XPath = xpath;
            Value = value;
        }

        /// <summary>The file relative to the install directory, for example <c>MyApp.exe.config</c> or <c>config\app.config</c>.</summary>
        public string File { get; }

        /// <summary>An XPath to one element or attribute, for example <c>/configuration/appSettings/add[@key='Server']/@value</c>.</summary>
        public string XPath { get; }

        /// <summary>The new value. MSI formatted text such as <c>[SERVER]</c> is expanded.</summary>
        public string Value { get; }

        /// <summary>Whether <see cref="XPath"/> is a valid XPath expression.</summary>
        internal bool HasValidXPath
        {
            get
            {
                try
                {
                    XPathExpression.Compile(XPath);
                    return !string.IsNullOrWhiteSpace(XPath);
                }
                catch (XPathException)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// The XPath as Windows Installer needs it. MSI reads <c>[...]</c> in the field as a property reference, so the
        /// brackets of XPath predicates are written as <c>[\[]</c> and <c>[\]]</c>.
        /// </summary>
        public string EscapedXPath
        {
            get
            {
                // One pass: escaping '[' adds a ']' that must not be escaped again.
                var escaped = new System.Text.StringBuilder(XPath.Length + 8);
                foreach (var c in XPath)
                    escaped.Append(c == '[' ? "[\\[]" : c == ']' ? "[\\]]" : c.ToString());
                return escaped.ToString();
            }
        }
    }
}
