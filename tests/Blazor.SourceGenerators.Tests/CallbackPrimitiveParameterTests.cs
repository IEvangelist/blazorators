// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for TS callback interfaces whose signature
/// includes a primitive parameter, e.g.
/// <code>
/// interface FrameRequestCallback {
///     (time: DOMHighResTimeStamp): void;
/// }
/// </code>
/// where <c>DOMHighResTimeStamp</c> is a TS alias for <c>number</c>.
///
/// <para>
/// The pre-existing emit paths split source-of-truth between two
/// collections on <see cref="CSharpAction"/>:
/// <list type="bullet">
/// <item>
///   <see cref="CSharpType.ToActionString"/> joined
///   <c>ActionDeclaration.DependentTypes.Keys</c>, which only contains
///   *custom* types. Primitive parameters silently disappeared from
///   the action's generic argument list, so a one-param callback like
///   <c>FrameRequestCallback</c> emitted the non-generic <c>Action</c>
///   (matching the zero-parameter shape) instead of
///   <c>Action&lt;double&gt;</c>.
/// </item>
/// <item>
///   <see cref="Blazor.SourceGenerators.Builders.SourceBuilder"/>
///   joined <c>ParameterDefinitions.Select(p =&gt; p.RawTypeName)</c>
///   verbatim, so the private field was emitted as
///   <c>Action&lt;number&gt;? _callback;</c> (using the TypeScript
///   spelling, which is not valid C#) and the
///   <c>[JSInvokable]</c> shim emitted <c>number time</c> as the
///   parameter type.
/// </item>
/// </list>
/// </para>
///
/// <para>
/// The fix consolidates both emit paths on a single helper that maps
/// each parameter through <see cref="Blazor.SourceGenerators.Types.TypeMap.PrimitiveTypes"/>
/// (and array-of-primitive shapes via
/// <see cref="Blazor.SourceGenerators.Types.TypeShape"/>) before
/// joining them into the action's generic argument list.
/// </para>
/// </summary>
public class CallbackPrimitiveParameterTests
{
    [Fact]
    public void ToActionString_SinglePrimitiveParameter_EmitsMappedCSharpPrimitive()
    {
        // After alias resolution (T1.x) the parser produces
        // RawTypeName = "number" for `time: DOMHighResTimeStamp`.
        var paramDef = new CSharpType("time", "number");
        var action = new CSharpAction(
            RawName: "FrameRequestCallback",
            RawReturnTypeName: "void",
            ParameterDefinitions: [paramDef]);

        var param = new CSharpType(
            "callback", "FrameRequestCallback",
            IsNullable: false, ActionDeclaration: action);

        Assert.Equal("Action<double> onCallback", param.ToActionString());
    }

    [Fact]
    public void ToActionString_MixedCustomAndPrimitiveParameters_PreservesOrderAndMapsPrimitives()
    {
        // Hypothetical: (time: number, position: GeolocationPosition): void
        // The action must preserve parameter order in the generic
        // argument list -- the shim's Invoke(...) call site depends on
        // positional alignment with the JS callback signature.
        var first = new CSharpType("time", "number");
        var second = new CSharpType("position", "GeolocationPosition");
        var action = new CSharpAction(
            RawName: "MixedCallback",
            RawReturnTypeName: "void",
            ParameterDefinitions: [first, second])
        {
            DependentTypes =
            {
                ["GeolocationPosition"] = new CSharpObject("GeolocationPosition", null),
            },
        };

        var param = new CSharpType(
            "callback", "MixedCallback",
            IsNullable: false, ActionDeclaration: action);

        Assert.Equal("Action<double, GeolocationPosition> onCallback", param.ToActionString());
    }

    [Fact]
    public void ToActionString_PrimitiveParameter_NullableEmitsMappedCSharpPrimitive()
    {
        var paramDef = new CSharpType("time", "number");
        var action = new CSharpAction(
            RawName: "FrameRequestCallback",
            RawReturnTypeName: "void",
            ParameterDefinitions: [paramDef]);

        var param = new CSharpType(
            "callback", "FrameRequestCallback",
            IsNullable: true, ActionDeclaration: action);

        Assert.Equal("Action<double>? onCallback = null", param.ToActionString());
    }

    [Fact]
    public void ToActionString_BooleanParameter_EmitsBool()
    {
        // The other common primitive parameter on a callback. Boolean
        // is in the TypeMap as "boolean" -> "bool". Verifies the
        // mapping isn't number-specific.
        var paramDef = new CSharpType("flag", "boolean");
        var action = new CSharpAction(
            RawName: "FlagCallback",
            RawReturnTypeName: "void",
            ParameterDefinitions: [paramDef]);

        var param = new CSharpType(
            "callback", "FlagCallback",
            IsNullable: false, ActionDeclaration: action);

        Assert.Equal("Action<bool> onCallback", param.ToActionString());
    }
}
