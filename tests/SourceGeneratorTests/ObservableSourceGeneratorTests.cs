using System.Collections.Immutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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
                public bool GenerateChangedEvent { get; set; } = true;
            }

            public interface IObservable
            {
                event Action<string>? PropertyChanged;
            }
        }
        """;

    /// <summary>
    /// Verifies a generated notification bridge can be consumed by an observable derived type.
    /// </summary>
    [Fact]
    public void GeneratedNotificationBridgeSupportsDerivedTypes()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class BaseTarget : IObservable
                    {
                        [Nexus.Core.Observable]
                        private int _baseValue;
                        public event Action<string>? PropertyChanged;
                    }

                    public partial class DerivedTarget : BaseTarget
                    {
                        [Nexus.Core.Observable]
                        private int _derivedValue;
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
        Assert.Contains(
            result.GeneratedSources,
            generated =>
                generated
                    .SourceText.ToString()
                    .Contains(
                        "protected void NotifyPropertyChanged(string propertyName)",
                        StringComparison.Ordinal
                    )
        );
    }

    /// <summary>
    /// Verifies an event-owning base receives a notification bridge without observable fields.
    /// </summary>
    [Fact]
    public void EventOwnerWithoutObservableFieldsGetsNotificationBridge()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class EventOwner : IObservable
                    {
                        public event Action<string>? PropertyChanged;
                    }

                    public partial class StandaloneEventOwner : IObservable
                    {
                        public event Action<string>? PropertyChanged;
                    }

                    public partial class DerivedTarget : EventOwner
                    {
                        [Nexus.Core.Observable]
                        private int _value;
                    }
                }
                """;

        var result = RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "NXS007");
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        var baseGenerated = Assert
            .Single(
                result.GeneratedSources,
                generated =>
                    generated
                        .SourceText.ToString()
                        .Contains("partial class EventOwner", StringComparison.Ordinal)
            )
            .SourceText.ToString();
        Assert.Contains(
            "protected void NotifyPropertyChanged(string propertyName)",
            baseGenerated,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("public int Value", baseGenerated, StringComparison.Ordinal);
        var standaloneGenerated = Assert
            .Single(
                result.GeneratedSources,
                generated =>
                    generated
                        .SourceText.ToString()
                        .Contains("partial class StandaloneEventOwner", StringComparison.Ordinal)
            )
            .SourceText.ToString();
        Assert.Contains(
            "protected void NotifyPropertyChanged(string propertyName)",
            standaloneGenerated,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// Verifies generic base definitions are recognized as owning generated notification bridges.
    /// </summary>
    [Fact]
    public void GenericBaseNotificationBridgeIsInheritedByConstructedDerivedType()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class GenericBase<T> : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private T _baseValue = default!;
                    }

                    public partial class DerivedTarget : GenericBase<int>
                    {
                        [Nexus.Core.Observable]
                        private int _derivedValue;
                    }
                }
                """;

        var result = RunGenerator(source);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "NXS007");
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.Equal(2, result.GeneratedSources.Length);
    }

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

                        protected virtual partial void AfterCountChanges(int previousValue)
                        {
                            Calls.Add($"after:{previousValue}");
                            if (_reentrantValue is not int value)
                                return;
                            _reentrantValue = null;
                            SetCount(value);
                        }

                        public void ScheduleReentrantChange(int value) => _reentrantValue = value;
                    }

                    public partial class PlainTarget : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private string _displayName = "initial";

                        [Nexus.Core.Observable("ExplicitName")]
                        private int _value;

                        [Nexus.Core.Observable]
                        private int __cached;
                    }

                    public partial class GeneratedNotificationTarget : IObservable
                    {
                        public event Action<string>? PropertyChanged;

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
                            target.PropertyChanged += name =>
                            {
                                propertyNotifications.Add(name);
                                target.Calls.Add("notify:" + name);
                            };
                            target.CountChanged += (previous, current) =>
                                target.Calls.Add($"changed:{previous}:{current}");
                            target.CountChanged += (previous, current) => valueChanges.Add($"{previous}:{current}");
                            target.Count = 3;
                            target.Count = -1;
                            target.ScheduleReentrantChange(9);
                            target.Count = 5;

                            var plain = new PlainTarget();
                            var plainChanges = new List<string>();
                            plain.DisplayNameChanged += (previous, current) => plainChanges.Add($"{previous}:{current}");
                            plain.DisplayName = "updated";

                            var generatedNotification = new GeneratedNotificationTarget();
                            var generatedPropertyName = string.Empty;
                            generatedNotification.PropertyChanged += name => generatedPropertyName = name;
                            generatedNotification.Amount = 4;

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
            "after:3,notify:Count,changed:3:-1,after:-1,notify:Count,changed:-1:5"
                + "|Count,Count|3:-1,-1:5|9|updated|initial:updated|False|Amount|4",
            output
        );

        var observableSource = Assert
            .Single(
                result.GeneratedSources,
                generated =>
                    generated
                        .SourceText.ToString()
                        .Contains("class ObservableTarget", StringComparison.Ordinal)
            )
            .SourceText.ToString();
        Assert.Contains(
            "private void __SetCount(int value)",
            observableSource,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "protected virtual void SetCount(int value)",
            observableSource,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "event global::System.Action<string> PropertyChanged",
            observableSource,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// Verifies an observable hook can omit the previous value parameter.
    /// </summary>
    [Fact]
    public void ParameterlessObservableHookIsInvoked()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class Target : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private int _value;
                        public int HookCalls { get; private set; }

                        protected virtual partial void AfterValueChanges() => HookCalls++;
                    }

                    public static class RuntimeProbe
                    {
                        public static int Run()
                        {
                            var target = new Target();
                            target.Value = 1;
                            return target.HookCalls;
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
        Assert.Contains(
            result.GeneratedSources,
            generated =>
                generated
                    .SourceText.ToString()
                    .Contains("AfterValueChanges();", StringComparison.Ordinal)
        );

        using var assemblyStream = new MemoryStream();
        var emitResult = result.Compilation.Emit(assemblyStream);
        Assert.True(emitResult.Success, string.Join(Environment.NewLine, emitResult.Diagnostics));
        assemblyStream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
        var runMethod = assembly.GetType("Probe.RuntimeProbe")!.GetMethod("Run")!;

        Assert.Equal(1, (int)runMethod.Invoke(null, null)!);
    }

    /// <summary>
    /// Verifies generated no-op hooks can be overridden by a derived class.
    /// </summary>
    [Fact]
    public void GeneratedHooksCanBeOverridden()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class BaseTarget : IObservable
                    {
                        [Nexus.Core.Observable]
                        private int _value;
                        public event Action<string>? PropertyChanged;
                    }

                    public sealed class DerivedTarget : BaseTarget
                    {
                        public int ParameterlessCalls { get; private set; }
                        public int PreviousValue { get; private set; }

                        protected override void AfterValueChanges() => ParameterlessCalls++;
                        protected override void AfterValueChanges(int previousValue) => PreviousValue = previousValue;
                    }

                    public static class RuntimeProbe
                    {
                        public static string Run()
                        {
                            var target = new DerivedTarget();
                            target.Value = 7;
                            return $"{target.ParameterlessCalls}:{target.PreviousValue}";
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

        Assert.Equal("1:0", (string)runMethod.Invoke(null, null)!);
    }

    /// <summary>
    /// Verifies PublicSetter controls property assignment while generated methods remain protected virtual.
    /// </summary>
    [Fact]
    public void PublicSetterControlsPropertyAssignment()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class Target : IObservable
                    {
                        public event Action<string>? PropertyChanged;

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
            "protected set => __SetInternal(value);",
            generated,
            StringComparison.Ordinal
        );
        Assert.Contains("set => __SetExternal(value);", generated, StringComparison.Ordinal);
        Assert.Contains(
            "protected virtual void SetExternal(int value)",
            generated,
            StringComparison.Ordinal
        );
        Assert.Contains("set => __SetExplicitPublic(value);", generated, StringComparison.Ordinal);
        Assert.Contains(
            "protected virtual void SetExplicitPublic(int value)",
            generated,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
    }

    /// <summary>
    /// Verifies a protected custom setter replaces the generated default assignment method.
    /// </summary>
    [Fact]
    public void ProtectedSetterImplementationReplacesGeneratedSetter()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class Target : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private bool _isBooleanProperty;

                        protected virtual void SetIsBooleanProperty(bool value)
                        {
                            _isBooleanProperty = value;
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
        var generated = Assert.Single(result.GeneratedSources).SourceText.ToString();
        Assert.DoesNotContain(
            "protected virtual void SetIsBooleanProperty(bool value)",
            generated,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// Verifies implemented partial observable hooks receive a defining declaration.
    /// </summary>
    [Fact]
    public void PartialObservableHookDeclarationIsGenerated()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class Target : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private int _value;

                        protected virtual partial void AfterValueChanges(int previousValue) { }
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
        Assert.Contains(
            result.GeneratedSources,
            generated =>
                generated
                    .SourceText.ToString()
                    .Contains(
                        "protected virtual partial void AfterValueChanges(int previousValue);",
                        StringComparison.Ordinal
                    )
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
                    public class NotPartial : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private int _value;
                    }
                }
                """;

        var result = RunGenerator(source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "NXS002");
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
                    public partial class Target : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable("ExplicitName")]
                        private int _value;

                        [Nexus.Core.Observable]
                        private int __cached;
                    }
                }
                """;

        var result = RunGenerator(source);
        var generated = Assert.Single(result.GeneratedSources).SourceText.ToString();

        Assert.Contains("int ExplicitName", generated, StringComparison.Ordinal);
        Assert.Contains("get => _value;", generated, StringComparison.Ordinal);
        Assert.Contains("int _cached", generated, StringComparison.Ordinal);
        Assert.Contains("get => __cached;", generated, StringComparison.Ordinal);
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
            diagnostic => diagnostic.Id.StartsWith("NXS", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Verifies the event-owning base receives a bridge when only its derived type has observable fields.
    /// </summary>
    [Fact]
    public void InheritedNotificationWithoutRaiserGetsGeneratedBridge()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public abstract partial class ObservableBase : Nexus.Core.IObservable
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

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "NXS007");
        Assert.DoesNotContain(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.Contains(
            result.GeneratedSources,
            generated =>
                generated
                    .SourceText.ToString()
                    .Contains("partial class ObservableBase", StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Verifies invalid field, name, duplicate, conflict, and hook declarations are diagnosed.
    /// </summary>
    /// <param name="members">Members included in the partial type.</param>
    /// <param name="expectedDiagnosticId">The diagnostic ID expected for the invalid declaration.</param>
    [Theory]
    [InlineData("[Nexus.Core.Observable] private readonly int _value;", "NXS001")]
    [InlineData("[Nexus.Core.Observable(\"\")] private int _value;", "NXS003")]
    [InlineData(
        "[Nexus.Core.Observable(\"Name\")] private int _first; [Nexus.Core.Observable(\"Name\")] private int _second;",
        "NXS004"
    )]
    [InlineData("public int Value => 0; [Nexus.Core.Observable] private int _value;", "NXS005")]
    [InlineData(
        "public int Value => 0; public virtual void SetValue(int value) { } [Nexus.Core.Observable] private int _value;",
        "NXS012"
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
                    public partial class Target : IObservable
                    {
                        public event Action<string>? PropertyChanged;

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
                    public class Base : IObservable
                    {
                        public event Action<string>? PropertyChanged;
                        protected void NotifyPropertyChanged(string propertyName) =>
                            PropertyChanged?.Invoke(propertyName);
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

        Assert.Equal(2, result.Diagnostics.Count(diagnostic => diagnostic.Id == "NXS005"));
        Assert.Contains(
            result.Diagnostics,
            diagnostic =>
                diagnostic.Id == "NXS012" && diagnostic.Severity == DiagnosticSeverity.Error
        );
        Assert.Empty(result.GeneratedSources);
    }

    /// <summary>
    /// Verifies inherited events remain usable without generating a shadowing event.
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
                        protected void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(propertyName);
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
                            target.Value = 2;
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

        Assert.Equal("2|Value|after|False", runMethod.Invoke(null, null));
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

                public interface IObservable
                {
                    event Action<string> PropertyChanged;
                }
            }
            namespace Probe
            {
                public partial class Target : Nexus.Core.IObservable
                {
                    public event Action<string> PropertyChanged;
                    [Nexus.Core.Observable]
                    private int _value;
                }
            }
            """;

        var result = RunGenerator(source, LanguageVersion.CSharp7_3);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "NXS008");
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

                public interface IObservable
                {
                    event Action<string> PropertyChanged;
                }
            }
            namespace Probe
            {
                public partial class Target : Nexus.Core.IObservable
                {
                    public event Action<string> PropertyChanged;
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

                public interface IObservable
                {
                    event Action<string> PropertyChanged;
                }
            }
            namespace Probe
            {
                public partial class Target : Nexus.Core.IObservable
                {
                    public event Action<string> PropertyChanged;
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
                    public partial class Target : IObservable
                    {
                        public event Action<string>? PropertyChanged;

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
            .Single(line => line.Contains("public string? Value", StringComparison.Ordinal))
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
        Assert.Equal("public string? Value", propertyLine);
        Assert.Contains("get => _value;", generated, StringComparison.Ordinal);
        Assert.Equal(NullableAnnotation.Annotated, property.NullableAnnotation);
    }

    /// <summary>
    /// Verifies generation is limited to observable classes and never supplies a missing event.
    /// </summary>
    [Fact]
    public void GenerationRequiresObservableContractAndAuthoredEvent()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public partial class Unrelated
                    {
                        [Nexus.Core.Observable]
                        private int _value;
                    }

                    public partial class MissingEvent : IObservable
                    {
                        [Nexus.Core.Observable]
                        private int _value;
                    }

                    public partial class Valid : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private int _value;
                    }
                }
                """;

        var result = RunGenerator(source);

        var generated = Assert.Single(result.GeneratedSources).SourceText.ToString();
        Assert.Contains("partial class Valid", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("partial class Unrelated", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("partial class MissingEvent", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "event global::System.Action<string> PropertyChanged",
            generated,
            StringComparison.Ordinal
        );
        Assert.Contains(
            result.Compilation.GetDiagnostics(),
            diagnostic => diagnostic.Id == "CS0535"
        );
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

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NXS009");
    }

    /// <summary>
    /// Verifies direct backing-field reads and writes produce analyzer warnings.
    /// </summary>
    [Fact]
    public async Task AnalyzerWarnsAboutDirectObservableBackingFieldAccess()
    {
        var source =
            ObservableContract
            + """
                namespace Probe
                {
                    public class Unrelated
                    {
                        [Nexus.Core.Observable]
                        private int _ignored;

                        public int Read() => _ignored;
                    }

                    public class Target : IObservable
                    {
                        public event Action<string>? PropertyChanged;

                        [Nexus.Core.Observable]
                        private int _value;

                        public int Read() => _value;
                        public void Write(int value) => _value = value;
                    }
                }
                """;
        var compilation = CreateCompilation(source, LanguageVersion.CSharp12);
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ObservableAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        var backingFieldWarnings = diagnostics.Where(diagnostic => diagnostic.Id == "NXS013");
        Assert.Equal(2, backingFieldWarnings.Count());
        Assert.All(
            backingFieldWarnings,
            diagnostic => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity)
        );
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
                diagnostic.Id == "NXS009"
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
