namespace Blazor.DOM.WakeLock.CompilationTests;

internal interface INullableGenericValueTypeOverloads
{
    void AcceptTask(ValueTask<double> value);
    void AcceptTask(ValueTask<double>? value);
    void AcceptMemory(Memory<byte> value);
    void AcceptMemory(Memory<byte>? value);
}
