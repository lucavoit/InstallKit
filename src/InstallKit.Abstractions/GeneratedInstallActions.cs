using System;
using System.Linq;
using System.Reflection;

namespace InstallKit
{
    /// <summary>
    /// Emitted by the InstallKit source generator for every install event handler: maps the handler
    /// (<c>Namespace.Type+Nested.Method</c>) to its generated custom action entry point.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class GeneratedInstallActionAttribute : Attribute
    {
        public GeneratedInstallActionAttribute(InstallStage stage, string handler, string actionName)
        {
            Stage = stage;
            Handler = handler;
            ActionName = actionName;
        }

        public InstallStage Stage { get; }

        public string Handler { get; }

        /// <summary>Name of the generated static [CustomAction] method in <c>InstallKit.Generated.InstallKitActions</c>.</summary>
        public string ActionName { get; }
    }

    /// <summary>Finds the generated custom action for a handler. Engines use this at build time.</summary>
    public static class GeneratedInstallActions
    {
        /// <summary>The generated class holding the entry points.</summary>
        public const string ActionsType = "InstallKit.Generated.InstallKitActions";

        /// <summary>The runtime key of a handler method, matching what the generator records.</summary>
        public static string KeyOf(MethodInfo method) => method.DeclaringType!.FullName + "." + method.Name;

        /// <summary>
        /// Returns the generated action and the assembly that contains it (the one that registered the
        /// handler, usually the installer program), or <c>null</c> when the generator did not see the handler.
        /// </summary>
        public static (string ActionName, Assembly Assembly)? Find(InstallEventHandler handler)
        {
            var method = handler.Handler.Method;
            var key = KeyOf(method);

            var candidates = new[] { Assembly.GetEntryAssembly(), method.DeclaringType!.Assembly }
                .Where(a => a != null)
                .Distinct();

            foreach (var assembly in candidates)
            {
                var match = assembly!.GetCustomAttributes<GeneratedInstallActionAttribute>()
                                     .FirstOrDefault(a => a.Stage == handler.Stage && a.Handler == key);
                if (match != null)
                    return (match.ActionName, assembly);
            }

            return null;
        }
    }
}
