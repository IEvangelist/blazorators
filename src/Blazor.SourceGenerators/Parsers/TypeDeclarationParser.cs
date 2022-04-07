// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Parsers;

internal sealed partial class TypeDeclarationParser
{
    static readonly Lazy<TypeDeclarationParser> s_defaultParser =
        new(
            valueFactory: () => new TypeDeclarationParser(TypeDeclarationReader.Default));

    readonly TypeDeclarationReader _reader;
    
    internal static TypeDeclarationParser Default => s_defaultParser.Value;

    internal TypeDeclarationParser(TypeDeclarationReader reader) => _reader = reader;

    public ParserResult<CSharpTopLevelObject> ParseTargetType(string typeName)
    {
        ParserResult<CSharpTopLevelObject> result = new(ParserResultStatus.Unknown);

        if (_reader.TryGetDeclaration(typeName, out var typeScriptDefinitionText) &&
            typeScriptDefinitionText is not null)
        {
            try
            {
                result = result with
                {
                    Status = ParserResultStatus.SuccessfullyParsed,
                    Value = ToTopLevelObject(typeScriptDefinitionText)
                };
            }
            catch (Exception ex)
            {
                result = result with
                {
                    Status = ParserResultStatus.ErrorParsing,
                    Error = ex.Message
                };
            }
        }
        else
        {
            result = result with
            {
                Status = ParserResultStatus.TargetTypeNotFound
            };
        }

        return result;
    }
}
