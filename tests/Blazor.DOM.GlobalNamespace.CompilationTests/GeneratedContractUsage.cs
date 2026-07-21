using Blazor.DOM.Namespaces.CSS;
using Blazor.DOM.Namespaces.WebAssembly;

namespace Blazor.DOM.GlobalNamespace.CompilationTests;

internal static class GeneratedContractUsage
{
    internal static void Verify(
        IWindow window,
        ICSSNamespace css,
        IWebAssemblyNamespace webAssembly,
        IModuleFactory moduleFactory,
        IModule module,
        BufferSource bytes)
    {
        _ = window.WakeLockConstructor.Create();
        _ = css.Supports("display", "grid");
        _ = css.Supports("(display: grid)");
        _ = css.Px(1);
        _ = webAssembly.Validate(bytes);
        _ = webAssembly.CompileAsync(bytes);
        _ = webAssembly.InstantiateAsync(module);
        _ = moduleFactory.Create(bytes);
    }
}
