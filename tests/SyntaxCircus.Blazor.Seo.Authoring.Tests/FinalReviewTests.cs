using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace SyntaxCircus.Blazor.Seo.Authoring.Tests;

public sealed class FinalReviewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class Selector(Func<CancellationToken, SeoAuthoringProvider?> select) : ISeoProviderSelector
    {
        public ValueTask<SeoAuthoringProvider?> SelectAsync(CancellationToken ct) => ValueTask.FromResult(select(ct));
    }

    private static async Task<SeoAuthoringPanelViewModel> CompleteWith(IServiceCollection services)
    {
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var presentation = scope.ServiceProvider.GetRequiredService<ISeoAuthoringPresentation>();
        var prepared = await presentation.PrepareAsync(TestDrafts.Draft() with { MetaDescription = null }, Token);
        return await presentation.CompleteAsync(prepared, Token);
    }

    private static void ShouldBeCoreOnlyWithBasicChecks(SeoAuthoringPanelViewModel view)
    {
        view.ExtraPending.ShouldBeFalse();
        view.Status.ShouldContain("turned off");
        (view.Search.Length + view.Writing.Length).ShouldBeGreaterThan(0, "the basic checks must still run");
    }

    private static IServiceCollection Base(bool withJs = true)
    {
        var services = new ServiceCollection().AddLogging();
        if (withJs)
        {
            services.AddSingleton(NSubstitute.Substitute.For<Microsoft.JSInterop.IJSRuntime>());
        }

        return services;
    }

    [Fact]
    public async Task Selection_ThrowingSelector_FallsBackToCoreOnly()
    {
        var services = Base();
        services.AddSingleton<ISeoProviderSelector>(new Selector(_ => throw new InvalidOperationException("down")));
        services.AddSeoAuthoring();

        ShouldBeCoreOnlyWithBasicChecks(await CompleteWith(services));
    }

    [Fact]
    public async Task Selection_SelectorThrowingNonCallerCancellation_FallsBackToCoreOnly()
    {
        var services = Base();
        services.AddSingleton<ISeoProviderSelector>(new Selector(_ => throw new TaskCanceledException("http timeout")));
        services.AddSeoAuthoring();

        ShouldBeCoreOnlyWithBasicChecks(await CompleteWith(services));
    }

    [Fact]
    public async Task Selection_YoastWithoutBaseUrl_FallsBackToCoreOnly()
    {
        var services = Base();
        services.AddSingleton<ISeoProviderSelector>(new Selector(_ => SeoAuthoringProvider.Yoast));
        services.AddSeoAuthoring();

        ShouldBeCoreOnlyWithBasicChecks(await CompleteWith(services));
    }

    [Fact]
    public async Task Selection_PowerSeoWithoutJsRuntime_FallsBackToCoreOnly()
    {
        var services = Base(withJs: false);
        services.AddSeoAuthoring(o => o.Provider = SeoAuthoringProvider.PowerSeo);

        ShouldBeCoreOnlyWithBasicChecks(await CompleteWith(services));
    }

    [Fact]
    public async Task Selection_CallerCancellation_StillPropagates()
    {
        using var cts = new CancellationTokenSource();
        var services = Base();
        services.AddSingleton<ISeoProviderSelector>(new Selector(ct =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return SeoAuthoringProvider.PowerSeo;
        }));
        services.AddSeoAuthoring();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var presentation = scope.ServiceProvider.GetRequiredService<ISeoAuthoringPresentation>();
        var prepared = await presentation.PrepareAsync(TestDrafts.Draft(), Token);

        await Should.ThrowAsync<OperationCanceledException>(async () => await presentation.CompleteAsync(prepared, cts.Token));
    }

    [Fact]
    public async Task Draft_WithoutCanonicalUrl_HasNoSnippetAddress_AndEmptyAnalyzerCanonical()
    {
        var draft = TestDrafts.Draft() with { CanonicalUrl = null };

        new DefaultSeoDraftDocumentBuilder().Build(draft).CanonicalUrl.ShouldBe("");
        var prepared = await TestDrafts.Presentation(null).PrepareAsync(draft, Token);
        prepared.Snippet.Address.ShouldBeNull();
        prepared.Snapshot.ShouldNotBeNull().CanonicalUrl.ShouldBe("");
    }
}
