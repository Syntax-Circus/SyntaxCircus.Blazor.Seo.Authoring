namespace SyntaxCircus.Blazor.Seo.Authoring;

/// <summary>Which extra writing-check companion runs after the always-on basic checks.</summary>
public enum SeoAuthoringProvider { PowerSeo, Yoast, CoreOnly }

public sealed class SeoAuthoringOptions
{
    /// <summary>How long the Yoast companion may take before its result is dropped.</summary>
    public static readonly TimeSpan YoastDeadline = TimeSpan.FromSeconds(6);

    /// <summary>HTTP timeout for the Yoast service client.</summary>
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(10);

    public SeoAuthoringProvider Provider { get; set; } = SeoAuthoringProvider.PowerSeo;

    /// <summary>
    /// Operator-owned origin of the Yoast analysis service. Must be absolute HTTP(S) without credentials, query or
    /// fragment; anything else disables Yoast (the provider falls back to <see cref="SeoAuthoringProvider.CoreOnly"/>).
    /// </summary>
    public Uri? YoastBaseUrl { get; set; }

    internal Uri? ValidYoastBaseUrl() => YoastBaseUrl is
        { IsAbsoluteUri: true, Scheme: "http" or "https", UserInfo.Length: 0, Query.Length: 0, Fragment.Length: 0 } endpoint
        && !string.IsNullOrEmpty(endpoint.Host)
            ? new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/")
            : null;
}
