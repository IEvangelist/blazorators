﻿// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Options;

namespace Blazor.SourceGenerators.Builders;

/// <summary>
/// Represents a builder for generating C# source code.
/// </summary>
[DebuggerDisplay("{ToSourceCodeString()}", Name = "{_options.TypeName}")]
internal sealed class SourceBuilder
{
    internal const char NewLine = '\n';
    private const string _twoNewLines = "\n\n";

    private readonly StringBuilder _builder = new();
    private readonly GeneratorOptions _options;
    private readonly bool _isService;

    private Indentation _indentation = new(0);
    private string? _implementationName;
    private string? _interfaceName;

    /// <summary>
    /// Gets or sets the set of fields used by the source builder.
    /// </summary>
    internal ISet<string>? Fields { get; private set; }

    /// <summary>
    /// Gets or sets the set of methods used by the source builder.
    /// </summary>
    internal ISet<string>? Methods { get; private set; }

    /// <summary>
    /// Gets the current indentation level of the source builder.
    /// </summary>
    internal int IndentationLevel => _indentation.Level;

    /// <summary>
    /// Gets the implementation name, which is lazily initialized the first time it is accessed.
    /// </summary>
    internal string ImplementationName => _implementationName ??=
        _options.Implementation.ToImplementationName(_isService);

    /// <summary>
    /// Gets the interface name, which is lazily initialized the first time it is accessed.
    /// </summary>
    internal string InterfaceName => _interfaceName ??=
        _options.Implementation.ToInterfaceName(_isService);

    internal SourceBuilder(GeneratorOptions options, bool isService = true)
    {
        _options = options;
        _isService = isService;
    }

    /// <summary>
    /// Appends the copy right header to the source builder.
    /// </summary>
    /// <returns>The updated source builder.</returns>
    internal SourceBuilder AppendCopyRightHeader()
    {
        _builder.Append("""
            // Copyright (c) David Pine. All rights reserved.
            // Licensed under the MIT License:
            // https://bit.ly/blazorators-license
            // Auto-generated by blazorators.


            """);

        return this;
    }

    internal SourceBuilder AppendUsingDeclarations()
    {
        if (_options is { SupportsGenerics: true })
        {
            _builder.Append($"using Blazor.Serialization.Extensions;{NewLine}");
            _builder.Append($"using System.Text.Json;{NewLine}");
        }

        if (!_options.IsWebAssembly)
        {
            _builder.Append($"using System.Threading.Tasks;{NewLine}");
        }

        _builder.Append(NewLine);

        return this;
    }

    internal SourceBuilder AppendNamespace(string namespaceString, bool isNullableContext = true)
    {
        if (isNullableContext)
        {
            _builder.Append($"#nullable enable{NewLine}");
        }

        _builder.Append($"namespace {namespaceString};{_twoNewLines}");

        return this;
    }

    internal SourceBuilder AppendPublicInterfaceDeclaration()
    {
        _builder.Append($"/// <summary>{NewLine}");
        _builder.Append($"/// Source generated interface definition of the <c>{_options.TypeName}</c> type.{NewLine}");
        _builder.Append($"/// </summary>{NewLine}");
        _builder.Append($"public partial interface {InterfaceName}{NewLine}");

        return this;
    }

    internal SourceBuilder AppendInternalImplementationDeclaration()
    {
        _builder.Append($"/// <inheritdoc />{NewLine}");
        _builder.Append($"internal sealed class {ImplementationName} : {InterfaceName}{NewLine}");

        return this;
    }

    internal SourceBuilder AppendImplementationCtor()
    {
        var javaScriptRuntime = _options.IsWebAssembly
            ? "IJSInProcessRuntime"
            : "IJSRuntime";

        _builder.Append($"{_indentation}internal readonly {javaScriptRuntime} _javaScript = null!;{_twoNewLines}");
        _builder.Append($"{_indentation}public {ImplementationName}({javaScriptRuntime} javaScript) =>{NewLine}");

        IncreaseIndentation();

        _builder.Append($"{_indentation}_javaScript = javaScript;{_twoNewLines}");

        DecreaseIndentation();

        return this;
    }

    internal SourceBuilder AppendOpeningCurlyBrace(bool increaseIndentation = false)
    {
        IncreaseIndentationImpl(increaseIndentation);

        _builder.Append($"{_indentation}{{{NewLine}");

        return this;
    }

    internal SourceBuilder AppendClosingCurlyBrace(bool decreaseIndentation = false)
    {
        DecreaseIndentationImpl(decreaseIndentation);

        _builder.Append($"{_indentation}}}{NewLine}");

        return this;
    }

    internal SourceBuilder AppendTripleSlashMethodComments(
        CSharpMethod method,
        bool extrapolateParameters = false,
        IndentationAdjustment adjustment = IndentationAdjustment.NoOp)
    {
        AdjustIndentation(adjustment);
        var indent = _indentation.ToString();

        _builder.Append($"{indent}/// <summary>{NewLine}");

        var jsMethodName = method.RawName.LowerCaseFirstLetter();
        var func = $"{_options.Implementation}.{jsMethodName}";

        _builder.Append($"{indent}/// Source generated implementation of <c>{func}</c>.{NewLine}");
        var fullUrl = $"https://developer.mozilla.org/docs/Web/API/{_options.TypeName}/{jsMethodName}";
        _builder.Append($"{indent}/// <a href=\"{fullUrl}\"></a>{NewLine}");
        _builder.Append($"{indent}/// </summary>{NewLine}");

        if (extrapolateParameters)
        {
            foreach (var (index, param) in method.ParameterDefinitions.Select())
            {
                if (index.IsFirst)
                {
                    _builder.Append(
                        $"/// <param name=\"component\">The calling Razor (or Blazor) component.</param>{NewLine}");
                }

                if (param.ActionDeclation is not null)
                {
                    var name = param.ToArgumentString();
                    var dependentTypes = param.ActionDeclation.DependentTypes.Keys;
                    var action =
                        $"Expects the name of a <c>\"JSInvokableAttribute\"</c> C# method with the following " +
                        $"<c>System.Action{{{string.Join(", ", dependentTypes)}}}\"</c>.";
                    _builder.Append(
                        $"/// <param name=\"{name}\">{action}</param>{NewLine}");
                }
                else
                {
                    _builder.Append(
                        $"/// <param name=\"{param.RawName}\">The <c>{param.RawTypeName}</c> value.</param>{NewLine}");
                }
            }
        }

        return this;
    }

    internal SourceBuilder AppendEmptyTripleSlashInheritdocComments(
        IndentationAdjustment adjustment = IndentationAdjustment.NoOp)
    {
        AdjustIndentation(adjustment);
        var indent = _indentation.ToString();

        _builder.Append($"{indent}/// <inheritdoc />{NewLine}");

        return this;
    }

    internal SourceBuilder AppendTripleSlashInheritdocComments(
        string csharpTypeName,
        string memberName,
        IndentationAdjustment adjustment = IndentationAdjustment.NoOp)
    {
        AdjustIndentation(adjustment);
        var indent = _indentation.ToString();

        _builder.Append($"{indent}/// <inheritdoc cref=\"{csharpTypeName}.{memberName}\" />{NewLine}");

        return this;
    }

    internal SourceBuilder AppendTripleSlashPropertyComments(
        CSharpProperty property,
        IndentationAdjustment adjustment = IndentationAdjustment.NoOp)
    {
        AdjustIndentation(adjustment);
        var indent = _indentation.ToString();

        _builder.Append($"{indent}/// <summary>{NewLine}");

        var jsMethodName = property.RawName.LowerCaseFirstLetter();
        var func = $"{_options.Implementation}.{jsMethodName}";

        _builder.Append($"{indent}/// Source generated implementation of <c>{func}</c>.\r\n");
        var fullUrl = $"https://developer.mozilla.org/docs/Web/API/{_options.TypeName}/{jsMethodName}";
        _builder.Append($"{indent}/// <a href=\"{fullUrl}\"></a>\r\n");
        _builder.Append($"{indent}/// </summary>\r\n");

        return this;
    }

    internal SourceBuilder AppendLine()
    {
        // We use a hard-coded new line instead of:
        // _builder.AppendLine() as the new line value changes by environment.
        // For consistency, we'll always generate the exact same new line.
        _builder.Append(NewLine);

        return this;
    }

    internal SourceBuilder AppendRaw(
        string content,
        bool appendNewLine = true,
        bool postIncreaseIndentation = false,
        bool omitIndentation = false)
    {
        var indentation = omitIndentation ? "" : _indentation.ToString();
        _builder.Append($"{indentation}{content}{(appendNewLine ? NewLine : string.Empty)}");

        if (postIncreaseIndentation)
        {
            IncreaseIndentation();
        }

        return this;
    }

    internal SourceBuilder IncreaseIndentation()
    {
        IncreaseIndentationImpl(true);

        return this;
    }

    internal SourceBuilder AppendConditionalDelegateFields(List<CSharpMethod>? methods)
    {
        if (methods is { Count: > 0 })
        {
            foreach (var group in
                methods.SelectMany(m => m.ParameterDefinitions)
                    .Where(param => param.ActionDeclation is not null)
                    .GroupBy(param => param.RawName))
            {
                var param = group.First();
                var keys =
                    param.ActionDeclation!.ParameterDefinitions.Select(p => p.RawTypeName);

                var fieldName = $"_{param.RawName}";
                Fields ??= new HashSet<string>();
                Fields.Add(fieldName);

                AppendRaw($"private Action<{string.Join(", ", keys)}>? {fieldName};");
            }

            AppendLine();
        }

        return this;
    }

    internal SourceBuilder AppendConditionalDelegateCallbackMethods(List<CSharpMethod>? methods)
    {
        if (methods is { Count: > 0 })
        {
            var level = IndentationLevel;

            foreach (var group in
                methods.SelectMany(m => m.ParameterDefinitions)
                    .Where(param => param.ActionDeclation is not null)
                    .GroupBy(param => param.RawName))
            {
                var param = group.First();
                var methodName = $"On{param.RawName.CapitalizeFirstLetter()}";
                Methods ??= new HashSet<string>();
                if (Methods.Add(methodName))
                {
                    var fieldName = $"_{param.RawName}";

                    AppendRaw("[JSInvokable]");
                    AppendRaw($"public void {methodName}(", postIncreaseIndentation: true);
                    AppendParameters(param, fieldName);
                }
            }

            AppendLine();
            ResetIndentiationTo(level);
        }

        return this;
    }

    private void AppendParameters(CSharpType param, string fieldName)
    {
        var args = new List<string>();
        foreach (var (interation, p) in param.ActionDeclation!.ParameterDefinitions!.Select())
        {
            args.Add(p.RawName);
            AppendRaw($"{p.RawTypeName} {p.RawName}", appendNewLine: false);
            if (interation.HasMore)
            {
                AppendRaw(",");
            }
            else
            {
                AppendRaw(") =>", postIncreaseIndentation: true);
                AppendRaw($"{fieldName}?.Invoke({string.Join(", ", args)});");
            }
        }
    }

    internal SourceBuilder DecreaseIndentation()
    {
        DecreaseIndentationImpl(true);

        return this;
    }

    internal SourceBuilder ResetIndentiationTo(int level)
    {
        _indentation = _indentation.ResetTo(level);

        return this;
    }

    private void IncreaseIndentationImpl(bool increaseIndentation = false) =>
        AdjustIndentation(increaseIndentation
            ? IndentationAdjustment.Increase
            : IndentationAdjustment.NoOp);

    private void DecreaseIndentationImpl(bool decreaseIndentation = false) =>
        AdjustIndentation(decreaseIndentation
            ? IndentationAdjustment.Decrease
            : IndentationAdjustment.NoOp);

    private void AdjustIndentation(IndentationAdjustment adjustment) => _indentation = adjustment switch
    {
        IndentationAdjustment.Increase => _indentation.Increase(),
        IndentationAdjustment.Decrease => _indentation.Decrease(),
        _ => _indentation
    };

    internal string ToSourceCodeString() => _builder.ToString();
}
