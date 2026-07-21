namespace Blazor.ExampleConsumer.EndToEndTests;

[CollectionDefinition(Name)]
public sealed class DomSiteCollection :
    ICollectionFixture<BlazoratorsSiteFixture>,
    ICollectionFixture<BlazorServerSiteFixture>,
    ICollectionFixture<BrowserFixture>
{
    public const string Name = "DOM host sites";
}
