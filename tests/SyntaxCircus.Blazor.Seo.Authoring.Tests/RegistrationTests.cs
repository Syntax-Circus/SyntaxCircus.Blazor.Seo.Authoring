using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SyntaxCircus.Blazor.Seo.Analysis.PowerSeo;
using SyntaxCircus.Seo.Analysis.Yoast;
using Xunit;

namespace SyntaxCircus.Blazor.Seo.Authoring.Tests;

public sealed class RegistrationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Selector(SeoAuthoringProvider? result) : ISeoProviderSelector
    {
        public int Calls;

        public ValueTask<SeoAuthoringProvider?> SelectAsync(CancellationToken ct)
        {
            Calls++;
            return ValueTask.FromResult(result);
        }
    }

    private static async Task<SeoAuthoringPanelViewModel> Baseline(IServiceCollection services)
    {
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var presentation = scope.ServiceProvider.GetRequiredService<ISeoAuthoringPresentation>();
        var prepared = await presentation.PrepareAsync(TestDrafts.Draft(), Token);
        return await presentation.BaselineAsync(prepared, Token);
    }

    [Fact]
    public async Task Registration_Default_UsesPowerSeo()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        services.AddSeoAuthoring();

        (await Baseline(services)).ExtraPending.ShouldBeTrue();
    }

    [Fact]
    public async Task Registration_YoastWithoutBaseUrl_FallsBackToCoreOnly()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        services.AddSeoAuthoring(o => o.Provider = SeoAuthoringProvider.Yoast);

        var view = await Baseline(services);

        view.ExtraPending.ShouldBeFalse();
        view.Status.ShouldContain("turned off");
        services.ShouldNotContain(d => d.ServiceType == typeof(YoastCompanion));
    }

    [Theory]
    [InlineData("ftp://service/")]
    [InlineData("http://user:secret@service/")]
    [InlineData("http://service/?q=1")]
    [InlineData("relative/")]
    public void Registration_UnsafeYoastBaseUrl_InstallsNoYoastClient(string url)
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        services.AddSeoAuthoring(o =>
        {
            o.Provider = SeoAuthoringProvider.Yoast;
            o.YoastBaseUrl = new Uri(url, UriKind.RelativeOrAbsolute);
        });

        services.ShouldNotContain(d => d.ServiceType == typeof(YoastCompanion));
    }

    [Fact]
    public void Registration_ValidYoastBaseUrl_InstallsYoastCompanionAndPowerSeoSupport()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        services.AddSeoAuthoring(o =>
        {
            o.Provider = SeoAuthoringProvider.Yoast;
            o.YoastBaseUrl = new Uri("http://seo-yoast:8080");
        });

        services.ShouldContain(d => d.ServiceType == typeof(YoastCompanion));
        services.ShouldContain(d => d.ServiceType == typeof(PowerSeoCompanion));
    }

    [Fact]
    public async Task Registration_SelectorReturningNull_UsesCoreOnly()
    {
        var selector = new Selector(null);
        var services = new ServiceCollection().AddLogging().AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        services.AddSingleton<ISeoProviderSelector>(selector);
        services.AddSeoAuthoring();

        var view = await Baseline(services);

        selector.Calls.ShouldBe(1);
        view.ExtraPending.ShouldBeFalse();
        view.Status.ShouldContain("turned off");
    }

    [Fact]
    public async Task Registration_SelectorOverridesConfiguredProvider()
    {
        var services = new ServiceCollection().AddLogging().AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        services.AddSingleton<ISeoProviderSelector>(new Selector(SeoAuthoringProvider.PowerSeo));
        services.AddSeoAuthoring(o => o.Provider = SeoAuthoringProvider.CoreOnly);

        (await Baseline(services)).ExtraPending.ShouldBeTrue();
    }

    [Fact]
    public async Task Registration_ConfigurationSection_BindsProvider()
    {
        var section = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Provider"] = "CoreOnly" }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        services.AddSeoAuthoring(section);

        (await Baseline(services)).Status.ShouldContain("turned off");
    }
}
