// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System;
using TypeScript.TypeConverter.CSharp;
using TypeScript.TypeConverter.Readers;

namespace TypeScript.TypeConverter.Parsers
{
    public partial class LibDomParser
    {
        private readonly LibDomReader _reader = new();

        public ParserResult<CSharpExtensionObject> ParseStaticType(string typeName)
        {
            ParserResult<CSharpExtensionObject> result = new(ParserResultStatus.Unknown);

            if (_reader.TryGetDeclaration(typeName, out var typeScriptDefinitionText) &&
                typeScriptDefinitionText is not null)
            {
                try
                {
                    result = result with
                    {
                        Status = ParserResultStatus.SuccessfullyParsed,
                        Value = ToExtensionObject(typeScriptDefinitionText)
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
}