using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SyntaxCircus.Blazor.Seo.Analysis.PowerSeo;
using SyntaxCircus.Seo.Analysis;
using SyntaxCircus.Seo.Analysis.Yoast;

namespace SyntaxCircus.Blazor.Seo.Authoring;

public static class SeoAuthoringServiceCollectionExtensions
{
    private const string YoastClientName = "SeoAuthoring.Yoast";

    /// <summary>
    /// Registers the analyzer, the PowerSeo companion, a Yoast companion when a valid
    /// <see cref="SeoAuthoringOptions.YoastBaseUrl"/> is set, the default <see cref="ISeoDraftDocumentBuilder"/> (replaceable)
    /// and a scoped <see cref="ISeoAuthoringPresentation"/>. Register an <see cref="ISeoProviderSelector"/> to choose the
    /// provider at runtime.
    /// </summary>
    public static IServiceCollection AddSeoAuthoring(this IServiceCollection services, Action<SeoAuthoringOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new SeoAuthoringOptions();
        configure?.Invoke(options);
        var yoastBaseUrl = options.ValidYoastBaseUrl();

        services.AddSingleton(options);
        services.TryAddSingleton<IContentSeoAnalyzer, ContentSeoAnalyzer>();
        services.TryAddSingleton<ISeoDraftDocumentBuilder, DefaultSeoDraftDocumentBuilder>();
        services.AddSyntaxCircusPowerSeoAnalysis();
        if (yoastBaseUrl is not null)
        {
            services.AddHttpClient(YoastClientName, client =>
            {
                client.BaseAddress = yoastBaseUrl;
                client.Timeout = SeoAuthoringOptions.HttpTimeout;
            }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            }).RemoveAllLoggers();
            services.AddScoped(p => new YoastCompanion(
                p.GetRequiredService<IHttpClientFactory>().CreateClient(YoastClientName), SeoAuthoringOptions.YoastDeadline));
        }

        services.AddScoped<ISeoAuthoringPresentation>(p => new SeoAuthoringPresentation(
            p.GetRequiredService<ISeoDraftDocumentBuilder>(), p.GetRequiredService<IContentSeoAnalyzer>(), null,
            async token =>
            {
                var selected = options.Provider;
                if (p.GetService<ISeoProviderSelector>() is { } selector)
                {
                    try
                    {
                        selected = await selector.SelectAsync(token) ?? SeoAuthoringProvider.CoreOnly;
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        // Basic analysis stays available if the selection source is down; an uncertain
                        // choice must never send draft text to an external service.
                        return null;
                    }
                }

                return selected switch
                {
                    SeoAuthoringProvider.PowerSeo => p.GetRequiredService<PowerSeoCompanion>(),
                    SeoAuthoringProvider.Yoast when yoastBaseUrl is not null => p.GetRequiredService<YoastCompanion>(),
                    _ => (ISeoCompanion?)null,
                };
            }));
        return services;
    }

    /// <summary>Binds <see cref="SeoAuthoringOptions"/> (<c>Provider</c>, <c>YoastBaseUrl</c>) from a configuration section.</summary>
    public static IServiceCollection AddSeoAuthoring(this IServiceCollection services, IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return services.AddSeoAuthoring(section.Bind);
    }
}
