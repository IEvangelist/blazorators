// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;

namespace blazorators
{
    [Generator]
    public class JavaScriptInteropSourceGenerator : ISourceGenerator
    {
        void ISourceGenerator.Execute(GeneratorExecutionContext context)
        {
            // STEPS:
            // 1. Warn if not referencing Microsoft.JSInterop.
            // 2. Determine if we should be adding generated source.
            // 3. Add generated source.

            // 1.
            if (!context.Compilation.ReferencedAssemblyNames.Any(
                ai => ai.Name.Equals("Microsoft.JSInterop", StringComparison.OrdinalIgnoreCase)))
            {
                // TODO: report warning that the consuming lib needs to reference this library

                //context.ReportDiagnostic(
                //    Diagnostic.Create(
                //        new DiagnosticDescriptor(
                //            1,
                //            ))
            }

            // 2.
            // TODO:

            // 3.
            context.AddSource(
                "javascript.interop.cs",
                SourceText.From(""));

            throw new NotImplementedException();
        }

        void ISourceGenerator.Initialize(GeneratorInitializationContext context)
        {
            throw new NotImplementedException();
        }
    }
}
