using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using LightReactive.Avalonia.Generator;

namespace LightReactive.Avalonia.Tests;

/// <summary>
/// Tests for the [Reactive] generator: the code it emits for partial properties
/// and that this code compiles together with the <see cref="ReactiveObject"/> base.
/// </summary>
public class ReactiveGeneratorTests
{
    #region Helpers

    private static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    /// <summary>Platform and test assemblies (including LightReactive.Avalonia) used to compile the samples.</summary>
    private static readonly ImmutableArray<MetadataReference> References =
        (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

    private const string AttributeHintName = "ReactiveAttribute.g.cs";

    private sealed record GeneratorRun(Compilation Output, GeneratorRunResult Result)
    {
        /// <summary>Compilation errors of the sample together with the generated code.</summary>
        public IReadOnlyList<Diagnostic> Errors =>
            Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        /// <summary>Text of the file containing the [Reactive] attribute.</summary>
        public string AttributeSource =>
            Result.GeneratedSources.Single(s => s.HintName == AttributeHintName).SourceText.ToString();

        /// <summary>Texts of the generated property files, excluding the [Reactive] attribute and EmbeddedAttribute.</summary>
        public IReadOnlyList<string> PropertySources =>
            Result.GeneratedSources
                .Where(s => s.HintName != AttributeHintName && !s.HintName.Contains("EmbeddedAttribute"))
                .Select(s => s.SourceText.ToString())
                .ToList();
    }

    private static GeneratorRun Run(string source, string assemblyName = "ReactiveGeneratorSample", MetadataReference? extraReference = null)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            extraReference is null ? References : References.Add(extraReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ReactiveGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);
        return new GeneratorRun(output, driver.GetRunResult().Results.Single());
    }

    private static string Describe(IEnumerable<Diagnostic> diagnostics)
        => string.Join(Environment.NewLine, diagnostics.Select(d => d.ToString()));

    #endregion

    #region Attribute

    [Fact]
    public void Generate_AnyCompilation_AddsInternalAttributeInLightReactiveAvaloniaNamespace()
    {
        GeneratorRun run = Run("namespace Sample { public class Empty { } }");

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        Assert.Empty(run.PropertySources);
        string text = run.AttributeSource;
        Assert.Contains("namespace LightReactive.Avalonia", text);
        Assert.Contains("[global::Microsoft.CodeAnalysis.EmbeddedAttribute]", text);
        Assert.Contains("internal sealed class ReactiveAttribute", text);
    }

    [Fact]
    public void Generate_InternalsVisibleToConsumer_NoAttributeConflict()
    {
        // The assembly sees the internal types of a neighbor, and both reference the generator.
        // Without EmbeddedAttribute the neighbor would see two [Reactive] attributes (CS0436).
        GeneratorRun library = Run("""
            using LightReactive.Avalonia;
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ConsumerSample")]
            namespace Library;
            public partial class LibraryVm : ReactiveObject
            {
                [Reactive] public partial int Value { get; set; }
            }
            """, "LibrarySample");
        Assert.True(library.Errors.Count == 0, Describe(library.Errors));

        using MemoryStream image = new();
        Assert.True(library.Output.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success);

        GeneratorRun consumer = Run("""
            using LightReactive.Avalonia;
            namespace Consumer;
            public partial class ConsumerVm : ReactiveObject
            {
                [Reactive] public partial string? Name { get; set; }
            }
            """, "ConsumerSample", MetadataReference.CreateFromImage(image.ToArray()));

        Assert.True(consumer.Errors.Count == 0, Describe(consumer.Errors));
        Assert.DoesNotContain(consumer.Output.GetDiagnostics(TestContext.Current.CancellationToken), d => d.Id == "CS0436");
    }

    #endregion

    #region Properties

    [Fact]
    public void Generate_PartialProperty_CreatesFieldBackedImplementation()
    {
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace Sample;
            public partial class Vm : ReactiveObject
            {
                [Reactive] public partial string Name { get; set; } = string.Empty;
            }
            """);

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        string text = Assert.Single(run.PropertySources);
        Assert.Contains("public partial string Name", text);
        Assert.Contains("get => field;", text);
        Assert.Contains("set => RaiseAndSetIfChanged(ref field, value);", text);
    }

    [Fact]
    public void Generate_PrivateSetter_KeepsAccessorModifier()
    {
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace Sample;
            public partial class Vm : ReactiveObject
            {
                [Reactive] public partial int Count { get; private set; }
            }
            """);

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        string text = Assert.Single(run.PropertySources);
        Assert.Contains("private set => RaiseAndSetIfChanged(ref field, value);", text);
    }

    [Fact]
    public void Generate_InitAccessor_UsesInitKeyword()
    {
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace Sample;
            public partial class Vm : ReactiveObject
            {
                [Reactive] public partial int Id { get; init; }
            }
            """);

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        string text = Assert.Single(run.PropertySources);
        Assert.Contains("init => RaiseAndSetIfChanged(ref field, value);", text);
    }

    [Fact]
    public void Generate_NullableReference_KeepsAnnotation()
    {
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace Sample;
            public partial class Vm : ReactiveObject
            {
                [Reactive] public partial string? Note { get; set; }
            }
            """);

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        string text = Assert.Single(run.PropertySources);
        Assert.Contains("string? Note", text);
    }

    [Fact]
    public void Generate_NestedGenericType_RepeatsContainingTypes()
    {
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace Sample;
            public partial class Outer<T>
            {
                public partial class Inner : ReactiveObject
                {
                    [Reactive] public partial T? Value { get; set; }
                }
            }
            """);

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        string text = Assert.Single(run.PropertySources);
        Assert.Contains("partial class Outer<T>", text);
        Assert.Contains("partial class Inner", text);
    }

    [Fact]
    public void Generate_GlobalNamespace_CompilesWithoutNamespaceBlock()
    {
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            public partial class GlobalVm : ReactiveObject
            {
                [Reactive] public partial bool IsBusy { get; set; }
            }
            """);

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        string text = Assert.Single(run.PropertySources);
        Assert.DoesNotContain("namespace", text);
    }

    [Fact]
    public void Generate_NoAccessModifier_RepeatsDeclarationModifiers()
    {
        // The compiler treats default accessibility and explicit private as different modifiers (CS8799).
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace Sample;
            public partial class Vm : ReactiveObject
            {
                [Reactive] partial int Count { get; set; }
            }
            """);

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        string text = Assert.Single(run.PropertySources);
        Assert.DoesNotContain("private", text);
    }

    [Fact]
    public void Generate_RequiredAndNewModifiers_KeptInImplementation()
    {
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace Sample;
            public class Base : ReactiveObject
            {
                public string? Title { get; set; }
            }
            public partial class Vm : Base
            {
                [Reactive] public required partial string Name { get; init; }
                [Reactive] public new partial string? Title { get; set; }
            }
            """);

        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        Assert.Contains(run.PropertySources, text => text.Contains("public required partial string Name"));
        Assert.Contains(run.PropertySources, text => text.Contains("public new partial string? Title"));
    }

    [Fact]
    public void Generate_PathsDifferingInCaseOrSeparators_CreatesAllFiles()
    {
        // Roslyn compares file names case-insensitively, and A.B and A_B yield the same readable name part.
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace A.B
            {
                public partial class X : ReactiveObject
                {
                    [Reactive] public partial int Id { get; set; }
                    [Reactive] public partial int ID { get; set; }
                }
            }
            namespace A_B
            {
                public partial class X : ReactiveObject
                {
                    [Reactive] public partial int Id { get; set; }
                }
            }
            """);

        Assert.Null(run.Result.Exception);
        Assert.True(run.Errors.Count == 0, Describe(run.Errors));
        Assert.Equal(3, run.PropertySources.Count);
    }

    [Fact]
    public void Generate_StaticProperty_Skipped()
    {
        GeneratorRun run = Run("""
            using LightReactive.Avalonia;
            namespace Sample;
            public partial class Vm : ReactiveObject
            {
                [Reactive] public static partial int Total { get; set; }
            }
            """);

        // RaiseAndSetIfChanged is an instance method: no code is generated for a static property.
        Assert.Empty(run.PropertySources);
    }

    #endregion
}
