namespace Blazor.ExampleConsumer.EndToEndTests;

[CollectionDefinition(Name)]
public sealed class ExampleSiteCollection :
    ICollectionFixture<BlazoratorsSiteFixture>,
    ICollectionFixture<BrowserFixture>
{
    public const string Name = "Blazor example site";
}
