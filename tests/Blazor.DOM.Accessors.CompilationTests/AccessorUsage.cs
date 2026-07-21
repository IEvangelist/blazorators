namespace Blazor.DOM.Accessors.CompilationTests;

public interface IAsymmetricAccessorFixture
{
    [global::Microsoft.JSInterop.DomAccessor(
        "value",
        global::Microsoft.JSInterop.DomAccessorOperation.Get,
        global::Microsoft.JSInterop.DomTransportKind.JsonValue,
        "string | null",
        Nullable = true,
        Streamable = false,
        StructuredClone = true)]
    string? Value { get; }

    [global::Microsoft.JSInterop.DomAccessor(
        "value",
        global::Microsoft.JSInterop.DomAccessorOperation.Set,
        global::Microsoft.JSInterop.DomTransportKind.JsonValue,
        "string",
        Nullable = false,
        Streamable = false,
        StructuredClone = true)]
    void SetValue(string value);
}

public static class AccessorUsage
{
    public static void Exercise(
        ISVGNumber number,
        IAsymmetricAccessorFixture asymmetric)
    {
        double value = number.Value;
        number.Value = value + 1;

        string? text = asymmetric.Value;
        asymmetric.SetValue(text ?? "");
    }
}
