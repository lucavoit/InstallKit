using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace InstallKit.Generators
{
    /// <summary>
    /// Finds installer mistakes while typing, before the installer program runs: an upgrade code that is not a
    /// constant GUID (a new one on every build breaks upgrades), an invalid or duplicated upgrade code, and a
    /// builder created with <c>new</c> instead of <c>Create(...)</c>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class InstallKitAnalyzer : DiagnosticAnalyzer
    {
        const string Category = "InstallKit";

        internal static readonly DiagnosticDescriptor NotConstant = new DiagnosticDescriptor(
            "IK300", "The upgrade code must be a constant GUID",
            "The upgrade code must be a constant GUID string. A value computed at build time (such as Guid.NewGuid()) gives every build a different product, so upgrades stop working. Generate a GUID once and paste it here.",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor InvalidGuid = new DiagnosticDescriptor(
            "IK301", "The upgrade code is not a valid GUID",
            "'{0}' is not a valid GUID, for example 0b6f9a8e-3c1d-4e57-9a43-6d2f1c8b7e10",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        internal static readonly DiagnosticDescriptor Duplicate = new DiagnosticDescriptor(
            "IK302", "The upgrade code is used more than once",
            "The upgrade code '{0}' is also used for another MSI or bundle. Every MSI and bundle needs its own.",
            Category, DiagnosticSeverity.Error, isEnabledByDefault: true, customTags: WellKnownDiagnosticTags.CompilationEnd);

        internal static readonly DiagnosticDescriptor BareBuilder = new DiagnosticDescriptor(
            "IK303", "Create builders with Create(...)",
            "Use {0}.Create(...) instead of 'new {0}()': a builder created with new has none of the model's defaults and would reset them",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(NotConstant, InvalidGuid, Duplicate, BareBuilder);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(start =>
            {
                var msiBuilder = start.Compilation.GetTypeByMetadataName("InstallKit.MsiBuilder");
                var bundleBuilder = start.Compilation.GetTypeByMetadataName("InstallKit.BundleBuilder");
                var serviceBuilder = start.Compilation.GetTypeByMetadataName("InstallKit.ServiceBuilder");
                var msiDefinition = start.Compilation.GetTypeByMetadataName("InstallKit.MsiDefinition");
                var bundleDefinition = start.Compilation.GetTypeByMetadataName("InstallKit.BundleDefinition");

                // Nothing to check in a project that does not use InstallKit.
                if (msiBuilder == null && bundleBuilder == null && msiDefinition == null)
                    return;

                var codes = new ConcurrentBag<(Guid Code, Location Location)>();

                // Reports a problem with the value given as an upgrade code, or remembers the code to find duplicates.
                void CheckUpgradeCode(IOperation value, Location location, Action<Diagnostic> report)
                {
                    value = Unwrap(value);

                    if (value is IDelegateCreationOperation || value.Type?.TypeKind == TypeKind.Delegate)
                    {
                        // The Func<string> overload is evaluated when the installer program runs, not at compile time.
                        report(Diagnostic.Create(NotConstant, location));
                        return;
                    }

                    var constant = value.ConstantValue;
                    if (!constant.HasValue)
                        report(Diagnostic.Create(NotConstant, location));
                    else if (constant.Value is string text && Guid.TryParse(text, out var guid) && guid != Guid.Empty)
                        codes.Add((guid, location));
                    else if (constant.Value is string invalid)
                        report(Diagnostic.Create(InvalidGuid, location, invalid));
                    // A null constant means "no upgrade code yet"; the runtime validator reports it (IK101).
                }

                start.RegisterOperationAction(ctx =>
                {
                    var call = (IInvocationOperation)ctx.Operation;
                    if (call.TargetMethod.Name != "WithUpgradeCode" || call.Arguments.Length != 1)
                        return;

                    var owner = call.TargetMethod.ContainingType;
                    if (Is(owner, msiBuilder) || Is(owner, bundleBuilder))
                        CheckUpgradeCode(call.Arguments[0].Value, call.Arguments[0].Syntax.GetLocation(), ctx.ReportDiagnostic);
                }, OperationKind.Invocation);

                start.RegisterOperationAction(ctx =>
                {
                    var assignment = (ISimpleAssignmentOperation)ctx.Operation;
                    if (!(assignment.Target is IPropertyReferenceOperation property) || property.Property.Name != "UpgradeCode")
                        return;

                    var owner = property.Property.ContainingType;
                    if (Is(owner, msiDefinition) || Is(owner, bundleDefinition))
                        CheckUpgradeCode(assignment.Value, assignment.Value.Syntax.GetLocation(), ctx.ReportDiagnostic);
                }, OperationKind.SimpleAssignment);

                start.RegisterOperationAction(ctx =>
                {
                    var creation = (IObjectCreationOperation)ctx.Operation;
                    var type = creation.Constructor?.ContainingType;
                    // Arguments the caller did not write (optional parameters) do not count.
                    var written = creation.Arguments.Any(a => !a.IsImplicit);
                    if (!written && (Is(type, msiBuilder) || Is(type, bundleBuilder) || Is(type, serviceBuilder)))
                        ctx.ReportDiagnostic(Diagnostic.Create(BareBuilder, creation.Syntax.GetLocation(), type!.Name));
                }, OperationKind.ObjectCreation);

                start.RegisterCompilationEndAction(end =>
                {
                    foreach (var group in codes.GroupBy(c => c.Code).Where(g => g.Count() > 1))
                    foreach (var (code, location) in group)
                        end.ReportDiagnostic(Diagnostic.Create(Duplicate, location, code.ToString("D")));
                });
            });
        }

        static IOperation Unwrap(IOperation operation)
        {
            while (operation is IConversionOperation conversion)
                operation = conversion.Operand;
            return operation;
        }

        static bool Is(ITypeSymbol? type, INamedTypeSymbol? expected) =>
            type != null && expected != null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, expected);
    }
}
