using Microsoft.JSInterop;

namespace Blazor.DOM.SupplementalBrowserApis.CompilationTests;

internal static class SupplementalBrowserApisUsage
{
    internal static async Task VerifyAsync(
        INavigator navigator,
        IWindow window,
        IBluetoothRemoteGATTCharacteristic characteristic,
        IUSBDevice usbDevice,
        IHIDDevice hidDevice,
        ISerialPort serialPort,
        IPresentationRequest presentationRequest,
        IGPU gpu,
        IGPUDevice gpuDevice,
        IGPUBuffer gpuBuffer,
        IBluetoothManufacturerDataMap manufacturerData,
        EventListenerOrEventListenerObject listener)
    {
        _ = navigator.Bluetooth;
        _ = navigator.Usb;
        _ = navigator.Hid;
        _ = navigator.Serial;
        _ = navigator.Presentation;
        _ = navigator.Gpu;

        await characteristic.WriteValueWithResponseAsync(default);
        _ = await characteristic.ReadValueAsync();
        await usbDevice.TransferOutAsync(1, default);
        await hidDevice.SendReportAsync(1, default);
        _ = serialPort.Readable;
        _ = serialPort.Writable;

        _ = window.PresentationRequestConstructor.Create("https://example.test/");
        _ = await presentationRequest.StartAsync();
        _ = await gpu.RequestAdapterAsync();
        _ = gpuDevice.Lost;
        _ = gpuBuffer.GetMappedRange();

        gpuDevice.AddEventListener("uncapturederror", listener);
        gpuDevice.RemoveEventListener("uncapturederror", listener);

        _ = manufacturerData.Values();
        _ = gpuDevice.Features.Value;
        _ = window.GPUBufferUsage.MAPREAD;
        _ = window.GPUMapMode.READ;
        _ = window.GPUShaderStage.COMPUTE;
        _ = window.GPUTextureUsage.RENDERATTACHMENT;

        _ = new BluetoothPermissionDescriptor
        {
            AcceptAllDevices = true,
            OptionalManufacturerData = [1],
        };
        _ = new USBPermissionDescriptor
        {
            Filters = [],
        };
    }
}
