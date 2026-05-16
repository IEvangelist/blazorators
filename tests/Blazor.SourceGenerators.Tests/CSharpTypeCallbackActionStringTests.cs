// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.CSharp;
using Xunit;

namespace Blazor.SourceGenerators.Tests;

/// <summary>
/// Regression coverage for callback parameter emission when the callback
/// has zero parameter definitions (e.g. the TS <c>VoidFunction</c>
/// interface used by <c>queueMicrotask(callback: VoidFunction)</c>).
///
/// <para>
/// <c>ToActionString</c> previously joined <see
/// cref="CSharpAction.DependentTypes"/> keys directly into the generic
/// argument list. For a zero-argument callback that set is empty, so
/// the generator emitted <c>Action&lt;&gt;? onCallback = null</c>,
/// which does not compile. The fix emits the non-generic <c>Action</c>
/// when there are no parameters.
/// </para>
/// </summary>
public class CSharpTypeCallbackActionStringTests
{
    [Fact]
    public void ToActionString_ZeroParameterCallback_EmitsNonGenericAction()
    {
        var action = new CSharpAction(
            RawName: "callback",
            RawReturnTypeName: "void",
            ParameterDefinitions: []);

        var param = new CSharpType("callback", "VoidFunction", IsNullable: false, ActionDeclaration: action);

        Assert.Equal("Action onCallback", param.ToActionString());
    }

    [Fact]
    public void ToActionString_ZeroParameterCallback_NullableEmitsNonGenericActionWithDefault()
    {
        var action = new CSharpAction(
            RawName: "callback",
            RawReturnTypeName: "void",
            ParameterDefinitions: []);

        var param = new CSharpType("callback", "VoidFunction", IsNullable: true, ActionDeclaration: action);

        Assert.Equal("Action? onCallback = null", param.ToActionString());
    }

    [Fact]
    public void ToActionString_SingleCustomTypeParameter_EmitsTypedAction()
    {
        var paramDef = new CSharpType("position", "GeolocationPosition");
        var action = new CSharpAction(
            RawName: "callback",
            RawReturnTypeName: "void",
            ParameterDefinitions: [paramDef])
        {
            DependentTypes = { ["GeolocationPosition"] = new CSharpObject("GeolocationPosition", null) }
        };

        var param = new CSharpType("callback", "PositionCallback", IsNullable: false, ActionDeclaration: action);

        Assert.Equal("Action<GeolocationPosition> onCallback", param.ToActionString());
    }
}
