// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Blazor.SourceGenerators.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Blazor.SourceGenerators
{
    [Generator]
    public class JavaScriptInteropGenerator : ISourceGenerator
    {
        private readonly LibDomParser _libDomParser = new();

        private static readonly DiagnosticDescriptor s_typeNameRequiredDiagnostic = new(
            "BR0001",
            "The TypeName is required to source generator JavaScript interop",
            "JSAutoInteropAttribute must provide a 'TypeName', as it is required to source generate JavaScript interop extensions",
            "Blazorators.JSAutoInteropAttribute",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor s_pathFromWindowRequiredDiagnostic = new(
            "BR0002",
            "The PathFromWindow is required to source generator JavaScript interop",
            "JSAutoInteropAttribute must provide a 'PathFromWindow', as it is required to source generate JavaScript interop extensions",
            "Blazorators.JSAutoInteropAttribute",
            DiagnosticSeverity.Error,
            true);

        private const string JSAutoInteropAttributeFullName = "JSAutoInteropAttribute";
        private const string JSAutoInteropAttributeSource = @"using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class JSAutoInteropAttribute : Attribute
{
    /// <summary>
    /// The type name that corresponds to the lib.dom.d.ts interface. For example, <c>""Geolocation""</c>.
    /// </summary>
    public string TypeName { get; set; } = null!;

    /// <summary>
    /// The path from the <c>window</c> object. For example,
    /// <c>""window.navigator.geolocation""</c> (or <c>""navigator.geolocation""</c>).
    /// </summary>
    public string PathFromWindow { get; set; } = null!;

    /// <summary>
    /// Whether to generate only pure JavaScript functions that do not require callbacks.
    /// For example, <c>Geolocation.clearWatch</c> is consider pure, but <c>Geolocation.watchPosition</c> is not.
    /// </summary>
    public bool OnlyGeneratePureJS { get; set; }

    /// <summary>
    /// The optional URL to the corresponding API.
    /// </summary>
    public string? Url { get; set; }
}
";

        private const string RecordCompatSource = @"using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}";

        public void Initialize(GeneratorInitializationContext context)
        {
#if DEBUG
            //if (!Debugger.IsAttached) Debugger.Launch();
#endif

            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(SyntaxContextReceiver.Create);
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Add source from text.
            context.AddSource("JSAutoInteropAttribute",
                SourceText.From(JSAutoInteropAttributeSource, Encoding.UTF8));
            context.AddSource("RecordCompat",
                SourceText.From(RecordCompatSource, Encoding.UTF8));

            if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver)
            {
                return;
            }

            foreach (var (options, classDeclaration, attribute) in receiver.ClassDeclarations)
            {
                if (options.TypeName is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            s_typeNameRequiredDiagnostic, attribute.GetLocation()));

                    continue;
                }

                if (options.PathFromWindow is null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            s_pathFromWindowRequiredDiagnostic, attribute.GetLocation()));

                    continue;
                }

                var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
                if (!isPartial)
                {
                    continue;
                }

                var model = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(classDeclaration);
                if (symbol is not ITypeSymbol typeSymbol || !typeSymbol.IsStatic)
                {
                    continue;
                }

                var result = _libDomParser.ParseStaticType(options.TypeName!);
                if (result.Status == ParserResultStatus.SuccessfullyParsed &&
                    result.Value is not null)
                {
                    var staticObject = result.Value;
                    if (staticObject.DependentTypes?.Any() ?? false)
                    {
                        foreach (var dependentObj in staticObject.DependentTypes.Where(t => !t.Value.IsActionParameter))
                        {
                            context.AddSource($"{dependentObj.Key}.generated.cs",
                                SourceText.From(dependentObj.Value.ToString(), Encoding.UTF8));
                        }
                    }

                    // TODO:
                    // Output JavaScript also

                    context.AddSource($"{typeSymbol.Name}.generated.cs",
                        SourceText.From(staticObject.ToStaticPartialClassString(), Encoding.UTF8));
                }
            }
        }

        private sealed class SyntaxContextReceiver : ISyntaxContextReceiver
        {
            internal static ISyntaxContextReceiver Create() => new SyntaxContextReceiver();

            public HashSet<(GeneratorOptions Options, ClassDeclarationSyntax ClassDeclaration, AttributeSyntax JSAutoInteropAttribute)> ClassDeclarations { get; } = new();

            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                if (context.Node is ClassDeclarationSyntax classDeclaration &&
                    classDeclaration.AttributeLists.Count > 0)
                {
                    foreach (var attributeListSyntax in classDeclaration.AttributeLists)
                    {
                        foreach (var attributeSyntax in attributeListSyntax.Attributes)
                        {
                            var name = attributeSyntax.Name.ToString();
                            if (JSAutoInteropAttributeFullName.Contains(name))
                            {
                                var options = GetGeneratorOptions(attributeSyntax);
                                ClassDeclarations.Add((options, classDeclaration, attributeSyntax));
                            }
                        }
                    }
                }
            }

            private static GeneratorOptions GetGeneratorOptions(AttributeSyntax attribute)
            {
                GeneratorOptions options = new();
                if (attribute is { ArgumentList: not null })
                {
                    static string RemoveQuotes(string value) => value.Replace("\"", "");

                    foreach (var arg in attribute.ArgumentList.Arguments)
                    {
                        var propName = arg.NameEquals?.Name?.ToString();
                        options = propName switch
                        {
                            nameof(options.TypeName) => options with
                            {
                                TypeName = RemoveQuotes(arg.Expression.ToString())
                            },
                            nameof(options.PathFromWindow) => options with
                            {
                                PathFromWindow = RemoveQuotes(arg.Expression.ToString())
                            },
                            nameof(options.OnlyGeneratePureJS) => options with
                            {
                                OnlyGeneratePureJS = bool.Parse(arg.Expression.ToString())
                            },
                            nameof(options.Url) => options with
                            {
                                Url = RemoveQuotes(arg.Expression.ToString())
                            },

                            _ => options
                        };
                    }
                }

                return options;
            }
        }
    }
}