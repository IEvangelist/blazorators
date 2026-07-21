namespace Microsoft.JSInterop;

[JSAutoInterop(
    TypeName = "Notification",
    Implementation = "window.Notification")]
public partial interface INotificationsService;
