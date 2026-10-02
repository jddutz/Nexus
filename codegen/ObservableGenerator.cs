using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Nexus.SourceGenerators;

/// <summary>
/// Generates readable properties and focused change notifications for observable fields.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ObservableGenerator : IIncrementalGenerator
{
    private const string ObservableAttributeName = "Nexus.Core.ObservableAttribute";
    private const string ObservableInterfaceName = "Nexus.Core.IObservable";

    private static readonly DiagnosticDescriptor UnsupportedField = new(
        "NXS001",
        "Unsupported observable field",
        "Observable field '{0}' must be a non-static, non-const, non-readonly instance field",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor NonPartialType = new(
        "NXS002",
        "Observable type must be partial",
        "Type '{0}' must be a partial class, and all containing types must be partial, to generate observable members",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor InvalidPropertyName = new(
        "NXS003",
        "Invalid observable property name",
        "Observable field '{0}' does not have a valid property name; use [Observable(\"Name\")] to specify one",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor DuplicatePropertyName = new(
        "NXS004",
        "Duplicate observable property name",
        "More than one observable field generates the member name '{0}'",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor MemberConflict = new(
        "NXS005",
        "Observable member conflicts with an existing member",
        "Generated observable member '{0}' conflicts with a member already declared in '{1}'",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor SetterConflict = new(
        "NXS012",
        "Observable setter conflicts with an existing member",
        "Observable setter '{0}' conflicts with a member already declared in '{1}'",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor InvalidHook = new(
        "NXS006",
        "Observable hook has an incompatible signature",
        "Hook '{0}' must be an accessible instance method with signature '{1}'",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor MissingNotificationMethod = new(
        "NXS007",
        "Inherited observable event cannot be raised",
        "Type '{0}' inherits IObservable.PropertyChanged but has no accessible void NotifyPropertyChanged(string) method",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor LanguageVersionTooLow = new(
        "NXS008",
        "C# language version is unsupported",
        "Observable generation requires C# 8.0 or later",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor InvalidPropertyChangedEvent = new(
        "NXS011",
        "Invalid observable notification event",
        "IObservable.PropertyChanged must have type Action<string>, but '{0}' has type '{1}'",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    private static readonly DiagnosticDescriptor SealedType = new(
        "NXS010",
        "Observable type cannot be sealed",
        "Type '{0}' must not be sealed because generated setters are virtual",
        "Nexus.Observable",
        DiagnosticSeverity.Error,
        true
    );

    /// <summary>
    /// Registers the syntax-driven observable field pipeline.
    /// </summary>
    /// <param name="context">The incremental generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            ObservableAttributeName,
            static (node, _) => node is VariableDeclaratorSyntax,
            static (attributeContext, _) => CreateCandidate(attributeContext)
        );

        context.RegisterSourceOutput(
            candidates.Collect().Combine(context.CompilationProvider),
            static (productionContext, input) =>
                Generate(productionContext, input.Left, input.Right)
        );
    }

    /// <summary>
    /// Converts an attributed field into the symbol data used by generation.
    /// </summary>
    /// <param name="context">The attributed syntax context.</param>
    /// <returns>The field, containing type, explicit name, and source location.</returns>
    private static (
        IFieldSymbol Field,
        INamedTypeSymbol Type,
        string? ExplicitName,
        Location Location
    ) CreateCandidate(GeneratorAttributeSyntaxContext context)
    {
        var field = (IFieldSymbol)context.TargetSymbol;
        var attribute = context.Attributes[0];
        var explicitName =
            attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;

        return (field, field.ContainingType, explicitName, field.Locations[0]);
    }

    /// <summary>
    /// Validates attributed fields, groups them by type, and emits generated source.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="fields">All observable fields found by the incremental pipeline.</param>
    private static void Generate(
        SourceProductionContext context,
        ImmutableArray<(
            IFieldSymbol Field,
            INamedTypeSymbol Type,
            string? ExplicitName,
            Location Location
        )> fields,
        Compilation compilation
    )
    {
        var groups = new Dictionary<
            INamedTypeSymbol,
            List<(IFieldSymbol Field, string? ExplicitName, Location Location)>
        >(SymbolEqualityComparer.Default);
        foreach (var candidate in fields)
        {
            if (!groups.TryGetValue(candidate.Type, out var members))
            {
                members = [];
                groups.Add(candidate.Type, members);
            }

            members.Add((candidate.Field, candidate.ExplicitName, candidate.Location));
        }

        var generatedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (
            var group in groups
                .OrderBy(item => GetInheritanceDepth(item.Key))
                .ThenBy(item => GetMetadataName(item.Key), StringComparer.Ordinal)
        )
            GenerateType(
                context,
                group.Key,
                group.Value,
                compilation,
                generatedTypes
            );
    }

    /// <summary>
    /// Validates and emits all observable members for one containing type.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="type">The type that contains the annotated fields.</param>
    /// <param name="fields">The annotated fields declared in the type.</param>
    private static void GenerateType(
        SourceProductionContext context,
        INamedTypeSymbol type,
        List<(IFieldSymbol Field, string? ExplicitName, Location Location)> fields,
        Compilation compilation,
        HashSet<INamedTypeSymbol> generatedTypes
    )
    {
        if (!ImplementsObservable(type, compilation))
            return;

        if (type.TypeKind != TypeKind.Class || !AreAllContainingTypesPartial(type))
        {
            foreach (var item in fields)
                context.ReportDiagnostic(
                    Diagnostic.Create(NonPartialType, item.Location, type.Name)
                );
            return;
        }

        var plans =
            new List<(
                IFieldSymbol Field,
                string PropertyName,
                string? AfterHook,
                string? AfterHookWithPreviousValue,
                bool HasPartialAfterHook,
                bool HasPartialAfterHookWithPreviousValue,
                Location Location
            )>();
        foreach (var item in fields)
        {
            if (!SupportsRequiredLanguageVersion(item.Location))
            {
                context.ReportDiagnostic(Diagnostic.Create(LanguageVersionTooLow, item.Location));
                continue;
            }

            if (item.Field.IsStatic || item.Field.IsConst || item.Field.IsReadOnly)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(UnsupportedField, item.Location, item.Field.Name)
                );
                continue;
            }

            var propertyName = item.ExplicitName ?? DerivePropertyName(item.Field.Name);
            if (!IsValidPropertyName(propertyName))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(InvalidPropertyName, item.Location, item.Field.Name)
                );
                continue;
            }

            var afterHook = FindHook(
                context,
                type,
                item.Field,
                "After" + propertyName + "Changes",
                "void",
                compilation,
                out var hasAfterHook,
                out var hasPartialAfterHook,
                withPreviousValue: false,
                reportInvalid: false
            );
            var afterHookWithPreviousValue = FindHook(
                context,
                type,
                item.Field,
                "After" + propertyName + "Changes",
                "void",
                compilation,
                out var hasAfterHookWithPreviousValue,
                out var hasPartialAfterHookWithPreviousValue,
                withPreviousValue: true,
                reportInvalid: false
            );
            if (
                (hasAfterHook || hasAfterHookWithPreviousValue)
                && afterHook is null
                && afterHookWithPreviousValue is null
            )
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        InvalidHook,
                        item.Field.Locations[0],
                        "After" + propertyName + "Changes",
                        "void After"
                            + propertyName
                            + "Changes() or void After"
                            + propertyName
                            + "Changes("
                            + item.Field.Type.ToDisplayString(
                                SymbolDisplayFormat.MinimallyQualifiedFormat
                            )
                            + " previousValue)"
                    )
                );
                continue;
            }
            if (
                afterHook is not null
                    && !hasPartialAfterHook
                    && GetBaseTypeHierarchy(type).First().GetMembers(afterHook).Length > 0
                || afterHookWithPreviousValue is not null
                    && !hasPartialAfterHookWithPreviousValue
                    && GetBaseTypeHierarchy(type)
                        .First()
                        .GetMembers(afterHookWithPreviousValue)
                        .Length > 0
            )
            {
                var hookName = afterHook ?? afterHookWithPreviousValue!;
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        InvalidHook,
                        item.Field.Locations[0],
                        hookName,
                        "protected virtual partial void "
                            + hookName
                            + "("
                            + (
                                afterHookWithPreviousValue is not null
                                    ? item.Field.Type.ToDisplayString(
                                        SymbolDisplayFormat.MinimallyQualifiedFormat
                                    ) + " previousValue"
                                    : ""
                            )
                            + ");"
                    )
                );
                continue;
            }
            plans.Add(
                (
                    item.Field,
                    propertyName,
                    hasAfterHook ? afterHook : null,
                    hasAfterHookWithPreviousValue ? afterHookWithPreviousValue : null,
                    hasPartialAfterHook,
                    hasPartialAfterHookWithPreviousValue,
                    item.Location
                )
            );
        }

        var duplicateNames = plans
            .GroupBy(plan => plan.PropertyName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        var duplicateNameSet = new HashSet<string>(duplicateNames, StringComparer.Ordinal);
        foreach (var plan in plans.Where(plan => duplicateNameSet.Contains(plan.PropertyName)))
            context.ReportDiagnostic(
                Diagnostic.Create(DuplicatePropertyName, plan.Location, plan.PropertyName)
            );
        plans.RemoveAll(plan => duplicateNameSet.Contains(plan.PropertyName));

        var generatedNames = new Dictionary<
            string,
            (string Kind, Location Location, IFieldSymbol Field)
        >(StringComparer.Ordinal);
        var conflictedFields = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
        foreach (var plan in plans)
        {
            var members = new[]
            {
                (Name: plan.PropertyName, Kind: "property"),
                (Name: "__Set" + plan.PropertyName, Kind: "wrapper"),
                (Name: "Set" + plan.PropertyName, Kind: "setter"),
            }.Concat(
                GeneratesChangedEvent(plan.Field)
                    ? [(Name: plan.PropertyName + "Changed", Kind: "event")]
                    : []
            );

            foreach (var member in members)
            {
                if (generatedNames.ContainsKey(member.Name))
                {
                    var first = generatedNames[member.Name];
                    context.ReportDiagnostic(
                        Diagnostic.Create(MemberConflict, plan.Location, member.Name, type.Name)
                    );
                    context.ReportDiagnostic(
                        Diagnostic.Create(MemberConflict, first.Location, member.Name, type.Name)
                    );
                    conflictedFields.Add(plan.Field);
                    conflictedFields.Add(first.Field);
                    continue;
                }
                generatedNames.Add(member.Name, (member.Kind, plan.Location, plan.Field));

                var existingMembers = GetBaseTypeHierarchy(type)
                    .SelectMany(candidateType => candidateType.GetMembers(member.Name))
                    .ToArray();
                var conflict = member.Kind switch
                {
                    "property" or "event" or "wrapper" => existingMembers.Length > 0,
                    "setter" => existingMembers.Any(existing =>
                        existing is not IMethodSymbol method
                        || method.MethodKind == MethodKind.Ordinary
                            && !method.IsStatic
                            && method.Parameters.Length == 1
                            && method.Parameters[0].RefKind == RefKind.None
                            && SymbolEqualityComparer.Default.Equals(
                                method.Parameters[0].Type,
                                plan.Field.Type
                            )
                    ),
                    _ => false,
                };

                if (conflict)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            member.Kind == "setter" ? SetterConflict : MemberConflict,
                            plan.Location,
                            member.Name,
                            type.Name
                        )
                    );
                    conflictedFields.Add(plan.Field);
                }
            }
        }
        plans.RemoveAll(plan => conflictedFields.Contains(plan.Field));

        if (plans.Count == 0)
            return;

        if (type.IsSealed)
        {
            foreach (var plan in plans)
                context.ReportDiagnostic(Diagnostic.Create(SealedType, plan.Location, type.Name));
            return;
        }

        var notification = GetNotificationStrategy(
            context,
            type,
            plans,
            compilation,
            generatedTypes
        );
        if (notification is null)
            return;

        var source = EmitSource(type, plans, notification.Value, compilation);
        context.AddSource(CreateHintName(type), SourceText.From(source, Encoding.UTF8));
        generatedTypes.Add(type);
    }

    /// <summary>
    /// Gets a type's inheritance depth so observable base classes are processed before derived classes.
    /// </summary>
    /// <param name="type">The type whose base classes are counted.</param>
    /// <returns>The number of base classes.</returns>
    private static int GetInheritanceDepth(INamedTypeSymbol type)
    {
        var depth = 0;
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            depth++;
        return depth;
    }

    /// <summary>
    /// Finds an optional hook and diagnoses an expected name with an incompatible signature.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="type">The type containing the annotated field.</param>
    /// <param name="field">The annotated field whose type defines the hook parameter.</param>
    /// <param name="hookName">The expected hook name.</param>
    /// <param name="returnType">The required hook return type.</param>
    /// <param name="compilation">The consuming compilation used to validate accessibility.</param>
    /// <param name="hasHook">Whether a method with the expected name is present.</param>
    /// <returns>The hook name when a matching method exists; otherwise <see langword="null"/>.</returns>
    private static string? FindHook(
        SourceProductionContext context,
        INamedTypeSymbol type,
        IFieldSymbol field,
        string hookName,
        string returnType,
        Compilation compilation,
        out bool hasHook,
        out bool hasPartialImplementation,
        bool withPreviousValue,
        bool reportInvalid
    )
    {
        var namedMembers = GetBaseTypeHierarchy(type)
            .SelectMany(candidateType => candidateType.GetMembers(hookName))
            .ToArray();
        var candidates = namedMembers.OfType<IMethodSymbol>().ToArray();
        hasHook = namedMembers.Length > 0;
        hasPartialImplementation = false;

        foreach (var method in candidates)
        {
            var expectedReturn =
                returnType == "bool"
                    ? method.ReturnType.SpecialType == SpecialType.System_Boolean
                    : method.ReturnsVoid;
            if (
                IsAccessibleFromGeneratedType(method, type, compilation)
                && !method.IsStatic
                && !method.IsGenericMethod
                && (
                    withPreviousValue
                        && method.Parameters.Length == 1
                        && method.Parameters[0].RefKind == RefKind.None
                        && SymbolEqualityComparer.Default.Equals(
                            method.Parameters[0].Type,
                            field.Type
                        )
                    || !withPreviousValue && method.Parameters.Length == 0
                )
                && expectedReturn
            )
            {
                hasPartialImplementation =
                    SymbolEqualityComparer.Default.Equals(method.ContainingType, type)
                    && method.DeclaringSyntaxReferences.Any(reference =>
                    {
                        var declaration = reference.GetSyntax() as MethodDeclarationSyntax;
                        return declaration is not null
                            && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                            && (
                                declaration.Body is not null
                                || declaration.ExpressionBody is not null
                            );
                    });
                return hookName;
            }
        }

        if (hasHook && reportInvalid)
        {
            var signature =
                returnType
                + " "
                + hookName
                + "("
                + (
                    withPreviousValue
                        ? field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                            + " previousValue"
                        : ""
                )
                + ")";
            context.ReportDiagnostic(
                Diagnostic.Create(InvalidHook, field.Locations[0], hookName, signature)
            );
        }

        return null;
    }

    /// <summary>
    /// Selects direct, inherited, or generated property-change notification for the type.
    /// </summary>
    /// <param name="context">The source production context.</param>
    /// <param name="type">The type being generated.</param>
    /// <param name="plans">The valid observable fields.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The notification strategy, or <see langword="null"/> after reporting an error.</returns>
    private static (bool GenerateNotificationMethod, string? NotifyMethod)? GetNotificationStrategy(
        SourceProductionContext context,
        INamedTypeSymbol type,
        List<(
            IFieldSymbol Field,
            string PropertyName,
            string? AfterHook,
            string? AfterHookWithPreviousValue,
            bool HasPartialAfterHook,
            bool HasPartialAfterHookWithPreviousValue,
            Location Location
        )> plans,
        Compilation compilation,
        HashSet<INamedTypeSymbol> generatedTypes
    )
    {
        var observableInterface = compilation.GetTypeByMetadataName(ObservableInterfaceName);
        if (observableInterface is null)
            return null;

        var interfaceEvent = observableInterface!
            .GetMembers("PropertyChanged")
            .OfType<IEventSymbol>()
            .FirstOrDefault();
        var implementation = interfaceEvent is null
            ? null
            : type.FindImplementationForInterfaceMember(interfaceEvent) as IEventSymbol;
        var directMember = type.GetMembers("PropertyChanged").FirstOrDefault();
        if (
            directMember is IEventSymbol directEvent
            && !IsPropertyChangedEvent(directEvent, compilation)
        )
        {
            foreach (var plan in plans)
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        InvalidPropertyChangedEvent,
                        plan.Location,
                        "PropertyChanged",
                        directEvent.Type.ToDisplayString(
                            SymbolDisplayFormat.MinimallyQualifiedFormat
                        )
                    )
                );
            return null;
        }

        if (implementation is not null)
        {
            if (!IsPropertyChangedEvent(implementation, compilation))
            {
                foreach (var plan in plans)
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            InvalidPropertyChangedEvent,
                            plan.Location,
                            implementation.Name,
                            implementation.Type.ToDisplayString(
                                SymbolDisplayFormat.MinimallyQualifiedFormat
                            )
                        )
                    );
                return null;
            }

            if (
                directMember is IEventSymbol implementedDirectEvent
                && SymbolEqualityComparer.Default.Equals(implementation, implementedDirectEvent)
            )
            {
                var declaredNotificationMethod = FindDeclaredNotificationMethod(type, compilation);
                return declaredNotificationMethod is not null
                    ? (false, declaredNotificationMethod)
                    : (true, "NotifyPropertyChanged");
            }

            var notificationMethod = FindNotificationMethod(type, compilation);
            if (notificationMethod is not null)
                return (false, notificationMethod);

            if (
                implementation.ContainingType is INamedTypeSymbol implementationType
                && generatedTypes.Contains(implementationType)
            )
                return (false, "NotifyPropertyChanged");

            foreach (var plan in plans)
                context.ReportDiagnostic(
                    Diagnostic.Create(MissingNotificationMethod, plan.Location, type.Name)
                );
            return null;
        }

        if (directMember is not null)
        {
            foreach (var plan in plans)
                context.ReportDiagnostic(
                    Diagnostic.Create(MemberConflict, plan.Location, "PropertyChanged", type.Name)
                );
            return null;
        }

        var inheritedMember = type.BaseType;
        while (inheritedMember is not null)
        {
            var inherited = inheritedMember.GetMembers("PropertyChanged").FirstOrDefault();
            if (inherited is not null)
            {
                var notificationMethod = FindNotificationMethod(type, compilation);
                if (
                    inherited is IEventSymbol inheritedEvent
                    && IsPropertyChangedEvent(inheritedEvent, compilation)
                )
                {
                    if (notificationMethod is not null)
                        return (false, notificationMethod);

                    if (generatedTypes.Contains(inheritedMember))
                        return (false, "NotifyPropertyChanged");
                }

                foreach (var plan in plans)
                    context.ReportDiagnostic(
                        Diagnostic.Create(MissingNotificationMethod, plan.Location, type.Name)
                    );
                return null;
            }
            inheritedMember = inheritedMember.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Determines whether a type implements the observable contract directly or through a base type.
    /// </summary>
    /// <param name="type">The type containing observable fields.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns><see langword="true"/> when the type implements <see cref="ObservableInterfaceName"/>.</returns>
    private static bool ImplementsObservable(INamedTypeSymbol type, Compilation compilation)
    {
        var observableInterface = compilation.GetTypeByMetadataName(ObservableInterfaceName);
        return observableInterface is not null
            && type.AllInterfaces.Any(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate, observableInterface)
            );
    }

    /// <summary>
    /// Emits a partial declaration containing generated properties, events, and setters.
    /// </summary>
    /// <param name="type">The type receiving the generated members.</param>
    /// <param name="plans">The validated observable fields.</param>
    /// <param name="notification">The notification strategy selected for the type.</param>
    /// <param name="compilation">The consuming compilation's options and language version.</param>
    /// <returns>The deterministic source text for the type.</returns>
    private static string EmitSource(
        INamedTypeSymbol type,
        List<(
            IFieldSymbol Field,
            string PropertyName,
            string? AfterHook,
            string? AfterHookWithPreviousValue,
            bool HasPartialAfterHook,
            bool HasPartialAfterHookWithPreviousValue,
            Location Location
        )> plans,
        (
            bool GenerateNotificationMethod,
            string? NotifyMethod
        ) notification,
        Compilation compilation
    )
    {
        var builder = new StringBuilder();
        var nullableDirective = GetNullableDirective(
            compilation,
            plans.Any(plan =>
                plan.Field.NullableAnnotation == NullableAnnotation.Annotated
                || HasTopLevelNullableSyntax(plan.Field)
                || HasNullableTypeAnnotation(plan.Field.Type)
            ),
            out var nullableAnnotationsEnabled,
            out var nullableWarningsEnabled
        );
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine(nullableDirective);
        builder.AppendLine("#pragma warning disable CS0067");

        var hasNamespace = !type.ContainingNamespace.IsGlobalNamespace;
        if (hasNamespace)
        {
            builder
                .Append("namespace ")
                .Append(type.ContainingNamespace.ToDisplayString())
                .AppendLine();
            builder.AppendLine("{");
        }

        var containingTypes = GetTypeHierarchy(type).Reverse().ToArray();
        foreach (var containingType in containingTypes)
        {
            builder.Append("partial ");
            if (containingType.IsStatic)
                builder.Append("static ");
            else if (containingType.TypeKind == TypeKind.Struct && containingType.IsReadOnly)
                builder.Append("readonly ");
            if (containingType.IsRefLikeType)
                builder.Append("ref ");
            if (containingType.IsRecord)
                builder.Append(
                    containingType.TypeKind == TypeKind.Struct ? "record struct " : "record class "
                );
            else if (containingType.TypeKind == TypeKind.Struct)
                builder.Append("struct ");
            else if (containingType.TypeKind == TypeKind.Interface)
                builder.Append("interface ");
            else
                builder.Append("class ");
            builder.Append(EscapeIdentifier(containingType.Name));
            AppendTypeParameters(builder, containingType);
            AppendTypeConstraints(builder, containingType);
            builder.AppendLine();
            builder.AppendLine("{");
        }

        foreach (var plan in plans)
        {
            if (plan.AfterHook is not null && plan.HasPartialAfterHook)
            {
                builder
                    .AppendLine("    /// <summary>Runs after the property value changes.</summary>");
                builder
                    .Append("    protected virtual partial void ")
                    .Append(EscapeIdentifier(plan.AfterHook))
                    .AppendLine("();");
            }
            else if (
                plan.AfterHook is null
                && !HasInheritedHook(type, plan.PropertyName, false, plan.Field.Type)
            )
            {
                builder
                    .AppendLine("    /// <summary>Runs after the property value changes.</summary>");
                builder
                    .Append("    protected virtual void After")
                    .Append(EscapeIdentifier(plan.PropertyName))
                    .AppendLine("Changes() { }");
            }

            if (
                plan.AfterHookWithPreviousValue is not null
                && plan.HasPartialAfterHookWithPreviousValue
            )
            {
                var hookTypeName = GetHookTypeName(plan.Field);
                builder
                    .AppendLine("    /// <summary>Runs after the property value changes.</summary>")
                    .AppendLine(
                        "    /// <param name=\"previousValue\">The value before the change.</param>"
                    );
                builder
                    .Append("    protected virtual partial void ")
                    .Append(EscapeIdentifier(plan.AfterHookWithPreviousValue))
                    .Append('(')
                    .Append(hookTypeName)
                    .AppendLine(" previousValue);");
            }
            else if (
                plan.AfterHookWithPreviousValue is null
                && !HasInheritedHook(type, plan.PropertyName, true, plan.Field.Type)
            )
            {
                var hookTypeName = GetHookTypeName(plan.Field);
                builder
                    .AppendLine("    /// <summary>Runs after the property value changes.</summary>")
                    .AppendLine(
                        "    /// <param name=\"previousValue\">The value before the change.</param>"
                    );
                builder
                    .Append("    protected virtual void After")
                    .Append(EscapeIdentifier(plan.PropertyName))
                    .Append("Changes(")
                    .Append(hookTypeName)
                    .AppendLine(" previousValue) { }");
            }
        }

        if (notification.GenerateNotificationMethod)
        {
            builder.AppendLine("    /// <summary>Raises the property-change event.</summary>");
            builder.AppendLine(
                "    /// <param name=\"propertyName\">The name of the changed property.</param>"
            );
            builder.AppendLine(
                "    protected void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(propertyName);"
            );
        }

        foreach (var plan in plans)
            EmitObservableMembers(
                builder,
                plan,
                notification,
                nullableAnnotationsEnabled,
                nullableWarningsEnabled
            );

        for (var index = containingTypes.Length - 1; index >= 0; index--)
            builder.AppendLine("}");

        if (hasNamespace)
            builder.AppendLine("}");

        return builder.ToString();
    }

    /// <summary>
    /// Determines whether the observable field requests a public property setter.
    /// </summary>
    /// <param name="field">The field annotated with <see cref="ObservableAttributeName"/>.</param>
    /// <returns><see langword="true"/> when the generated property should expose a setter.</returns>
    private static bool UsesPublicSetter(IFieldSymbol field)
    {
        foreach (var attribute in field.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != ObservableAttributeName)
                continue;

            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "PublicSetter" && argument.Value.Value is bool publicSetter)
                    return publicSetter;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the fully qualified hook parameter type, preserving a nullable field annotation.
    /// </summary>
    /// <param name="field">The observable backing field.</param>
    /// <returns>The hook parameter type name.</returns>
    private static string GetHookTypeName(IFieldSymbol field)
    {
        var typeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (
            (
                field.NullableAnnotation == NullableAnnotation.Annotated
                || HasTopLevelNullableSyntax(field)
            ) && !typeName.EndsWith("?", StringComparison.Ordinal)
        )
            typeName += "?";

        return typeName;
    }

    /// <summary>
    /// Determines whether a base type already provides a matching generated hook.
    /// </summary>
    /// <param name="type">The type receiving generated members.</param>
    /// <param name="propertyName">The observable property name.</param>
    /// <param name="withPreviousValue">Whether the hook receives the previous value.</param>
    /// <param name="fieldType">The observable field type.</param>
    /// <returns><see langword="true"/> when an inherited hook is available.</returns>
    private static bool HasInheritedHook(
        INamedTypeSymbol type,
        string propertyName,
        bool withPreviousValue,
        ITypeSymbol fieldType
    ) =>
        GetBaseTypeHierarchy(type)
            .Skip(1)
            .SelectMany(candidate => candidate.GetMembers("After" + propertyName + "Changes"))
            .OfType<IMethodSymbol>()
            .Any(method =>
                !method.IsStatic
                && method.ReturnsVoid
                && (
                    withPreviousValue
                        ? method.Parameters.Length == 1
                            && SymbolEqualityComparer.Default.Equals(
                                method.Parameters[0].Type,
                                fieldType
                            )
                        : method.Parameters.Length == 0
                )
            );

    /// <summary>Emits the property, typed event, and mutation methods for one backing field.</summary>
    /// </summary>
    /// <param name="builder">The source text builder.</param>
    /// <param name="plan">The validated field and its generated hook names.</param>
    /// <param name="notification">The type-level notification strategy.</param>
    /// <param name="nullableAnnotationsEnabled">Whether nullable annotations are enabled.</param>
    /// <param name="nullableWarningsEnabled">Whether nullable warnings are enabled.</param>
    private static void EmitObservableMembers(
        StringBuilder builder,
        (
            IFieldSymbol Field,
            string PropertyName,
            string? AfterHook,
            string? AfterHookWithPreviousValue,
            bool HasPartialAfterHook,
            bool HasPartialAfterHookWithPreviousValue,
            Location Location
        ) plan,
        (
            bool GenerateNotificationMethod,
            string? NotifyMethod
        ) notification,
        bool nullableAnnotationsEnabled,
        bool nullableWarningsEnabled
    )
    {
        var fieldName = EscapeIdentifier(plan.Field.Name);
        var propertyName = EscapeIdentifier(plan.PropertyName);
        var setterName = EscapeIdentifier("Set" + plan.PropertyName);
        var eventName = EscapeIdentifier(plan.PropertyName + "Changed");
        var generatesChangedEvent = GeneratesChangedEvent(plan.Field);
        var hasTopLevelNullableAnnotation =
            plan.Field.NullableAnnotation == NullableAnnotation.Annotated
            || HasTopLevelNullableSyntax(plan.Field);
        var typeName = plan.Field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (hasTopLevelNullableAnnotation && !typeName.EndsWith("?", StringComparison.Ordinal))
            typeName += "?";

        builder
            .Append("    /// <summary>Gets the value of ")
            .Append(propertyName)
            .AppendLine(".</summary>");
        builder
            .Append("    public ")
            .Append(typeName)
            .Append(' ')
            .Append(propertyName)
            .AppendLine()
            .AppendLine("    {")
            .Append("        get => ")
            .Append(fieldName)
            .AppendLine(";");
        builder.Append("        ");
        if (!UsesPublicSetter(plan.Field))
            builder.Append("protected ");
        builder.Append("set => __Set").Append(propertyName).AppendLine("(value);");
        builder.AppendLine("    }");
        if (generatesChangedEvent)
        {
            builder
                .Append("    /// <summary>Occurs when ")
                .Append(propertyName)
                .AppendLine(" changes.</summary>");
            builder
                .Append("    public event global::System.Action<")
                .Append(typeName)
                .Append(", ")
                .Append(typeName)
                .Append('>');
            if (nullableAnnotationsEnabled)
                builder.Append('?');
            builder.Append(' ').Append(eventName);
            if (nullableWarningsEnabled && !nullableAnnotationsEnabled)
                builder.Append(" = null!");
            builder.AppendLine(";");
        }
        builder.AppendLine(
            "    /// <summary>Sets the value and raises change notifications.</summary>"
        );
        builder.AppendLine("    /// <param name=\"value\">The value to assign.</param>");
        builder
            .Append("    private void __Set")
            .Append(propertyName)
            .Append('(')
            .Append(typeName)
            .AppendLine(" value)");
        builder.AppendLine("    {");
        builder
            .Append("        if (global::System.Collections.Generic.EqualityComparer<")
            .Append(typeName)
            .Append(">.Default.Equals(value, ")
            .Append(fieldName)
            .AppendLine("))");
        builder.AppendLine("            return;");
        builder.AppendLine();
        builder.Append("        var previousValue = ").Append(fieldName).AppendLine(";");
        builder.Append("        ").Append(setterName).AppendLine("(value);");
        builder
            .Append("        if (global::System.Collections.Generic.EqualityComparer<")
            .Append(typeName)
            .Append(">.Default.Equals(previousValue, ")
            .Append(fieldName)
            .AppendLine("))");
        builder.AppendLine("            return;");
        builder.AppendLine();
        builder.Append("        var assignedValue = ").Append(fieldName).AppendLine(";");
        builder
            .Append("        After")
            .Append(EscapeIdentifier(plan.PropertyName))
            .AppendLine("Changes();");
        builder
            .Append("        After")
            .Append(EscapeIdentifier(plan.PropertyName))
            .AppendLine("Changes(previousValue);");

        builder
            .Append("        ")
            .Append(EscapeIdentifier(notification.NotifyMethod!))
            .Append("(nameof(")
            .Append(propertyName)
            .AppendLine("));");

        if (generatesChangedEvent)
            builder
                .Append("        ")
                .Append(eventName)
                .AppendLine("?.Invoke(previousValue, assignedValue);");
        builder.AppendLine("    }");

        builder.AppendLine("    /// <summary>Assigns the value of the property.</summary>");
        builder.AppendLine("    /// <param name=\"value\">The value to assign.</param>");
        builder
            .Append("    ")
            .Append("protected virtual void ")
            .Append(setterName)
            .Append('(')
            .Append(typeName)
            .AppendLine(" value)");
        builder.AppendLine("    {");
        builder.Append("        ").Append(fieldName).AppendLine(" = value;");
        builder.AppendLine("    }");
    }

    /// <summary>
    /// Determines whether the field requests a generated typed change event.
    /// </summary>
    /// <param name="field">The observable field.</param>
    /// <returns><see langword="true"/> unless generation is disabled by the attribute.</returns>
    private static bool GeneratesChangedEvent(IFieldSymbol field)
    {
        foreach (var attribute in field.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != ObservableAttributeName)
                continue;

            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "GenerateChangedEvent" && argument.Value.Value is bool value)
                    return value;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks that the type and each containing type have partial declarations.
    /// </summary>
    /// <param name="type">The type that contains the annotated field.</param>
    /// <returns><see langword="true"/> when every declaration is partial.</returns>
    private static bool AreAllContainingTypesPartial(INamedTypeSymbol type)
    {
        foreach (var candidate in GetTypeHierarchy(type))
        {
            if (candidate.DeclaringSyntaxReferences.Length == 0)
                return false;

            foreach (var reference in candidate.DeclaringSyntaxReferences)
            {
                if (
                    reference.GetSyntax() is not TypeDeclarationSyntax declaration
                    || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                )
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns type symbols from the target type through its outermost containing type.
    /// </summary>
    /// <param name="type">The nested or top-level type.</param>
    /// <returns>The type hierarchy ordered from inner to outer.</returns>
    private static IEnumerable<INamedTypeSymbol> GetTypeHierarchy(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            yield return current;
    }

    /// <summary>
    /// Derives a PascalCase property name from a single-underscore field name.
    /// </summary>
    /// <param name="fieldName">The field's source identifier.</param>
    /// <returns>The derived property name, or an empty string when derivation is unsupported.</returns>
    private static string DerivePropertyName(string fieldName)
    {
        if (fieldName.Length < 2 || fieldName[0] != '_')
            return string.Empty;

        return char.ToUpperInvariant(fieldName[1]) + fieldName.Substring(2);
    }

    /// <summary>
    /// Checks whether a property name is a single valid C# identifier.
    /// </summary>
    /// <param name="name">The proposed property name.</param>
    /// <returns><see langword="true"/> when the name can be emitted as an identifier.</returns>
    private static bool IsValidPropertyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name![0] == '@')
            return false;

        return SyntaxFacts.IsValidIdentifier(name)
            || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None;
    }

    /// <summary>
    /// Escapes a keyword when it is emitted as a C# identifier.
    /// </summary>
    /// <param name="identifier">The identifier value without an escape prefix.</param>
    /// <returns>The identifier's source spelling.</returns>
    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None
        || SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None
            ? "@" + identifier
            : identifier;

    /// <summary>
    /// Checks whether an event implements the required string property-change contract.
    /// </summary>
    /// <param name="eventSymbol">The event to inspect.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns><see langword="true"/> when the event has type <c>Action&lt;string&gt;</c>.</returns>
    private static bool IsPropertyChangedEvent(IEventSymbol eventSymbol, Compilation compilation)
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
    /// Finds an accessible notification method that can raise an inherited event.
    /// </summary>
    /// <param name="type">The generated type.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The method name when a suitable method exists.</returns>
    private static string? FindNotificationMethod(INamedTypeSymbol type, Compilation compilation)
    {
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        foreach (var candidateType in GetBaseTypeHierarchy(type))
        {
            foreach (
                var method in candidateType
                    .GetMembers("NotifyPropertyChanged")
                    .OfType<IMethodSymbol>()
            )
            {
                if (CanNotifyPropertyChanged(method, type, compilation, stringType))
                    return method.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a compatible notification method declared by the class that owns the event.
    /// </summary>
    /// <param name="type">The class that declares the event.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>The method name when a compatible method is declared.</returns>
    private static string? FindDeclaredNotificationMethod(
        INamedTypeSymbol type,
        Compilation compilation
    )
    {
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        return type.GetMembers("NotifyPropertyChanged")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(method => CanNotifyPropertyChanged(method, type, compilation, stringType))
            ?.Name;
    }

    /// <summary>
    /// Checks whether a notification method can be called from generated code.
    /// </summary>
    /// <param name="method">The candidate method.</param>
    /// <param name="type">The generated class.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="stringType">The compilation's string type symbol.</param>
    /// <returns><see langword="true"/> when the method accepts one string and returns void.</returns>
    private static bool CanNotifyPropertyChanged(
        IMethodSymbol method,
        INamedTypeSymbol type,
        Compilation compilation,
        ITypeSymbol stringType
    ) =>
        IsAccessibleFromGeneratedType(method, type, compilation)
        && !method.IsStatic
        && method.ReturnsVoid
        && !method.IsGenericMethod
        && method.Parameters.Length == 1
        && method.Parameters[0].RefKind == RefKind.None
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stringType);

    /// <summary>
    /// Determines whether a method can be called from generated code in the target type.
    /// </summary>
    /// <param name="method">The candidate method.</param>
    /// <param name="targetType">The type receiving generated code.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns><see langword="true"/> when the method is accessible.</returns>
    private static bool IsAccessibleFromGeneratedType(
        IMethodSymbol method,
        INamedTypeSymbol targetType,
        Compilation compilation
    ) => compilation.IsSymbolAccessibleWithin(method, targetType);

    /// <summary>
    /// Formats the nullable context for generated source and reports annotation support.
    /// </summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="requiresNullableAnnotations">Whether generated signatures include annotated types.</param>
    /// <param name="annotationsEnabled">Whether nullable annotations are enabled.</param>
    /// <param name="warningsEnabled">Whether nullable warnings are enabled.</param>
    /// <returns>The nullable directive matching the project configuration.</returns>
    private static string GetNullableDirective(
        Compilation compilation,
        bool requiresNullableAnnotations,
        out bool annotationsEnabled,
        out bool warningsEnabled
    )
    {
        var options = compilation.Options as CSharpCompilationOptions;
        var nullableContext = options?.NullableContextOptions ?? NullableContextOptions.Disable;
        annotationsEnabled =
            nullableContext is NullableContextOptions.Enable or NullableContextOptions.Annotations
            || requiresNullableAnnotations;
        warningsEnabled =
            nullableContext is NullableContextOptions.Enable or NullableContextOptions.Warnings;
        return nullableContext switch
        {
            NullableContextOptions.Enable => "#nullable enable",
            NullableContextOptions.Annotations => "#nullable enable annotations",
            NullableContextOptions.Warnings => requiresNullableAnnotations
                ? "#nullable enable"
                : "#nullable enable warnings",
            NullableContextOptions.Disable when requiresNullableAnnotations =>
                "#nullable enable annotations",
            _ => "#nullable disable",
        };
    }

    /// <summary>
    /// Checks whether a field type or one of its nested type arguments carries nullable annotation.
    /// </summary>
    /// <param name="type">The field type to inspect.</param>
    /// <returns><see langword="true"/> when nullable syntax is needed in generated source.</returns>
    private static bool HasNullableTypeAnnotation(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
            return true;

        return type switch
        {
            IArrayTypeSymbol arrayType => HasNullableTypeAnnotation(arrayType.ElementType),
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(HasNullableTypeAnnotation),
            IPointerTypeSymbol pointerType => HasNullableTypeAnnotation(pointerType.PointedAtType),
            _ => false,
        };
    }

    /// <summary>
    /// Checks whether a field declaration explicitly wraps its type in nullable syntax.
    /// </summary>
    /// <param name="field">The field whose declaration is inspected.</param>
    /// <returns><see langword="true"/> when the declared field type ends in a nullable marker.</returns>
    private static bool HasTopLevelNullableSyntax(IFieldSymbol field)
    {
        foreach (var reference in field.DeclaringSyntaxReferences)
        {
            if (
                reference.GetSyntax() is VariableDeclaratorSyntax variable
                && variable.Parent is VariableDeclarationSyntax declaration
                && declaration.Type is NullableTypeSyntax
            )
                return true;
        }

        return false;
    }

    /// <summary>
    /// Appends the type parameters declared by a partial type.
    /// </summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="type">The type whose parameters are emitted.</param>
    private static void AppendTypeParameters(StringBuilder builder, INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0)
            return;

        builder.Append('<');
        builder.Append(
            string.Join(
                ", ",
                type.TypeParameters.Select(parameter => EscapeIdentifier(parameter.Name))
            )
        );
        builder.Append('>');
    }

    /// <summary>
    /// Appends the constraints declared by a generic partial type.
    /// </summary>
    /// <param name="builder">The generated source builder.</param>
    /// <param name="type">The generic type whose constraints are emitted.</param>
    private static void AppendTypeConstraints(StringBuilder builder, INamedTypeSymbol type)
    {
        foreach (var parameter in type.TypeParameters)
        {
            var constraints = new List<string>();
            if (parameter.HasUnmanagedTypeConstraint)
                constraints.Add("unmanaged");
            else if (parameter.HasValueTypeConstraint)
                constraints.Add("struct");
            else if (parameter.HasReferenceTypeConstraint)
                constraints.Add(
                    parameter.ReferenceTypeConstraintNullableAnnotation
                    == NullableAnnotation.Annotated
                        ? "class?"
                        : "class"
                );
            else if (parameter.HasNotNullConstraint)
                constraints.Add("notnull");

            constraints.AddRange(
                parameter.ConstraintTypes.Select(constraint =>
                    constraint.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                )
            );
            if (parameter.HasConstructorConstraint)
                constraints.Add("new()");

            if (constraints.Count > 0)
                builder
                    .Append("    where ")
                    .Append(EscapeIdentifier(parameter.Name))
                    .Append(" : ")
                    .AppendLine(string.Join(", ", constraints));
        }
    }

    /// <summary>
    /// Builds a deterministic, collision-resistant hint name from a metadata name.
    /// </summary>
    /// <param name="type">The type receiving generated source.</param>
    /// <returns>A source-generator hint name unique to the full nested generic type.</returns>
    private static string CreateHintName(INamedTypeSymbol type)
    {
        var metadataName = GetMetadataName(type);
        var builder = new StringBuilder("Observable_");
        foreach (var character in metadataName)
            builder.Append(
                ((int)character).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)
            );
        return builder.Append(".g.cs").ToString();
    }

    /// <summary>
    /// Builds the namespace-qualified metadata name, including containing types and arities.
    /// </summary>
    /// <param name="type">The type whose metadata name is requested.</param>
    /// <returns>The stable metadata name.</returns>
    private static string GetMetadataName(INamedTypeSymbol type)
    {
        var names = GetTypeHierarchy(type).Select(candidate => candidate.MetadataName).Reverse();
        var namespaceName = type.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : type.ContainingNamespace.ToDisplayString() + ".";
        return namespaceName + string.Join("+", names);
    }

    /// <summary>
    /// Checks whether the source declaration uses C# 8.0 or a newer effective language version.
    /// </summary>
    /// <param name="location">The annotated field's source location.</param>
    /// <returns><see langword="true"/> when the required language features are available.</returns>
    private static bool SupportsRequiredLanguageVersion(Location location)
    {
        if (location.SourceTree?.Options is not CSharpParseOptions parseOptions)
            return true;

        var version = parseOptions.LanguageVersion;
        return version
                is LanguageVersion.Default
                    or LanguageVersion.Latest
                    or LanguageVersion.Preview
            || version >= LanguageVersion.CSharp8;
    }

    /// <summary>
    /// Returns the target type and its base classes, most-derived type first.
    /// </summary>
    /// <param name="type">The type whose base chain is returned.</param>
    /// <returns>The type and each base class in order.</returns>
    private static IEnumerable<INamedTypeSymbol> GetBaseTypeHierarchy(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }
}
