using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Nexus.SourceGenerators;

namespace Nexus.SourceGenerators.Tests;

/// <summary>
/// Exercises observable generation and diagnostics using isolated Roslyn compilations.
/// </summary>
public sealed class ObservableSourceGeneratorTests
{
    private static readonly ImmutableArray<MetadataReference> PlatformReferences = (
        (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!
    )
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToImmutableArray<MetadataReference>();

    private const string ObservableContract = """
        using System;
        using System.Collections.Generic;
        using Nexus.Core;
        namespace Nexus.Core
        {
            [AttributeUsage(AttributeTargets.Field)]
            public sealed class ObservableAttribute(string? propertyName = null) : Attribute
            {
                public string? PropertyName { get; } = propertyName;
                public bool PublicSetter { get; set; } = true;
            }

            public interface IObservable
            {
                event Action<string>? PropertyChanged;
            }
        }
        """;

    /// <summary>
    /// Verifies generated source compiles and that the generated setters preserve v1 behavior.
    /// </summary>
    [Fact]
    public void GeneratedSettersHonorHooksNotificationsAndReentrancy()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class ObservableTarget : IObservable
                    {
                        [Nexus.Core.Observable]
                        private int _count = 3;
                        private int? _reentrantValue;
                        public List<string> Calls { get; } = new();
                        public event Action<string>? PropertyChanged;

                        private bool ValidateCount(int value)
                        {
                            Calls.Add($"validate:{value}");
                            return value >= 0;
                        }

                        private void BeforeCountChanges(int value) => Calls.Add($"before:{value}");

                        private void AfterCountChanges(int previousValue)
                        {
                            Calls.Add($"after:{previousValue}");
                            if (_reentrantValue is not int value)
                                return;
                            _reentrantValue = null;
                            SetCount(value);
                        }

                        public void ScheduleReentrantChange(int value) => _reentrantValue = value;
                    }

                    public partial class PlainTarget
                    {
                        [Nexus.Core.Observable]
                        private string _displayName = "initial";

                        [Nexus.Core.Observable("ExplicitName")]
                        private int _value;

                        [Nexus.Core.Observable]
                        private int __cached;
                    }

                    public partial class GeneratedNotificationTarget : IObservable
                    {
                        [Nexus.Core.Observable]
                        private int _amount;
                    }

                    public partial class GenericOuter<T> where T : class, new()
                    {
                        public partial class Nested<U> where U : IComparable<U>
                        {
                            [Nexus.Core.Observable]
                            private U _value = default!;
                        }
                    }

                    public static class RuntimeProbe
                    {
                        public static string Run()
                        {
                            var target = new ObservableTarget();
                            var propertyNotifications = new List<string>();
                            var valueChanges = new List<string>();
                            target.PropertyChanged += propertyNotifications.Add;
                            target.CountChanged += (previous, current) => valueChanges.Add($"{previous}:{current}");
                            target.SetCount(3);
                            target.SetCount(-1);
                            target.ScheduleReentrantChange(9);
                            target.SetCount(5);

                            var plain = new PlainTarget();
                            var plainChanges = new List<string>();
                            plain.DisplayNameChanged += (previous, current) => plainChanges.Add($"{previous}:{current}");
                            plain.SetDisplayName("updated");

                            var generatedNotification = new GeneratedNotificationTarget();
                            var generatedPropertyName = string.Empty;
                            generatedNotification.PropertyChanged += name => generatedPropertyName = name;
                            generatedNotification.SetAmount(4);

                            return string.Join(",", target.Calls)
                                + "|" + string.Join(",", propertyNotifications)
                                + "|" + string.Join(",", valueChanges)
                                + "|" + target.Count
                                + "|" + plain.DisplayName
                                + "|" + string.Join(",", plainChanges)
                                + "|" + (typeof(PlainTarget).GetEvent("PropertyChanged") is null)
                                + "|" + generatedPropertyName
                                + "|" + generatedNotification.Amount;
                        }
                    }
                }
                """;

        var result = RunGenerator(source);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );

        using var assemblyStream = new MemoryStream();
        var emitResult = result.Compilation.Emit(assemblyStream);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
        assemblyStream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
        var runMethod = assembly.GetType("Probe.RuntimeProbe")!.GetMethod("Run")!;
        var output = (string)runMethod.Invoke(null, null)!;

        Assert.Equal(
            "validate:-1,validate:5,before:5,after:3,validate:9,before:9,after:5"
                + "|Count,Count|5:9,3:5|9|updated|initial:updated|True|Amount|4",
            output
        );
    }

    /// <summary>
    /// Verifies generated setters are public by default and can be protected explicitly.
    /// </summary>
    [Fact]
    public void SettersArePublicByDefaultAndCanBeProtected()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class Target
                    {
                        [Nexus.Core.Observable(PublicSetter = false)]
                        private int _internal;

                        [Nexus.Core.Observable]
                        private int _external;

                        [Nexus.Core.Observable(PublicSetter = true)]
                        private int _explicitPublic;
                    }
                }
                """;

        var result = RunGenerator(source);
        var generated = Assert.Single(result.GeneratedSources).SourceText.ToString();

        Assert.Contains(
            "protected virtual void SetInternal(int value)",
            generated,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "public virtual void SetExternal(int value)",
            generated,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "public virtual void SetExplicitPublic(int value)",
            generated,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
    }

    /// <summary>
    /// Verifies non-partial declarations produce a diagnostic instead of invalid generated code.
    /// </summary>
    [Fact]
    public void NonPartialContainingTypeProducesDiagnostic()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public class NotPartial
                    {
                        [Nexus.Core.Observable]
                        private int _value;
                    }
                }
                """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "NXSOBS002");
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// Verifies explicit names override derivation and only one leading underscore is removed.
    /// </summary>
    [Fact]
    public void ExplicitNamesOverrideSingleUnderscoreDerivation()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class Target
                    {
                        [Nexus.Core.Observable("ExplicitName")]
                        private int _value;

                        [Nexus.Core.Observable]
                        private int __cached;
                    }
                }
                """;

        var result = RunGenerator(source);
        var generated = Assert.Single(result.GeneratedSources).SourceText.ToString();

        Assert.Contains("int ExplicitName => _value", generated, StringComparison.Ordinal);
        Assert.Contains("int _cached => __cached", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
    }

    /// <summary>
    /// Verifies an unrelated short-named attribute does not trigger observable generation.
    /// </summary>
    [Fact]
    public void AttributeMatchingUsesTheFullyQualifiedSymbol()
    {
        var source =
            ObservableContract
            + """
                using System;
                namespace Other
                {
                    [AttributeUsage(AttributeTargets.Field)]
                    public sealed class ObservableAttribute : Attribute { }
                }
                namespace Probe
                {
                    public partial class Target
                    {
                        [Other.Observable]
                        private int _value;
                    }
                }
                """;

        var result = RunGenerator(source);

        Assert.Empty(result.GeneratedSources);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Id.StartsWith("NXSOBS", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Verifies inherited events without a raiser are diagnosed rather than shadowed.
    /// </summary>
    [Fact]
    public void InheritedNotificationWithoutRaiserProducesDiagnostic()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public abstract class ObservableBase : Nexus.Core.IObservable
                    {
                        public event Action<string>? PropertyChanged;
                    }

                    public partial class Target : ObservableBase
                    {
                        [Nexus.Core.Observable]
                        private int _value;
                    }
                }
                """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "NXSOBS007");
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// Verifies invalid field, name, duplicate, conflict, and hook declarations are diagnosed.
    /// </summary>
    /// <param name="members">Members included in the partial type.</param>
    /// <param name="expectedDiagnosticId">The diagnostic ID expected for the invalid declaration.</param>
    [Theory]
    [InlineData("[Nexus.Core.Observable] private readonly int _value;", "NXSOBS001")]
    [InlineData("[Nexus.Core.Observable(\"\")] private int _value;", "NXSOBS003")]
    [InlineData(
        "[Nexus.Core.Observable(\"Name\")] private int _first; [Nexus.Core.Observable(\"Name\")] private int _second;",
        "NXSOBS004"
    )]
    [InlineData("public int Value => 0; [Nexus.Core.Observable] private int _value;", "NXSOBS005")]
    [InlineData(
        "public int Value => 0; public virtual void SetValue(int value) { } [Nexus.Core.Observable] private int _value;",
        "NXSOBS005"
    )]
    [InlineData(
        "private bool ValidateValue(string value) => true; [Nexus.Core.Observable] private int _value;",
        "NXSOBS006"
    )]
    public void InvalidAuthoringPatternsProduceDiagnostics(
        string members,
        string expectedDiagnosticId
    )
    {
        var source =
            ObservableContract
            + $$"""
                namespace Probe
                {
                    public partial class Target
                    {
                        {{members}}
                    }
                }
                """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == expectedDiagnosticId);
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// Verifies inherited generated-name conflicts are diagnosed instead of hidden.
    /// </summary>
    [Fact]
    public void InheritedGeneratedMemberConflictsProduceDiagnostics()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public class Base
                    {
                        public int Value => 1;
                        public virtual void SetCurrent(int value) { }
                        public event Action<int, int>? NameChanged;
                    }

                    public partial class Target : Base
                    {
                        [Nexus.Core.Observable]
                        private int _value;

                        [Nexus.Core.Observable]
                        private int _current;

                        [Nexus.Core.Observable]
                        private int _name;
                    }
                }
                """;

        var result = RunGenerator(source);

        Assert.Equal(3, result.Diagnostics.Count(diagnostic => diagnostic.Id == "NXSOBS005"));
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// Verifies inherited events and hooks are called through accessible base members.
    /// </summary>
    [Fact]
    public void InheritedNotificationAndHooksAreUsedWithoutShadowingTheEvent()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public class ObservableBase : Nexus.Core.IObservable
                    {
                        public event Action<string>? PropertyChanged;
                        public List<string> Calls { get; } = new();
                        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(propertyName);
                        protected bool ValidateValue(int value)
                        {
                            Calls.Add("validate");
                            return value > 0;
                        }
                        protected void BeforeValueChanges(int value) => Calls.Add("before");
                        protected void AfterValueChanges(int previousValue) => Calls.Add("after");
                    }

                    public partial class DerivedObservable : ObservableBase
                    {
                        private new event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private int _value = 1;

                        public void SubscribeToHiddenEvent(Action<string> handler) => PropertyChanged += handler;
                    }

                    public static class RuntimeProbe
                    {
                        public static string Run()
                        {
                            var target = new DerivedObservable();
                            var propertyName = string.Empty;
                            var hiddenEventRaised = false;
                            ((Nexus.Core.IObservable)target).PropertyChanged += name => propertyName = name;
                            target.SubscribeToHiddenEvent(_ => hiddenEventRaised = true);
                            target.SetValue(2);
                            return target.Value + "|" + propertyName + "|" + string.Join(",", target.Calls)
                                + "|" + hiddenEventRaised;
                        }
                    }
                }
                """;

        var result = RunGenerator(source);
        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.DoesNotContain(
            result.GeneratedSources.Select(generated => generated.SourceText.ToString()),
            generated =>
                generated.Contains("event global::System.Action<string>", StringComparison.Ordinal)
        );

        using var assemblyStream = new MemoryStream();
        var emitResult = result.Compilation.Emit(assemblyStream);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
        assemblyStream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
        var runMethod = assembly.GetType("Probe.RuntimeProbe")!.GetMethod("Run")!;

        Assert.Equal("2|Value|validate,before,after|False", runMethod.Invoke(null, null));
    }

    /// <summary>
    /// Verifies unsupported language versions receive a clear diagnostic.
    /// </summary>
    [Fact]
    public void UnsupportedLanguageVersionProducesDiagnostic()
    {
        const string source = """
            using System;
            namespace Nexus.Core
            {
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class ObservableAttribute : Attribute { }
            }
            namespace Probe
            {
                public partial class Target
                {
                    [Nexus.Core.Observable]
                    private int _value;
                }
            }
            """;

        var result = RunGenerator(source, LanguageVersion.CSharp7_3);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "NXSOBS008");
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// Verifies generated code compiles at the supported C# 8 language-version floor.
    /// </summary>
    [Fact]
    public void GeneratedCodeCompilesWithCSharp8()
    {
        const string source = """
            using System;
            namespace Nexus.Core
            {
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class ObservableAttribute : Attribute { }
            }
            namespace Probe
            {
                public partial class Target
                {
                    [Nexus.Core.Observable]
                    private int _value;
                }
            }
            """;

        var result = RunGenerator(source, LanguageVersion.CSharp8);

        Assert.DoesNotContain(
            result.Diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
    }

    /// <summary>
    /// Verifies generated events are initialized when nullable warnings are enabled without annotations.
    /// </summary>
    [Fact]
    public void GeneratedEventsRespectNullableWarningContext()
    {
        const string source = """
            using System;
            namespace Nexus.Core
            {
                [AttributeUsage(AttributeTargets.Field)]
                public sealed class ObservableAttribute : Attribute { }
            }
            namespace Probe
            {
                public partial class Target
                {
                    [Nexus.Core.Observable]
                    private object _value = new();
                }
            }
            """;

        var result = RunGenerator(source, nullableOptions: NullableContextOptions.Warnings);
        var diagnostics = result.Compilation.GetDiagnostics();

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CS8618");
        Assert.Contains(
            result.GeneratedSources,
            generated =>
                generated
                    .SourceText.ToString()
                    .Contains("ValueChanged = null!", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Verifies nullable field annotations are preserved in generated properties.
    /// </summary>
    [Fact]
    public void NullableFieldAnnotationsArePreservedInEnabledContext()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class Target
                    {
                        [Nexus.Core.Observable]
                        private string? _value;
                    }
                }
                """;

        var inputCompilation = CreateCompilation(
            source,
            LanguageVersion.CSharp12,
            NullableContextOptions.Enable
        );
        var inputField = inputCompilation
            .GetTypeByMetadataName("Probe.Target")!
            .GetMembers("_value")
            .OfType<IFieldSymbol>()
            .Single();
        Assert.Equal(NullableAnnotation.Annotated, inputField.NullableAnnotation);

        var result = RunGenerator(source, nullableOptions: NullableContextOptions.Enable);
        var diagnostics = result.Compilation.GetDiagnostics();
        var generated = Assert.Single(result.GeneratedSources).SourceText.ToString();
        var propertyLine = generated
            .Split('\n')
            .Single(line => line.Contains(" Value => _value;", StringComparison.Ordinal))
            .Trim();
        var property = result
            .Compilation.GetTypeByMetadataName("Probe.Target")!
            .GetMembers("Value")
            .OfType<IPropertySymbol>()
            .Single();

        Assert.DoesNotContain(
            diagnostics,
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.Equal("public string? Value => _value;", propertyLine);
        Assert.Equal(NullableAnnotation.Annotated, property.NullableAnnotation);
    }

    /// <summary>
    /// Verifies the analyzer reports an invalid existing IObservable event signature.
    /// </summary>
    [Fact]
    public async Task AnalyzerReportsInvalidPropertyChangedEvent()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public class InvalidObservable : Nexus.Core.IObservable
                    {
                        public event Action<int>? PropertyChanged;
                    }
                }
                """;
        var compilation = CreateCompilation(source, LanguageVersion.CSharp12);
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ObservableAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NXSOBS009");
    }

    /// <summary>
    /// Verifies the analyzer checks a visible event even when the interface maps to a base event.
    /// </summary>
    [Fact]
    public async Task AnalyzerReportsInvalidHiddenPropertyChangedEvent()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public class ObservableBase : Nexus.Core.IObservable
                    {
                        public event Action<string>? PropertyChanged;
                    }

                    public class InvalidDerivedObservable : ObservableBase
                    {
                        public new event Action<int>? PropertyChanged;
                    }
                }
                """;
        var compilation = CreateCompilation(source, LanguageVersion.CSharp12);
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ObservableAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Contains(
            diagnostics,
            diagnostic =>
                diagnostic.Id == "NXSOBS009"
                && diagnostic.GetMessage().Contains("Action<int>", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Runs the incremental generator and returns the updated compilation and generated output.
    /// </summary>
    /// <param name="source">The source code to compile.</param>
    /// <param name="languageVersion">The language version used to parse the source.</param>
    /// <returns>The updated compilation, generator diagnostics, and generated source files.</returns>
    private static (
        CSharpCompilation Compilation,
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources
    ) RunGenerator(
        string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp12,
        NullableContextOptions? nullableOptions = null
    )
    {
        var compilation = CreateCompilation(source, languageVersion, nullableOptions);
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ObservableGenerator().AsSourceGenerator()],
            parseOptions: parseOptions
        );
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var updatedCompilation,
            out var diagnostics
        );
        var generatedSources = driver
            .GetRunResult()
            .Results.SelectMany(result => result.GeneratedSources)
            .ToImmutableArray();

        return ((CSharpCompilation)updatedCompilation, diagnostics, generatedSources);
    }

    /// <summary>
    /// Creates a compilation with platform references and nullable analysis enabled when supported.
    /// </summary>
    /// <param name="source">The source code to compile.</param>
    /// <param name="languageVersion">The language version used for parsing.</param>
    /// <returns>A C# compilation suitable for generator and analyzer testing.</returns>
    private static CSharpCompilation CreateCompilation(
        string source,
        LanguageVersion languageVersion,
        NullableContextOptions? nullableOptions = null
    )
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var compilationNullableOptions =
            nullableOptions
            ?? (
                languageVersion >= LanguageVersion.CSharp8
                    ? NullableContextOptions.Enable
                    : NullableContextOptions.Disable
            );

        return CSharpCompilation.Create(
            "ObservableGeneratorTests_" + Guid.NewGuid().ToString("N"),
            [syntaxTree],
            PlatformReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: compilationNullableOptions
            )
        );
    }
}
