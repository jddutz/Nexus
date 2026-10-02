using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nexus.SourceGenerators;

/// <summary>
/// Validates existing property-change events used by observable types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ObservableAnalyzer : DiagnosticAnalyzer
{
    private const string ObservableAttributeName = "Nexus.Core.ObservableAttribute";
    private const string ObservableInterfaceName = "Nexus.Core.IObservable";

    private static readonly DiagnosticDescriptor InvalidPropertyChangedEvent = new(
        "NXS009",
        "Invalid observable notification event",
        "IObservable.PropertyChanged must have type Action<string>, but '{0}' has type '{1}'",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    /// <summary>
    /// Gets the diagnostics supported by this analyzer.
    /// </summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(InvalidPropertyChangedEvent);

    /// <summary>
    /// Registers the observable event validation analysis.
    /// </summary>
    /// <param name="context">The analyzer initialization context.</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            var observableAttribute = startContext.Compilation.GetTypeByMetadataName(
                ObservableAttributeName
            );
            var observableInterface = startContext.Compilation.GetTypeByMetadataName(
                ObservableInterfaceName
            );
            if (observableAttribute is null || observableInterface is null)
                return;

            startContext.RegisterSymbolAction(
                symbolContext =>
                    AnalyzeType(symbolContext, observableAttribute, observableInterface),
                SymbolKind.NamedType
            );
        });
    }

    /// <summary>
    /// Validates a visible PropertyChanged event on an IObservable type without generated fields.
    /// </summary>
    /// <param name="context">The named-type analysis context.</param>
    /// <param name="observableAttribute">The exact ObservableAttribute symbol.</param>
    /// <param name="observableInterface">The exact IObservable symbol.</param>
    private static void AnalyzeType(
        SymbolAnalysisContext context,
        INamedTypeSymbol observableAttribute,
        INamedTypeSymbol observableInterface
    )
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (
            !type.AllInterfaces.Any(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate, observableInterface)
            )
        )
            return;

        var hasGeneratedFields = type.GetMembers()
            .OfType<IFieldSymbol>()
            .SelectMany(field => field.GetAttributes())
            .Any(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, observableAttribute)
            );
        if (hasGeneratedFields)
            return;

        var visibleMember = type.GetMembers("PropertyChanged").FirstOrDefault();
        var visibleEvent = visibleMember as IEventSymbol;
        if (visibleEvent is not null && !HasRequiredEventType(visibleEvent, context.Compilation))
            ReportInvalidEvent(context, visibleEvent);

        var interfaceEvent = observableInterface
            .GetMembers("PropertyChanged")
            .OfType<IEventSymbol>()
            .FirstOrDefault();
        var implementation = interfaceEvent is null
            ? null
            : type.FindImplementationForInterfaceMember(interfaceEvent) as IEventSymbol;
        if (
            implementation is not null
            && !SymbolEqualityComparer.Default.Equals(implementation, visibleEvent)
            && !HasRequiredEventType(implementation, context.Compilation)
        )
        {
            ReportInvalidEvent(context, implementation);
        }

        if (implementation is null && visibleEvent is null && type.BaseType is not null)
        {
            foreach (var candidateType in GetBaseTypes(type.BaseType))
            {
                var inherited = candidateType
                    .GetMembers("PropertyChanged")
                    .OfType<IEventSymbol>()
                    .FirstOrDefault();
                if (inherited is null)
                    continue;

                if (!HasRequiredEventType(inherited, context.Compilation))
                    ReportInvalidEvent(context, inherited);
                break;
            }
        }
    }

    /// <summary>
    /// Reports an invalid PropertyChanged event at its declaration.
    /// </summary>
    /// <param name="context">The named-type analysis context.</param>
    /// <param name="eventSymbol">The event with an unsupported signature.</param>
    private static void ReportInvalidEvent(SymbolAnalysisContext context, IEventSymbol eventSymbol)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(
                InvalidPropertyChangedEvent,
                eventSymbol.Locations.FirstOrDefault() ?? Location.None,
                eventSymbol.Name,
                eventSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            )
        );
    }

    /// <summary>
    /// Checks whether an event uses the required Action&lt;string&gt; delegate type.
    /// </summary>
    /// <param name="eventSymbol">The event to validate.</param>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns><see langword="true"/> when the event type is exactly Action&lt;string&gt;.</returns>
    private static bool HasRequiredEventType(IEventSymbol eventSymbol, Compilation compilation)
    {
        var actionType = compilation.GetTypeByMetadataName("System.Action`1");
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        return actionType is not null
            && eventSymbol.Type is INamedTypeSymbol eventType
            && SymbolEqualityComparer.Default.Equals(eventType.OriginalDefinition, actionType)
            && eventType.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(eventType.TypeArguments[0], stringType);
    }

    /// <summary>
    /// Enumerates a type and its base classes, most-derived type first.
    /// </summary>
    /// <param name="type">The type whose base chain is traversed.</param>
    /// <returns>The type and its base classes in order.</returns>
    private static IEnumerable<INamedTypeSymbol> GetBaseTypes(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }
}
