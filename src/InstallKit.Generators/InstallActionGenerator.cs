using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace InstallKit.Generators
{
    /// <summary>
    /// Generates one [CustomAction] entry point per install event handler. The output only depends on
    /// WiX's DTF library and InstallKit.Abstractions, never on WixSharp, so every engine can use it:
    /// the .NET Framework engine embeds it as a managed custom action and WixSharp.Core compiles it
    /// to native code (NativeAOT), where reflection-based dispatch would not work.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class InstallActionGenerator : IIncrementalGenerator
    {
        internal const string SessionType = "WixToolset.Dtf.WindowsInstaller.Session";
        static readonly string[] Stages = { "OnLoad", "OnBeforeInstall", "OnAfterInstall" };
        static readonly string[] Owners = { "InstallKit.MsiBuilder", "InstallKit.MsiDefinition" };

        static readonly DiagnosticDescriptor NotAMethod = new DiagnosticDescriptor(
            "IK160", "Install event handler must be a method",
            "Pass a static method (for example Setup.Configure) instead of {0}: install-time code is compiled separately and cannot capture or name lambdas",
            "InstallKit", DiagnosticSeverity.Error, isEnabledByDefault: true);

        static readonly DiagnosticDescriptor NotStatic = new DiagnosticDescriptor(
            "IK161", "Install event handler must be static",
            "'{0}' must be static: it runs on the target machine, where no instance of its class exists",
            "InstallKit", DiagnosticSeverity.Error, isEnabledByDefault: true);

        static readonly DiagnosticDescriptor NotAccessible = new DiagnosticDescriptor(
            "IK162", "Install event handler must be internal or public",
            "'{0}' and its containing types must be internal or public so the generated custom action can call it",
            "InstallKit", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var calls = context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, _) => node is InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name },
                        ArgumentList.Arguments.Count: > 0,
                    } && Stages.Contains(name),
                    transform: static (ctx, ct) => Analyze(ctx, ct))
                .Where(static r => r != null)
                .Select(static (r, _) => r!)
                .Collect();

            var hasDtf = context.CompilationProvider.Select(static (c, _) => c.GetTypeByMetadataName(SessionType) != null);

            context.RegisterSourceOutput(calls.Combine(hasDtf), static (spc, input) =>
            {
                var (results, dtf) = input;
                foreach (var diagnostic in results.Where(r => r.Diagnostic != null))
                    spc.ReportDiagnostic(diagnostic.Diagnostic!);

                var handlers = results.Where(r => r.Handler != null).Select(r => r.Handler!).Distinct().OrderBy(h => h.ActionName).ToList();

                // Without WiX's DTF library (no engine referenced) there is nothing to generate against.
                if (!dtf || handlers.Count == 0)
                    return;

                spc.AddSource("InstallKit.InstallActions.g.cs", Render(handlers));
                spc.AddSource("InstallKit.SessionInstallEvent.g.cs", SessionAdapterSource);
            });
        }

        internal sealed class Result
        {
            public HandlerInfo? Handler;
            public Diagnostic? Diagnostic;
        }

        internal sealed record HandlerInfo(string Stage, string MethodExpression, string HandlerKey, string ActionName);

        static Result? Analyze(GeneratorSyntaxContext ctx, CancellationToken ct)
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;
            if (ctx.SemanticModel.GetOperation(invocation, ct) is not IInvocationOperation operation)
                return null;

            var owner = operation.TargetMethod.ContainingType?.ToDisplayString();
            if (owner == null || !Owners.Contains(owner) || operation.Arguments.Length == 0)
                return null;

            var stage = operation.TargetMethod.Name.Substring(2); // "OnAfterInstall" -> "AfterInstall"
            var argument = operation.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0)?.Value;
            var location = invocation.ArgumentList.Arguments[0].GetLocation();

            while (argument is IConversionOperation conversion)
                argument = conversion.Operand;

            if (argument is not IDelegateCreationOperation { Target: IMethodReferenceOperation reference })
                return Error(NotAMethod, location, invocation.ArgumentList.Arguments[0].ToString());

            var method = reference.Method;
            if (method.MethodKind != MethodKind.Ordinary || method.ContainingType == null || method.ContainingType.IsGenericType)
                return Error(NotAMethod, location, method.Name);

            if (!method.IsStatic)
                return Error(NotStatic, location, method.ToDisplayString());

            if (!IsAccessible(method))
                return Error(NotAccessible, location, method.ToDisplayString());

            var key = HandlerKey(method);
            return new Result
            {
                Handler = new HandlerInfo(
                    stage,
                    method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + method.Name,
                    key,
                    ActionName(stage, key)),
            };
        }

        static Result Error(DiagnosticDescriptor descriptor, Location location, string argument) =>
            new Result { Diagnostic = Diagnostic.Create(descriptor, location, argument) };

        static bool IsAccessible(ISymbol symbol)
        {
            for (var current = symbol; current != null && current is not INamespaceSymbol; current = current.ContainingSymbol)
            {
                if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The runtime name of the handler as reflection reports it: Namespace.Outer+Inner.Method.
        /// Engines compute the same key from the delegate's MethodInfo to find the generated action.
        /// </summary>
        internal static string HandlerKey(IMethodSymbol method)
        {
            var types = new List<string>();
            for (var type = method.ContainingType; type != null; type = type.ContainingType)
                types.Insert(0, type.MetadataName);

            var ns = method.ContainingType!.ContainingNamespace;
            var prefix = ns == null || ns.IsGlobalNamespace ? string.Empty : ns.ToDisplayString() + ".";
            return prefix + string.Join("+", types) + "." + method.Name;
        }

        /// <summary>Stable, MSI-safe custom action name (max 72 chars, identifier characters only).</summary>
        internal static string ActionName(string stage, string handlerKey)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in stage + ":" + handlerKey)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }

                return $"InstallKit_{stage}_{hash:x8}";
            }
        }

        static string Render(IReadOnlyList<HandlerInfo> handlers)
        {
            var code = new StringBuilder();
            code.AppendLine("// <auto-generated>InstallKit install event entry points.</auto-generated>");
            code.AppendLine("#nullable enable");
            foreach (var h in handlers)
                code.AppendLine($"[assembly: global::InstallKit.GeneratedInstallAction(global::InstallKit.InstallStage.{h.Stage}, \"{h.HandlerKey}\", \"{h.ActionName}\")]");

            code.AppendLine();
            code.AppendLine("namespace InstallKit.Generated");
            code.AppendLine("{");
            code.AppendLine("    /// <summary>Custom action entry points, one per install event handler.</summary>");
            code.AppendLine("    public static class InstallKitActions");
            code.AppendLine("    {");
            foreach (var h in handlers)
            {
                code.AppendLine("        [global::WixToolset.Dtf.WindowsInstaller.CustomAction]");
                code.AppendLine($"        public static global::WixToolset.Dtf.WindowsInstaller.ActionResult {h.ActionName}(global::WixToolset.Dtf.WindowsInstaller.Session session)");
                code.AppendLine($"            => global::InstallKit.Generated.SessionInstallEvent.Run(session, global::InstallKit.InstallStage.{h.Stage}, {h.MethodExpression});");
                code.AppendLine();
            }

            code.AppendLine("    }");
            code.AppendLine("}");
            return code.ToString();
        }

        /// <summary>
        /// Adapts a raw MSI session to InstallEvent using only DTF, so it works in every engine and under NativeAOT.
        /// Deferred (elevated) actions can only read CustomActionData; engines pass the needed properties.
        /// </summary>
        const string SessionAdapterSource = @"// <auto-generated>InstallKit session adapter.</auto-generated>
#nullable enable
namespace InstallKit.Generated
{
    using global::WixToolset.Dtf.WindowsInstaller;

    internal sealed class SessionInstallEvent : global::InstallKit.InstallEvent
    {
        readonly Session session;
        readonly bool deferred;

        SessionInstallEvent(Session session, global::InstallKit.InstallStage stage)
        {
            this.session = session;
            Stage = stage;
            deferred = session.GetMode(InstallRunMode.Scheduled);
        }

        public override global::InstallKit.InstallStage Stage { get; }

        public override bool IsUninstalling => Get(""REMOVE"") == ""ALL"";

        public override bool IsInstalling => Get(""Installed"") == null && !IsUninstalling;

        public override bool IsUpgrading => Get(""WIX_UPGRADE_DETECTED"") != null || Get(""UPGRADINGPRODUCTCODE"") != null;

        public override bool IsRepairing => Get(""Installed"") != null && Get(""REINSTALL"") == ""ALL"";

        public override bool IsUiSuppressed => int.TryParse(Get(""UILevel""), out var level) && level <= 4;

        public override string? InstallDir => Get(""INSTALLDIR"");

        public override string? this[string property] => Get(property);

        public override void Log(string message) => session.Log(message);

        string? Get(string name)
        {
            string? value;
            if (deferred)
                value = session.CustomActionData.TryGetValue(name, out var data) ? data : null;
            else
                value = session[name];

            return string.IsNullOrEmpty(value) ? null : value;
        }

        public static ActionResult Run(Session session, global::InstallKit.InstallStage stage, global::System.Action<global::InstallKit.InstallEvent> handler)
        {
            var installEvent = new SessionInstallEvent(session, stage);
            try
            {
                handler(installEvent);
            }
            catch (global::System.Exception ex)
            {
                session.Log(""InstallKit: "" + stage + "" handler failed: "" + ex);
                return ActionResult.Failure;
            }

            if (installEvent.IsCanceled)
            {
                session.Log(""InstallKit: "" + stage + "" handler cancelled the install: "" + installEvent.CancelReason);
                return ActionResult.Failure;
            }

            return ActionResult.Success;
        }
    }
}
";
    }
}
