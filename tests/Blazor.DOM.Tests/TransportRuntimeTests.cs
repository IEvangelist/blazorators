// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.DOM.Tests.Fakes;
using System.Text.Json;

namespace Blazor.DOM.Tests;

public sealed class TransportRuntimeTests
{
    private static readonly DomTransportDescriptor s_blobTransport =
        DomTransportDescriptor.JsReference(
            "Blob",
            nullable: true,
            streamable: true,
            structuredClone: true);

    private static readonly DomTransportDescriptor s_eventTransport =
        DomTransportDescriptor.JsReference("Event");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_results_use_callback_delivery_for_every_path(bool wasm)
    {
        var host = CreateHost(wasm);
        var propertyReference = new FakeJSObjectReference();
        var methodReference = new FakeJSObjectReference();
        var indexReference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["getPropertyDotNetObjectReference"] =
            DeliverResultReference(propertyReference, handlerIndex: 2);
        host.Module.InvocationHandlers["invokeMethodDotNetObjectReference"] =
            DeliverResultReference(methodReference, handlerIndex: 3);
        host.Module.InvocationHandlers["getIndexDotNetObjectReference"] =
            DeliverResultReference(indexReference, handlerIndex: 2);
        var target = new FakeJSObjectReference();

        var property = await host.Runtime.GetPropertyRefAsync(
            target,
            "ownerDocument");
        var method = await host.Runtime.InvokeMethodRefAsync(
            target,
            "querySelector",
            ["main"]);
        var index = await host.Runtime.GetIndexRefAsync(target, 0);

        Assert.Same(propertyReference, property);
        Assert.Same(methodReference, method);
        Assert.Same(indexReference, index);
        Assert.Contains(
            host.Module.Invocations,
            invocation => invocation.Identifier == "getPropertyDotNetObjectReference");
        Assert.Contains(
            host.Module.Invocations,
            invocation => invocation.Identifier == "invokeMethodDotNetObjectReference");
        Assert.Contains(
            host.Module.Invocations,
            invocation => invocation.Identifier == "getIndexDotNetObjectReference");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nullable_blob_callback_receives_null_typed_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        host.Module.InvocationHandlers["invokeMethodReferenceCallback"] =
            async (args, _) =>
            {
                var handler = Assert.IsType<
                    DotNetObjectReference<DomReferenceCallbackHandler<FixtureBlobProxy>>>(
                    args![4]);
                await handler.Value.HandleReferenceAsync(null);
                return null;
            };
        DomBorrowedReference<FixtureBlobProxy>? received = null;
        var callbackInvoked = false;

        await host.Runtime.InvokeMethodReferenceCallbackAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            "toBlob",
            0,
            ["image/png"],
            factory,
            s_blobTransport,
            value =>
            {
                callbackInvoked = true;
                received = value;
                return Task.CompletedTask;
            });

        Assert.True(callbackInvoked);
        Assert.Null(received);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Primitive_and_reviewed_DTO_values_use_JSON(bool wasm)
    {
        var host = CreateHost(wasm);
        var target = new FakeJSObjectReference();
        host.Module.ReturnValues["invokeMethod"] = 7;

        var primitive = await host.Runtime.InvokeMethodAsync<int>(
            target,
            "primitive",
            [new Dictionary<string, object?> { ["enabled"] = true }]);

        host.Module.ReturnValues["invokeMethod"] = new FixtureDto("typed");
        var dto = await host.Runtime.InvokeMethodAsync<FixtureDto>(
            target,
            "dto",
            [new FixtureDto("argument")]);
        host.Module.ReturnValues.Remove("invokeMethod");
        await host.Runtime.InvokeMethodVoidAsync(
            target,
            "dynamic",
            [DomDynamicValue.Json("explicit", "unknown")]);

        Assert.Equal(7, primitive);
        Assert.Equal("typed", dto.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typed_union_selects_JSON_or_reference_transport(bool wasm)
    {
        var host = CreateHost(wasm);
        var target = new FakeJSObjectReference();
        var reference = new FakeJSObjectReference();

        await host.Runtime.InvokeMethodVoidAsync(
            target,
            "jsonUnion",
            [FixtureUnion.FromJson("text")]);
        await host.Runtime.InvokeMethodVoidAsync(
            target,
            "referenceUnion",
            [FixtureUnion.FromReference(reference)]);

        var jsonCall = Assert.Single(
            host.Module.Invocations,
            call => call.Args?[1] as string == "jsonUnion");
        var jsonArguments = Assert.IsType<object?[]>(jsonCall.Args![2]);
        Assert.Equal("text", jsonArguments[0]);
        var referenceCall = Assert.Single(
            host.Module.Invocations,
            call => call.Args?[1] as string == "referenceUnion");
        var referenceArguments = Assert.IsType<object?[]>(referenceCall.Args![2]);
        Assert.Same(reference, referenceArguments[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typed_union_rejects_uninitialized_and_wrong_arm_transport(bool wasm)
    {
        var host = CreateHost(wasm);
        var target = new FakeJSObjectReference();

        var uninitialized = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                target,
                "invalid",
                [default(FixtureUnion)]).AsTask());
        var wrongType = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                target,
                "invalid",
                [FixtureUnion.WithWrongReferenceValue("not a reference")]).AsTask());

        Assert.Contains("no selected arm", uninitialized.Message);
        Assert.Contains(nameof(IJSObjectReference), wrongType.Message);
        Assert.Empty(host.Module.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nullable_union_arm_normalizes_inside_JSON_container(bool wasm)
    {
        var host = CreateHost(wasm);

        await host.Runtime.InvokeMethodVoidAsync(
            new FakeJSObjectReference(),
            "nestedUnion",
            [new[] { FixtureUnion.FromNull() }]);

        var invocation = Assert.Single(host.Module.Invocations);
        var arguments = Assert.IsType<object?[]>(invocation.Args![2]);
        var values = Assert.IsType<object?[]>(arguments[0]);
        Assert.Null(Assert.Single(values));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inbound_union_delivers_discriminated_JSON_arm(bool wasm)
    {
        var host = CreateHost(wasm);
        host.Module.InvocationHandlers["invokeMethodUnion"] = async (args, _) =>
        {
            var handler = Assert.IsType<
                DotNetObjectReference<DomUnionDeliveryHandler<string>>>(args![4]);
            Assert.True(await handler.Value.ReceiveJsonAsync(
                0,
                JsonSerializer.SerializeToElement("clipboard text")));
            return null;
        };

        var result = await host.Runtime.InvokeMethodUnionAsync(
            new FakeJSObjectReference(),
            "read",
            null,
            [
                DomUnionInboundArm<string>.String(value => $"text:{value}"),
                DomUnionInboundArm<string>.Reference("Blob", _ => "blob"),
            ]);

        Assert.Equal("text:clipboard text", result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inbound_union_delivers_discriminated_reference_arm(bool wasm)
    {
        var host = CreateHost(wasm);
        var reference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["invokeMethodUnion"] = async (args, _) =>
        {
            var handler = Assert.IsType<
                DotNetObjectReference<DomUnionDeliveryHandler<string>>>(args![4]);
            Assert.True(await handler.Value.ReceiveReferenceAsync(1, reference));
            return null;
        };

        var result = await host.Runtime.InvokeMethodUnionAsync(
            new FakeJSObjectReference(),
            "read",
            null,
            [
                DomUnionInboundArm<string>.String(value => $"text:{value}"),
                DomUnionInboundArm<string>.Reference(
                    "Blob",
                    value => ReferenceEquals(reference, value) ? "blob" : "wrong"),
            ]);

        Assert.Equal("blob", result);
        Assert.False(reference.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inbound_union_factory_failure_rolls_back_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var reference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["invokeMethodUnion"] = async (args, _) =>
        {
            var handler = Assert.IsType<
                DotNetObjectReference<DomUnionDeliveryHandler<string>>>(args![4]);
            Assert.True(await handler.Value.ReceiveReferenceAsync(0, reference));
            return null;
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Runtime.InvokeMethodUnionAsync(
                new FakeJSObjectReference(),
                "read",
                null,
                [
                    DomUnionInboundArm<string>.Reference(
                        "Blob",
                        _ => throw new InvalidOperationException("factory failed")),
                ]).AsTask());

        Assert.True(reference.IsDisposed);
        Assert.Equal(1, reference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_result_callback_returns_value_and_releases_borrowed_proxy(
        bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var reference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["invokeMethodReferenceResultCallback"] =
            async (args, _) =>
            {
                var handler = Assert.IsType<DotNetObjectReference<
                    DomReferenceResultCallbackHandler<FixtureBlobProxy, int>>>(
                    args![4]);
                var delivery = await handler.Value
                    .HandleReferenceResultAsync(reference);
                Assert.True(delivery.Accepted);
                return delivery.Result;
            };

        var result = await host.Runtime
            .InvokeMethodReferenceResultCallbackAsync<FixtureBlobProxy, int>(
                new FakeJSObjectReference(),
                "request",
                1,
                ["exclusive"],
                factory,
                DomTransportDescriptor.JsReference("FixtureBlob | null", true),
                value => Task.FromResult(value is null ? 0 : 42));

        Assert.Equal(42, result);
        Assert.True(reference.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unsupported_transport_fails_before_JS_invocation(bool wasm)
    {
        var host = CreateHost(wasm);
        var target = new FakeJSObjectReference();

        var resultError = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodAsync<object>(
                target,
                "ambiguous",
                null).AsTask());
        var argumentError = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                target,
                "ambiguous",
                [new UnreviewedDto("value")]).AsTask());
        var explicitError = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                target,
                "ambiguous",
                [
                    DomDynamicValue.Create(
                        "value",
                        DomTransportDescriptor.Unsupported(
                            "unknown",
                            "Union has incompatible transports.")),
                ]).AsTask());
        var invalidEscapeHatch = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                target,
                "ambiguous",
                [DomDynamicValue.Json(new UnreviewedDto("value"))]).AsTask());
        var invalidReferenceEscapeHatch = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                target,
                "ambiguous",
                [
                    DomDynamicValue.Create(
                        "not a reference",
                        DomTransportDescriptor.JsReference("unknown")),
                ]).AsTask());

        Assert.Contains("not an approved JSON transport", resultError.Message);
        Assert.Contains(nameof(UnreviewedDto), argumentError.Message);
        Assert.Contains(
            "Union has incompatible transports",
            explicitError.Message);
        Assert.Contains(nameof(UnreviewedDto), invalidEscapeHatch.Message);
        Assert.Contains(
            nameof(IJSObjectReference),
            invalidReferenceEscapeHatch.Message);
        Assert.Empty(host.Module.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_JSON_cycle_reports_current_and_ancestor_paths(bool wasm)
    {
        var host = CreateHost(wasm);
        var value = new Dictionary<string, object?>();
        value["self"] = value;

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                new FakeJSObjectReference(),
                "cyclic",
                [value]).AsTask());

        Assert.Equal(
            "JSON container at 'arguments[0].self' contains a reference cycle " +
            "to 'arguments[0]'.",
            exception.Message);
        Assert.Empty(host.Module.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Indirect_JSON_cycle_reports_escaped_precise_path(bool wasm)
    {
        var host = CreateHost(wasm);
        var root = new Dictionary<string, object?>();
        var child = new List<object?>();
        root["child node"] = child;
        child.Add(new Dictionary<string, object?> { ["parent"] = root });

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                new FakeJSObjectReference(),
                "cyclic",
                [root]).AsTask());

        Assert.Equal(
            "JSON container at 'arguments[0][\"child node\"][0].parent' contains " +
            "a reference cycle to 'arguments[0]'.",
            exception.Message);
        Assert.Empty(host.Module.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JSON_depth_limit_accepts_exact_boundary(bool wasm)
    {
        var host = CreateHost(wasm);
        var value = CreateNestedJson(DomTransportValidator.MaximumJsonContainerDepth);

        await host.Runtime.InvokeMethodVoidAsync(
            new FakeJSObjectReference(),
            "boundary",
            [value]);

        Assert.Single(
            host.Module.Invocations,
            invocation => invocation.Identifier == "invokeMethod");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JSON_depth_limit_rejects_first_container_beyond_boundary(bool wasm)
    {
        var host = CreateHost(wasm);
        var value = CreateNestedJson(
            DomTransportValidator.MaximumJsonContainerDepth + 1);
        var rejectedPath = "arguments[0]" +
            string.Concat(
                Enumerable.Repeat(
                    ".child",
                    DomTransportValidator.MaximumJsonContainerDepth));

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                new FakeJSObjectReference(),
                "tooDeep",
                [value]).AsTask());

        Assert.Equal(
            $"JSON container at '{rejectedPath}' exceeds the maximum validation " +
            $"depth of {DomTransportValidator.MaximumJsonContainerDepth}.",
            exception.Message);
        Assert.Empty(host.Module.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Single_pass_JSON_sequence_is_materialized_once(bool wasm)
    {
        var host = CreateHost(wasm);
        var sequence = new DestructiveEnumerable(
            [1, "two", new Dictionary<string, object?> { ["three"] = 3 }]);

        await host.Runtime.InvokeMethodVoidAsync(
            new FakeJSObjectReference(),
            "consume",
            [sequence]);

        Assert.Equal(1, sequence.EnumerationCount);
        var invocation = Assert.Single(
            host.Module.Invocations,
            item => item.Identifier == "invokeMethod");
        var arguments = Assert.IsType<object?[]>(invocation.Args![2]);
        var normalized = Assert.IsType<List<object?>>(arguments[0]);
        Assert.Equal(3, normalized.Count);
        Assert.Equal(1, normalized[0]);
        Assert.Equal("two", normalized[1]);
        var dictionary = Assert.IsType<Dictionary<string, object?>>(normalized[2]);
        Assert.Equal(3, dictionary["three"]);
    }

    [Theory]
    [InlineData(JsonPayloadDestination.Method)]
    [InlineData(JsonPayloadDestination.Property)]
    [InlineData(JsonPayloadDestination.Index)]
    public async Task Real_JSRuntime_marshals_exact_JSON_depth_boundary(
        JsonPayloadDestination destination)
    {
        var host = await CreateSerializingHostAsync();
        var value = CreateNestedJson(DomTransportValidator.MaximumJsonContainerDepth);

        await DispatchJsonPayloadAsync(host.Runtime, host.Target, destination, value);

        Assert.Equal(
            DomTransportValidator.FrameworkJsonSerializerMaxDepth,
            host.JSRuntime.SerializerMaxDepth);
        Assert.Equal(
            DomTransportValidator.MaximumJsonContainerDepth +
                DomTransportValidator.MaximumInteropJsonWrapperDepth +
                DomTransportValidator.RequiredTerminalJsonValueDepth,
            host.JSRuntime.SerializerMaxDepth);
        var invocation = Assert.Single(
            host.JSRuntime.Invocations,
            item => item.Identifier == GetPayloadIdentifier(destination));
        using var document = JsonDocument.Parse(
            Assert.IsType<string>(invocation.ArgsJson));
        var marshaled = document.RootElement[2];
        if (destination == JsonPayloadDestination.Method)
        {
            marshaled = marshaled[0];
        }
        for (var depth = 1;
            depth < DomTransportValidator.MaximumJsonContainerDepth;
            depth++)
        {
            marshaled = marshaled.GetProperty("child");
        }
        Assert.True(marshaled.GetProperty("value").GetBoolean());
    }

    [Theory]
    [InlineData(JsonPayloadDestination.Method)]
    [InlineData(JsonPayloadDestination.Property)]
    [InlineData(JsonPayloadDestination.Index)]
    public async Task Real_JSRuntime_rejects_first_JSON_container_beyond_boundary(
        JsonPayloadDestination destination)
    {
        var host = await CreateSerializingHostAsync();
        var value = CreateNestedJson(
            DomTransportValidator.MaximumJsonContainerDepth + 1);
        var rootPath = destination switch
        {
            JsonPayloadDestination.Method => "arguments[0]",
            JsonPayloadDestination.Property => "property 'boundary'",
            JsonPayloadDestination.Index => "index [7]",
            _ => throw new ArgumentOutOfRangeException(nameof(destination)),
        };
        var rejectedPath = rootPath +
            string.Concat(
                Enumerable.Repeat(
                    ".child",
                    DomTransportValidator.MaximumJsonContainerDepth));

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => DispatchJsonPayloadAsync(
                host.Runtime,
                host.Target,
                destination,
                value).AsTask());

        Assert.Equal(
            $"JSON container at '{rejectedPath}' exceeds the maximum validation " +
            $"depth of {DomTransportValidator.MaximumJsonContainerDepth}.",
            exception.Message);
        Assert.Empty(host.JSRuntime.Invocations);
    }

    [Fact]
    public async Task Real_JSRuntime_direct_cycle_fails_before_invocation()
    {
        var host = await CreateSerializingHostAsync();
        var value = new Dictionary<string, object?>();
        value["self"] = value;

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                host.Target,
                "cyclic",
                [value]).AsTask());

        Assert.Equal(
            "JSON container at 'arguments[0].self' contains a reference cycle " +
            "to 'arguments[0]'.",
            exception.Message);
        Assert.Empty(host.JSRuntime.Invocations);
    }

    [Fact]
    public async Task Real_JSRuntime_indirect_cycle_fails_before_invocation()
    {
        var host = await CreateSerializingHostAsync();
        var root = new Dictionary<string, object?>();
        var child = new List<object?>();
        root["child node"] = child;
        child.Add(new Dictionary<string, object?> { ["parent"] = root });

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                host.Target,
                "cyclic",
                [root]).AsTask());

        Assert.Equal(
            "JSON container at 'arguments[0][\"child node\"][0].parent' contains " +
            "a reference cycle to 'arguments[0]'.",
            exception.Message);
        Assert.Empty(host.JSRuntime.Invocations);
    }

    [Fact]
    public async Task Real_JSRuntime_marshals_single_pass_sequence_after_one_materialization()
    {
        var host = await CreateSerializingHostAsync();
        var sequence = new DestructiveEnumerable(
            [1, "two", new Dictionary<string, object?> { ["three"] = 3 }]);

        await host.Runtime.InvokeMethodVoidAsync(
            host.Target,
            "consume",
            [sequence]);

        Assert.Equal(1, sequence.EnumerationCount);
        var invocation = Assert.Single(
            host.JSRuntime.Invocations,
            item => item.Identifier == "invokeMethod");
        using var document = JsonDocument.Parse(
            Assert.IsType<string>(invocation.ArgsJson));
        var marshaled = document.RootElement[2][0];
        Assert.Equal(1, marshaled[0].GetInt32());
        Assert.Equal("two", marshaled[1].GetString());
        Assert.Equal(3, marshaled[2].GetProperty("three").GetInt32());
    }

    [Fact]
    public async Task Real_JSRuntime_marshals_JsonElement_at_depth_boundary()
    {
        var host = await CreateSerializingHostAsync();
        var value = JsonSerializer.SerializeToElement(
            CreateNestedJson(DomTransportValidator.MaximumJsonContainerDepth));

        await host.Runtime.InvokeMethodVoidAsync(
            host.Target,
            "jsonElement",
            [value]);

        Assert.Single(
            host.JSRuntime.Invocations,
            item => item.Identifier == "invokeMethod");
    }

    [Fact]
    public async Task Deep_JsonElement_fails_before_real_JSRuntime_invocation()
    {
        var host = await CreateSerializingHostAsync();
        var value = JsonSerializer.SerializeToElement(
            CreateNestedJson(DomTransportValidator.MaximumJsonContainerDepth + 1));

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                host.Target,
                "jsonElement",
                [value]).AsTask());

        Assert.Contains(
            $"depth of {DomTransportValidator.MaximumJsonContainerDepth}",
            exception.Message);
        Assert.Empty(host.JSRuntime.Invocations);
    }

    [Fact]
    public async Task Real_JSRuntime_marshals_reviewed_DTO_at_depth_boundary()
    {
        var host = await CreateSerializingHostAsync();
        var value = CreateNestedDto(DomTransportValidator.MaximumJsonContainerDepth);

        await host.Runtime.InvokeMethodVoidAsync(
            host.Target,
            "reviewedDto",
            [value]);

        Assert.Single(
            host.JSRuntime.Invocations,
            item => item.Identifier == "invokeMethod");
    }

    [Fact]
    public async Task Deep_reviewed_DTO_fails_before_real_JSRuntime_invocation()
    {
        var host = await CreateSerializingHostAsync();
        var value = CreateNestedDto(
            DomTransportValidator.MaximumJsonContainerDepth + 1);

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodVoidAsync(
                host.Target,
                "reviewedDto",
                [value]).AsTask());

        Assert.Contains(
            $"depth of {DomTransportValidator.MaximumJsonContainerDepth}",
            exception.Message);
        Assert.Empty(host.JSRuntime.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nullable_reference_results_return_typed_null_for_every_path(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        host.Module.InvocationHandlers["getPropertyDotNetObjectReference"] =
            DeliverResultReference(null, handlerIndex: 2);
        host.Module.InvocationHandlers["invokeMethodDotNetObjectReference"] =
            DeliverResultReference(null, handlerIndex: 3);
        host.Module.InvocationHandlers["getIndexDotNetObjectReference"] =
            DeliverResultReference(null, handlerIndex: 2);

        var property = await host.Runtime.GetPropertyReferenceAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            "nullableBlob",
            factory,
            s_blobTransport);
        var method = await host.Runtime.InvokeMethodReferenceAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            "nullableBlob",
            null,
            factory,
            s_blobTransport);
        var index = await host.Runtime.GetIndexReferenceAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            0,
            factory,
            s_blobTransport);

        Assert.Null(property);
        Assert.Null(method);
        Assert.Null(index);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Non_null_reference_results_are_owned_typed_proxies_for_every_path(
        bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var propertyReference = new FakeJSObjectReference();
        var methodReference = new FakeJSObjectReference();
        var indexReference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["getPropertyDotNetObjectReference"] =
            DeliverResultReference(propertyReference, handlerIndex: 2);
        host.Module.InvocationHandlers["invokeMethodDotNetObjectReference"] =
            DeliverResultReference(methodReference, handlerIndex: 3);
        host.Module.InvocationHandlers["getIndexDotNetObjectReference"] =
            DeliverResultReference(indexReference, handlerIndex: 2);
        var target = new FakeJSObjectReference();

        var property = await host.Runtime.GetPropertyReferenceAsync<FixtureBlobProxy>(
            target,
            "blob",
            factory,
            s_blobTransport);
        var method = await host.Runtime.InvokeMethodReferenceAsync<FixtureBlobProxy>(
            target,
            "slice",
            null,
            factory,
            s_blobTransport);
        var index = await host.Runtime.GetIndexReferenceAsync<FixtureBlobProxy>(
            target,
            0,
            factory,
            s_blobTransport);

        Assert.Same(propertyReference, property?.Reference);
        Assert.Same(methodReference, method?.Reference);
        Assert.Same(indexReference, index?.Reference);
        Assert.Equal(0, propertyReference.DisposeCallCount);
        Assert.Equal(0, methodReference.DisposeCallCount);
        Assert.Equal(0, indexReference.DisposeCallCount);
        await property!.DisposeAsync();
        await method!.DisposeAsync();
        await index!.DisposeAsync();
        Assert.Equal(1, propertyReference.DisposeCallCount);
        Assert.Equal(1, methodReference.DisposeCallCount);
        Assert.Equal(1, indexReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_result_failure_after_delivery_releases_proxy(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var reference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["getPropertyDotNetObjectReference"] =
            async (args, _) =>
            {
                var handler = Assert.IsType<
                    DotNetObjectReference<DomReferenceDeliveryHandler>>(
                    args![2]);
                Assert.True(
                    await handler.Value.ReceiveReferenceAsync(reference));
                throw new JSException("result delivery failed");
            };

        await Assert.ThrowsAsync<JSException>(
            () => host.Runtime.GetPropertyReferenceAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "blob",
                factory,
                DomTransportDescriptor.JsReference("Blob")).AsTask());

        Assert.Equal(1, reference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_cancellation_before_invocation_creates_no_JS_reference(
        bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.Runtime.GetPropertyReferenceAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "blob",
                factory,
                s_blobTransport,
                cancellationSource.Token).AsTask());

        Assert.DoesNotContain(
            host.Module.Invocations,
            invocation => invocation.Identifier == "getPropertyDotNetObjectReference");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_cancellation_during_delivery_rejects_and_disposes_late_ID(
        bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        using var cancellationSource = new CancellationTokenSource();
        var deliveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        DomReferenceDeliveryHandler? deliveryHandler = null;
        host.Module.InvocationHandlers["getPropertyDotNetObjectReference"] =
            async (args, cancellationToken) =>
            {
                deliveryHandler = Assert.IsType<
                    DotNetObjectReference<DomReferenceDeliveryHandler>>(args![2]).Value;
                deliveryStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            };

        var operation = host.Runtime.GetPropertyReferenceAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            "blob",
            factory,
            s_blobTransport,
            cancellationSource.Token).AsTask();
        await deliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        var lateReference = new FakeJSObjectReference();
        var accepted = await Assert.IsType<DomReferenceDeliveryHandler>(deliveryHandler)
            .ReceiveReferenceAsync(lateReference);
        Assert.False(accepted);
        Assert.Equal(1, lateReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_cancellation_after_delivery_releases_delivered_ID(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        using var cancellationSource = new CancellationTokenSource();
        var reference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["getPropertyDotNetObjectReference"] =
            async (args, _) =>
            {
                var handler = Assert.IsType<
                    DotNetObjectReference<DomReferenceDeliveryHandler>>(args![2]);
                Assert.True(
                    await handler.Value.ReceiveReferenceAsync(reference));
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.Runtime.GetPropertyReferenceAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "blob",
                factory,
                s_blobTransport,
                cancellationSource.Token).AsTask());

        Assert.Equal(1, reference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reference_cancellation_after_commit_preserves_caller_ownership(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        using var cancellationSource = new CancellationTokenSource();
        var reference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["getPropertyDotNetObjectReference"] =
            DeliverResultReference(reference, handlerIndex: 2);

        var result = await host.Runtime.GetPropertyReferenceAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            "blob",
            factory,
            s_blobTransport,
            cancellationSource.Token);
        cancellationSource.Cancel();

        Assert.Equal(0, reference.DisposeCallCount);
        await result!.DisposeAsync();
        Assert.Equal(1, reference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Borrowed_reference_is_released_after_awaited_handler(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var blobReference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["invokeMethodReferenceCallback"] =
            DeliverReference(blobReference);
        host.Module.ReturnValues["getProperty"] = 4L;
        DomBorrowedReference<FixtureBlobProxy>? borrowed = null;

        await host.Runtime.InvokeMethodReferenceCallbackAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            "toBlob",
            0,
            null,
            factory,
            s_blobTransport,
            async value =>
            {
                borrowed = Assert.IsType<DomBorrowedReference<FixtureBlobProxy>>(value);
                Assert.Same(blobReference, borrowed.Proxy.Reference);
                Assert.Equal(4, await borrowed.Proxy.GetSizeAsync());
                await Task.Yield();
                Assert.False(blobReference.IsDisposed);
            });

        Assert.Equal(1, blobReference.DisposeCallCount);
        Assert.Throws<ObjectDisposedException>(() => _ = borrowed!.Proxy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Promotion_transfers_ownership_and_double_disposal_is_safe(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var blobReference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["invokeMethodReferenceCallback"] =
            DeliverReference(blobReference);
        FixtureBlobProxy? promoted = null;

        await host.Runtime.InvokeMethodReferenceCallbackAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            "toBlob",
            0,
            null,
            factory,
            s_blobTransport,
            value =>
            {
                promoted = Assert.IsType<DomBorrowedReference<FixtureBlobProxy>>(value)
                    .Promote();
                return Task.CompletedTask;
            });

        Assert.NotNull(promoted);
        Assert.Equal(0, blobReference.DisposeCallCount);
        await promoted.DisposeAsync();
        await promoted.DisposeAsync();
        Assert.Equal(1, blobReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Callback_exception_releases_even_a_provisionally_promoted_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var blobReference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["invokeMethodReferenceCallback"] =
            DeliverReference(blobReference);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Runtime.InvokeMethodReferenceCallbackAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "toBlob",
                0,
                null,
                factory,
                s_blobTransport,
                value =>
                {
                    _ = Assert.IsType<DomBorrowedReference<FixtureBlobProxy>>(value)
                        .Promote();
                    throw new InvalidOperationException("handler failed");
                }).AsTask());

        Assert.Equal("handler failed", exception.Message);
        Assert.Equal(1, blobReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Proxy_factory_failure_releases_callback_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = new DomProxyFactory(host.Runtime);
        var blobReference = new FakeJSObjectReference();
        host.Module.InvocationHandlers["invokeMethodReferenceCallback"] =
            DeliverReference(blobReference);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.Runtime.InvokeMethodReferenceCallbackAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "toBlob",
                0,
                null,
                factory,
                s_blobTransport,
                _ => Task.CompletedTask).AsTask());

        Assert.Equal(1, blobReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typed_event_path_uses_borrowed_reference_and_owned_registration(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var registration = new FakeJSObjectReference();
        DomReferenceCallbackHandler<FixtureBlobProxy>? handler = null;
        host.Module.InvocationHandlers["addDotNetReferenceEventListener"] =
            async (args, _) =>
            {
                handler = Assert.IsType<
                    DotNetObjectReference<DomReferenceCallbackHandler<FixtureBlobProxy>>>(
                    args![2]).Value;
                var registrationHandler = Assert.IsType<
                    DotNetObjectReference<DomEventRegistrationHandler>>(
                    args[4]);
                await registrationHandler.Value.ReceiveRegistrationAsync(registration);
                return null;
            };
        var eventReference = new FakeJSObjectReference();
        FixtureBlobProxy? received = null;

        var subscription = await host.Runtime.AddReferenceEventListenerAsync<FixtureBlobProxy>(
            new FakeJSObjectReference(),
            "dataavailable",
            factory,
            s_eventTransport,
            value =>
            {
                received = value.Proxy;
                return Task.CompletedTask;
            });
        await Assert.IsType<DomReferenceCallbackHandler<FixtureBlobProxy>>(handler)
            .HandleReferenceAsync(eventReference);
        await subscription.DisposeAsync();
        await subscription.DisposeAsync();

        Assert.NotNull(received);
        Assert.Equal(1, eventReference.DisposeCallCount);
        Assert.Single(
            registration.Invocations,
            invocation => invocation.Identifier == "dispose");
        Assert.Equal(1, registration.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Descriptor_subscription_routes_name_transport_and_options_through_existing_registry(
        bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var targetReference = new FakeJSObjectReference();
        var target = new FixtureBlobProxy(targetReference, host.Runtime, factory);
        var registration = new FakeJSObjectReference();
        object? interopOptions = null;
        host.Module.InvocationHandlers["addDotNetReferenceEventListener"] =
            async (args, _) =>
            {
                Assert.Equal("ready-state", args![1]);
                interopOptions = args[6];
                var registrationHandler = Assert.IsType<
                    DotNetObjectReference<DomEventRegistrationHandler>>(args[4]);
                await registrationHandler.Value.ReceiveRegistrationAsync(registration);
                return null;
            };
        var descriptor = DomEventDescriptor<FixtureBlobProxy>.Reference(
            "ready-state",
            "FixtureEventMap",
            "FixtureBlob",
            deprecated: false,
            "FixtureEventMap/decl[0]/member[0]/ready-state");

        var subscription = await target.SubscribeAsync(
            descriptor,
            _ => Task.CompletedTask,
            new DomEventListenerOptions
            {
                Capture = true,
                Once = true,
                Passive = false,
            });
        await subscription.DisposeAsync();

        Assert.NotNull(interopOptions);
        var type = interopOptions.GetType();
        Assert.Equal(true, type.GetProperty("capture")!.GetValue(interopOptions));
        Assert.Equal(true, type.GetProperty("once")!.GetValue(interopOptions));
        Assert.Equal(false, type.GetProperty("passive")!.GetValue(interopOptions));
        Assert.Single(
            registration.Invocations,
            invocation => invocation.Identifier == "dispose");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Value_descriptor_uses_typed_json_callback_and_existing_listener_registry(
        bool wasm)
    {
        var host = CreateHost(wasm);
        DomCallbackHandler? handler = null;
        host.Module.InvocationHandlers["addDotNetEventListener"] =
            async (args, _) =>
            {
                handler = Assert.IsType<
                    DotNetObjectReference<DomCallbackHandler>>(args![2]).Value;
                var registrationHandler = Assert.IsType<
                    DotNetObjectReference<DomEventIdRegistrationHandler>>(args[4]);
                await registrationHandler.Value.ReceiveRegistrationAsync(77);
                return null;
            };
        var descriptor = DomEventDescriptor<int?>.Value(
            "count",
            "FixtureEventMap",
            "number | null",
            nullable: true,
            deprecated: false,
            "FixtureEventMap/decl[0]/member[0]/count");
        int? received = null;

        var subscription = await host.Runtime.SubscribeValueAsync(
            new FakeJSObjectReference(),
            descriptor,
            value =>
            {
                received = value;
                return Task.CompletedTask;
            },
            options: true);
        await Assert.IsType<DomCallbackHandler>(handler).HandleEventAsync("42");
        await subscription.DisposeAsync();

        Assert.Equal(42, received);
        Assert.Contains(
            host.Module.Invocations,
            invocation => invocation.Identifier == "removeDotNetEventListener"
                && Equals(invocation.Args![0], 77));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typed_event_registration_failure_closes_handler_and_late_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        DomReferenceCallbackHandler<FixtureBlobProxy>? handler = null;
        host.Module.InvocationHandlers["addDotNetReferenceEventListener"] =
            (args, _) =>
            {
                handler = Assert.IsType<
                    DotNetObjectReference<DomReferenceCallbackHandler<FixtureBlobProxy>>>(
                    args![2]).Value;
                return ValueTask.FromException<object?>(
                    new JSException("registration failed"));
            };

        await Assert.ThrowsAsync<JSException>(
            () => host.Runtime.AddReferenceEventListenerAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "dataavailable",
                factory,
                s_eventTransport,
                _ => Task.CompletedTask).AsTask());

        var lateReference = new FakeJSObjectReference();
        var accepted =
            await Assert.IsType<DomReferenceCallbackHandler<FixtureBlobProxy>>(handler)
                .HandleReferenceAsync(lateReference);
        Assert.False(accepted);
        Assert.Equal(1, lateReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Event_cancellation_before_invocation_registers_nothing(bool wasm)
    {
        var jsonHost = CreateHost(wasm);
        var typedHost = CreateHost(wasm);
        var factory = CreateFactory(typedHost.Runtime);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => jsonHost.Runtime.AddEventListenerAsync(
                new FakeJSObjectReference(),
                "click",
                _ => Task.CompletedTask,
                cancellationSource.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => typedHost.Runtime.AddReferenceEventListenerAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "dataavailable",
                factory,
                s_eventTransport,
                _ => Task.CompletedTask,
                cancellationSource.Token).AsTask());

        Assert.Empty(jsonHost.Module.Invocations);
        Assert.Empty(typedHost.Module.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typed_event_cancellation_during_delivery_disposes_late_registration(
        bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        using var cancellationSource = new CancellationTokenSource();
        var deliveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        DomEventRegistrationHandler? registrationHandler = null;
        host.Module.InvocationHandlers["addDotNetReferenceEventListener"] =
            async (args, cancellationToken) =>
            {
                registrationHandler = Assert.IsType<
                    DotNetObjectReference<DomEventRegistrationHandler>>(args![4]).Value;
                deliveryStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            };

        var operation =
            host.Runtime.AddReferenceEventListenerAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "dataavailable",
                factory,
                s_eventTransport,
                _ => Task.CompletedTask,
                cancellationSource.Token).AsTask();
        await deliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        var lateRegistration = new FakeJSObjectReference();
        var accepted =
            await Assert.IsType<DomEventRegistrationHandler>(registrationHandler)
                .ReceiveRegistrationAsync(lateRegistration);
        Assert.False(accepted);
        Assert.Single(
            lateRegistration.Invocations,
            invocation => invocation.Identifier == "dispose");
        Assert.Equal(1, lateRegistration.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JSON_event_cancellation_during_delivery_rejects_late_listener_ID(
        bool wasm)
    {
        var host = CreateHost(wasm);
        using var cancellationSource = new CancellationTokenSource();
        var deliveryStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        DomEventIdRegistrationHandler? registrationHandler = null;
        host.Module.InvocationHandlers["addDotNetEventListener"] =
            async (args, cancellationToken) =>
            {
                registrationHandler = Assert.IsType<
                    DotNetObjectReference<DomEventIdRegistrationHandler>>(args![4]).Value;
                deliveryStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            };

        var operation = host.Runtime.AddEventListenerAsync(
            new FakeJSObjectReference(),
            "click",
            _ => Task.CompletedTask,
            cancellationSource.Token).AsTask();
        await deliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        var accepted =
            await Assert.IsType<DomEventIdRegistrationHandler>(registrationHandler)
                .ReceiveRegistrationAsync(73);
        Assert.False(accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typed_event_cancellation_after_delivery_rolls_back_registration(
        bool wasm)
    {
        var host = CreateHost(wasm);
        var factory = CreateFactory(host.Runtime);
        var registration = new FakeJSObjectReference();
        using var cancellationSource = new CancellationTokenSource();
        host.Module.InvocationHandlers["addDotNetReferenceEventListener"] =
            async (args, _) =>
            {
                var registrationHandler = Assert.IsType<
                    DotNetObjectReference<DomEventRegistrationHandler>>(args![4]);
                await registrationHandler.Value.ReceiveRegistrationAsync(registration);
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.Runtime.AddReferenceEventListenerAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "dataavailable",
                factory,
                s_eventTransport,
                _ => Task.CompletedTask,
                cancellationSource.Token).AsTask());

        Assert.Single(
            registration.Invocations,
            invocation => invocation.Identifier == "dispose");
        Assert.Equal(1, registration.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JSON_event_cancellation_after_delivery_rolls_back_listener_ID(
        bool wasm)
    {
        var host = CreateHost(wasm);
        using var cancellationSource = new CancellationTokenSource();
        host.Module.InvocationHandlers["addDotNetEventListener"] =
            async (args, _) =>
            {
                var registrationHandler = Assert.IsType<
                    DotNetObjectReference<DomEventIdRegistrationHandler>>(args![4]);
                await registrationHandler.Value.ReceiveRegistrationAsync(73);
                cancellationSource.Cancel();
                throw new OperationCanceledException(cancellationSource.Token);
            };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.Runtime.AddEventListenerAsync(
                new FakeJSObjectReference(),
                "click",
                _ => Task.CompletedTask,
                cancellationSource.Token).AsTask());

        var removal = Assert.Single(
            host.Module.Invocations,
            invocation => invocation.Identifier == "removeDotNetEventListener");
        Assert.Equal(73, removal.Args![0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Event_cancellation_after_commit_preserves_subscription_ownership(
        bool wasm)
    {
        var jsonHost = CreateHost(wasm);
        jsonHost.Module.ReturnValues["addDotNetEventListener"] = 81;
        using var jsonCancellationSource = new CancellationTokenSource();
        var jsonSubscription = await jsonHost.Runtime.AddEventListenerAsync(
            new FakeJSObjectReference(),
            "click",
            _ => Task.CompletedTask,
            jsonCancellationSource.Token);

        var typedHost = CreateHost(wasm);
        var factory = CreateFactory(typedHost.Runtime);
        var registration = new FakeJSObjectReference();
        typedHost.Module.InvocationHandlers["addDotNetReferenceEventListener"] =
            async (args, _) =>
            {
                var handler = Assert.IsType<
                    DotNetObjectReference<DomEventRegistrationHandler>>(args![4]);
                Assert.True(
                    await handler.Value.ReceiveRegistrationAsync(registration));
                return null;
            };
        using var typedCancellationSource = new CancellationTokenSource();
        var typedSubscription =
            await typedHost.Runtime.AddReferenceEventListenerAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "dataavailable",
                factory,
                s_eventTransport,
                _ => Task.CompletedTask,
                typedCancellationSource.Token);

        jsonCancellationSource.Cancel();
        typedCancellationSource.Cancel();
        Assert.DoesNotContain(
            jsonHost.Module.Invocations,
            invocation => invocation.Identifier == "removeDotNetEventListener");
        Assert.Equal(0, registration.DisposeCallCount);

        await jsonSubscription.DisposeAsync();
        await typedSubscription.DisposeAsync();
        Assert.Single(
            jsonHost.Module.Invocations,
            invocation => invocation.Identifier == "removeDotNetEventListener");
        Assert.Equal(1, registration.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Blob_stream_forwards_maximum_and_disposes_stream_and_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var content = new TrackingStream([1, 2, 3, 4]);
        var streamReference = new FakeJSStreamReference(4, content);
        host.Module.InvocationHandlers["createDotNetStreamReference"] =
            DeliverStream(streamReference, handlerIndex: 1);
        using var cancellationSource = new CancellationTokenSource();

        var stream = await host.Runtime.OpenReadStreamAsync(
            new FakeJSObjectReference(),
            s_blobTransport,
            4,
            cancellationSource.Token);

        Assert.Equal(4, stream.Length);
        Assert.Same(content, stream.Stream);
        Assert.Equal(4, streamReference.MaximumLength);
        Assert.Equal(cancellationSource.Token, streamReference.CancellationToken);
        await stream.DisposeAsync();
        await stream.DisposeAsync();
        Assert.True(content.IsDisposed);
        Assert.Equal(1, streamReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false, "Blob")]
    [InlineData(false, "ArrayBuffer")]
    [InlineData(false, "Uint8Array<ArrayBuffer>")]
    [InlineData(true, "Blob")]
    [InlineData(true, "ArrayBuffer")]
    [InlineData(true, "Uint8Array<ArrayBuffer>")]
    public async Task Empty_binary_value_uses_owned_stream_without_JS_stream_reference(
        bool wasm,
        string sourceType)
    {
        var host = CreateHost(wasm);
        host.Module.InvocationHandlers["createDotNetStreamReference"] =
            async (args, _) =>
            {
                var callback = Assert.IsType<
                    DotNetObjectReference<DomStreamCallbackHandler>>(args![1]);
                await callback.Value.ReceiveStreamAsync(null, 0, hasValue: true);
                return null;
            };

        var stream = await host.Runtime.OpenReadStreamAsync(
            new FakeJSObjectReference(),
            sourceType == "Blob"
                ? s_blobTransport
                : DomTransportDescriptor.Binary(sourceType),
            0);

        Assert.Equal(0, stream.Length);
        Assert.Equal(-1, stream.Stream.ReadByte());
        await stream.DisposeAsync();
        await stream.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => stream.Stream.ReadByte());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArrayBuffer_method_result_uses_stream_reference_path(bool wasm)
    {
        var host = CreateHost(wasm);
        var streamReference = new FakeJSStreamReference(
            3,
            new TrackingStream([1, 2, 3]));
        host.Module.InvocationHandlers["invokeMethodDotNetStreamReference"] =
            DeliverStream(streamReference, handlerIndex: 3);

        await using var stream = await host.Runtime.InvokeMethodStreamAsync(
            new FakeJSObjectReference(),
            "arrayBuffer",
            ["argument"],
            DomTransportDescriptor.Binary("ArrayBuffer"),
            10);

        var invocation = Assert.Single(
            host.Module.Invocations,
            item => item.Identifier == "invokeMethodDotNetStreamReference");
        Assert.Equal("arrayBuffer", invocation.Args![1]);
        Assert.Equal(10, streamReference.MaximumLength);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nullable_binary_result_distinguishes_null_from_empty(bool wasm)
    {
        var host = CreateHost(wasm);
        var transport = DomTransportDescriptor.Binary(
            "ArrayBuffer | null",
            nullable: true);
        host.Module.InvocationHandlers["invokeMethodDotNetStreamReference"] =
            async (args, _) =>
            {
                var callback = Assert.IsType<
                    DotNetObjectReference<DomStreamCallbackHandler>>(args![3]);
                await callback.Value.ReceiveStreamAsync(
                    null,
                    0,
                    hasValue: false);
                return null;
            };

        var nullResult = await host.Runtime.InvokeMethodNullableStreamAsync(
            new FakeJSObjectReference(),
            "getKey",
            null,
            transport,
            10);

        Assert.Null(nullResult);

        host.Module.InvocationHandlers["invokeMethodDotNetStreamReference"] =
            async (args, _) =>
            {
                var callback = Assert.IsType<
                    DotNetObjectReference<DomStreamCallbackHandler>>(args![3]);
                await callback.Value.ReceiveStreamAsync(
                    null,
                    0,
                    hasValue: true);
                return null;
            };

        await using var emptyResult =
            await host.Runtime.InvokeMethodNullableStreamAsync(
                new FakeJSObjectReference(),
                "getKey",
                null,
                transport,
                10);

        Assert.NotNull(emptyResult);
        Assert.Equal(0, emptyResult.Length);
        Assert.Equal(-1, emptyResult.Stream.ReadByte());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Null_nonnullable_stream_result_fails_precisely(bool wasm)
    {
        var host = CreateHost(wasm);
        host.Module.InvocationHandlers["invokeMethodDotNetStreamReference"] =
            async (args, _) =>
            {
                var callback = Assert.IsType<
                    DotNetObjectReference<DomStreamCallbackHandler>>(args![3]);
                await callback.Value.ReceiveStreamAsync(
                    null,
                    0,
                    hasValue: false);
                return null;
            };

        var exception = await Assert.ThrowsAsync<DomTransportException>(
            () => host.Runtime.InvokeMethodStreamAsync(
                new FakeJSObjectReference(),
                "arrayBuffer",
                null,
                DomTransportDescriptor.Binary("ArrayBuffer"),
                10).AsTask());

        Assert.Contains("non-nullable stream", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stream_maximum_failure_releases_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var streamReference = new FakeJSStreamReference(
            8,
            new TrackingStream(new byte[8]));
        host.Module.InvocationHandlers["createDotNetStreamReference"] =
            DeliverStream(streamReference, handlerIndex: 1);

        await Assert.ThrowsAsync<IOException>(
            () => host.Runtime.OpenReadStreamAsync(
                new FakeJSObjectReference(),
                s_blobTransport,
                4).AsTask());

        Assert.Equal(4, streamReference.MaximumLength);
        Assert.Equal(1, streamReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nonstreamable_binary_descriptor_fails_without_stream_invocation(bool wasm)
    {
        var host = CreateHost(wasm);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => host.Runtime.OpenReadStreamAsync(
                new FakeJSObjectReference(),
                DomTransportDescriptor.Binary(
                    "SharedArrayBuffer",
                    streamable: false),
                1).AsTask());

        Assert.Contains("not streamable", exception.Message);
        Assert.DoesNotContain(
            host.Module.Invocations,
            invocation => invocation.Identifier == "createDotNetStreamReference");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stream_cancellation_releases_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        using var cancellationSource = new CancellationTokenSource();
        var streamReference = new FakeJSStreamReference(
            1,
            new TrackingStream([1]));
        host.Module.InvocationHandlers["createDotNetStreamReference"] =
            async (args, _) =>
            {
                var callback = Assert.IsType<
                    DotNetObjectReference<DomStreamCallbackHandler>>(args![1]);
                await callback.Value.ReceiveStreamAsync(
                    streamReference,
                    streamReference.Length,
                    hasValue: true);
                cancellationSource.Cancel();
                return null;
            };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.Runtime.OpenReadStreamAsync(
                new FakeJSObjectReference(),
                s_blobTransport,
                1,
                cancellationSource.Token).AsTask());

        Assert.Equal(1, streamReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stream_registration_failure_releases_delivered_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var streamReference = new FakeJSStreamReference(
            1,
            new TrackingStream([1]));
        host.Module.InvocationHandlers["createDotNetStreamReference"] =
            async (args, _) =>
            {
                var callback = Assert.IsType<
                    DotNetObjectReference<DomStreamCallbackHandler>>(args![1]);
                await callback.Value.ReceiveStreamAsync(
                    streamReference,
                    streamReference.Length,
                    hasValue: true);
                throw new JSException("stream registration failed");
            };

        await Assert.ThrowsAsync<JSException>(
            () => host.Runtime.OpenReadStreamAsync(
                new FakeJSObjectReference(),
                s_blobTransport,
                1).AsTask());

        Assert.Equal(1, streamReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stream_callback_rejects_and_releases_late_reference(bool wasm)
    {
        var host = CreateHost(wasm);
        var firstReference = new FakeJSStreamReference(
            1,
            new TrackingStream([1]));
        DomStreamCallbackHandler? handler = null;
        host.Module.InvocationHandlers["createDotNetStreamReference"] =
            async (args, _) =>
            {
                var callback = Assert.IsType<
                    DotNetObjectReference<DomStreamCallbackHandler>>(args![1]);
                handler = callback.Value;
                await handler.ReceiveStreamAsync(
                    firstReference,
                    firstReference.Length,
                    hasValue: true);
                return null;
            };

        await using var stream = await host.Runtime.OpenReadStreamAsync(
            new FakeJSObjectReference(),
            s_blobTransport,
            1);
        var lateReference = new FakeJSStreamReference(
            1,
            new TrackingStream([2]));

        var accepted = await Assert.IsType<DomStreamCallbackHandler>(handler)
            .ReceiveStreamAsync(
                lateReference,
                lateReference.Length,
                hasValue: true);

        Assert.False(accepted);
        Assert.Equal(1, lateReference.DisposeCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Typed_callback_preserves_prerender_exception(bool wasm)
    {
        IDomRuntime runtime;
        if (wasm)
        {
            var jsRuntime = new FakeJSInProcessRuntime();
            jsRuntime.QueueImportFailure(
                new InvalidOperationException(
                    "JavaScript interop calls cannot be issued during prerendering."));
            runtime = new WasmDomRuntime(jsRuntime);
        }
        else
        {
            var jsRuntime = new FakeJSRuntime();
            jsRuntime.QueueImportFailure(
                new InvalidOperationException(
                    "JavaScript interop calls cannot be issued during prerendering."));
            runtime = new ServerDomRuntime(jsRuntime);
        }
        var factory = CreateFactory(runtime);

        await Assert.ThrowsAsync<DomJSException>(
            () => runtime.InvokeMethodReferenceCallbackAsync<FixtureBlobProxy>(
                new FakeJSObjectReference(),
                "toBlob",
                0,
                null,
                factory,
                s_blobTransport,
                _ => Task.CompletedTask).AsTask());
    }

    private static RuntimeHost CreateHost(bool wasm)
    {
        if (wasm)
        {
            var module = new FakeJSInProcessObjectReference();
            return new RuntimeHost(
                new WasmDomRuntime(new FakeJSInProcessRuntime(module)),
                module);
        }

        var serverModule = new FakeJSObjectReference();
        return new RuntimeHost(
            new ServerDomRuntime(new FakeJSRuntime(serverModule)),
            serverModule);
    }

    private static async Task<SerializingRuntimeHost> CreateSerializingHostAsync()
    {
        var jsRuntime = new SerializingJSRuntime();
        var target = await jsRuntime.InvokeAsync<IJSObjectReference>(
            "test.createObjectReference",
            []);
        jsRuntime.Invocations.Clear();
        return new SerializingRuntimeHost(
            new ServerDomRuntime(jsRuntime),
            jsRuntime,
            target);
    }

    private static ValueTask DispatchJsonPayloadAsync(
        IDomRuntime runtime,
        IJSObjectReference target,
        JsonPayloadDestination destination,
        object value) =>
        destination switch
        {
            JsonPayloadDestination.Method => runtime.InvokeMethodVoidAsync(
                target,
                "boundary",
                [value]),
            JsonPayloadDestination.Property => runtime.SetPropertyAsync(
                target,
                "boundary",
                value),
            JsonPayloadDestination.Index => runtime.SetIndexAsync(
                target,
                7,
                value),
            _ => throw new ArgumentOutOfRangeException(nameof(destination)),
        };

    private static string GetPayloadIdentifier(JsonPayloadDestination destination) =>
        destination switch
        {
            JsonPayloadDestination.Method => "invokeMethod",
            JsonPayloadDestination.Property => "setProperty",
            JsonPayloadDestination.Index => "setIndex",
            _ => throw new ArgumentOutOfRangeException(nameof(destination)),
        };

    private static DomProxyFactory CreateFactory(IDomRuntime runtime)
    {
        var factory = new DomProxyFactory(runtime);
        factory.Register<FixtureBlobProxy>(
            (reference, registeredRuntime, registeredFactory) =>
                new FixtureBlobProxy(
                    reference,
                    registeredRuntime,
                    registeredFactory));
        return factory;
    }

    private static Func<object?[]?, CancellationToken, ValueTask<object?>>
        DeliverResultReference(
            IJSObjectReference? reference,
            int handlerIndex) =>
        async (args, _) =>
        {
            var handler = Assert.IsType<
                DotNetObjectReference<DomReferenceDeliveryHandler>>(
                args![handlerIndex]);
            Assert.True(
                await handler.Value.ReceiveReferenceAsync(reference));
            return null;
        };

    private static Func<object?[]?, CancellationToken, ValueTask<object?>>
        DeliverReference(
            IJSObjectReference reference,
            int handlerIndex = 4) =>
        async (args, _) =>
        {
            var handler = Assert.IsType<
                DotNetObjectReference<DomReferenceCallbackHandler<FixtureBlobProxy>>>(
                args![handlerIndex]);
            await handler.Value.HandleReferenceAsync(reference);
            return null;
        };

    private static Func<object?[]?, CancellationToken, ValueTask<object?>>
        DeliverStream(IJSStreamReference reference, int handlerIndex) =>
        async (args, _) =>
        {
            var handler = Assert.IsType<
                DotNetObjectReference<DomStreamCallbackHandler>>(
                args![handlerIndex]);
            await handler.Value.ReceiveStreamAsync(
                reference,
                reference.Length,
                hasValue: true);
            return null;
        };

    private static Dictionary<string, object?> CreateNestedJson(int containerCount)
    {
        Assert.True(containerCount > 0);
        var root = new Dictionary<string, object?>();
        var current = root;
        for (var depth = 1; depth < containerCount; depth++)
        {
            var child = new Dictionary<string, object?>();
            current["child"] = child;
            current = child;
        }
        current["value"] = true;
        return root;
    }

    private static FixtureNestedDto CreateNestedDto(int containerCount)
    {
        Assert.True(containerCount > 0);
        var current = new FixtureNestedDto(null);
        for (var depth = 1; depth < containerCount; depth++)
        {
            current = new FixtureNestedDto(current);
        }
        return current;
    }

    private sealed record RuntimeHost(
        IDomRuntime Runtime,
        IConfigurableJSObjectReference Module);

    private sealed record SerializingRuntimeHost(
        IDomRuntime Runtime,
        SerializingJSRuntime JSRuntime,
        IJSObjectReference Target);

    public enum JsonPayloadDestination
    {
        Method,
        Property,
        Index,
    }

    [DomJsonValue]
    private sealed record FixtureDto(string Name);

    [DomJsonValue]
    private sealed record FixtureNestedDto(object? Child);

    private sealed record UnreviewedDto(string Name);

    private sealed class DestructiveEnumerable(
        IEnumerable<object?> values) : IEnumerable<object?>
    {
        private int _enumerationCount;

        public int EnumerationCount => Volatile.Read(ref _enumerationCount);

        public IEnumerator<object?> GetEnumerator()
        {
            if (Interlocked.Increment(ref _enumerationCount) != 1)
            {
                throw new InvalidOperationException(
                    "This sequence can only be enumerated once.");
            }
            return values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    private sealed class FixtureBlobProxy(
        IJSObjectReference reference,
        IDomRuntime runtime,
        IDomProxyFactory factory)
        : DomProxyBase(reference, runtime, factory)
    {
        public ValueTask<long> GetSizeAsync(CancellationToken cancellationToken = default) =>
            Runtime.GetPropertyAsync<long>(Reference, "size", cancellationToken);
    }

    private readonly struct FixtureUnion : IDomUnionValue
    {
        private readonly object? _value;
        private readonly DomTransportDescriptor? _transport;

        private FixtureUnion(
            int armIndex,
            object? value,
            DomTransportDescriptor transport)
        {
            ArmIndex = armIndex;
            _value = value;
            _transport = transport;
        }

        public int ArmIndex { get; }

        public DomTransportDescriptor SelectedTransport =>
            _transport ?? DomTransportDescriptor.Unsupported(
                "uninitialized union",
                "No arm selected.");

        public static FixtureUnion FromJson(string value) =>
            new(1, value, DomTransportDescriptor.JsonValue("string"));

        public static FixtureUnion FromReference(IJSObjectReference value) =>
            new(2, value, DomTransportDescriptor.JsReference("Blob"));

        public static FixtureUnion FromNull() =>
            new(3, null, DomTransportDescriptor.JsonValue("null", nullable: true));

        public static FixtureUnion WithWrongReferenceValue(object value) =>
            new(2, value, DomTransportDescriptor.JsReference("Blob"));
    }
}
