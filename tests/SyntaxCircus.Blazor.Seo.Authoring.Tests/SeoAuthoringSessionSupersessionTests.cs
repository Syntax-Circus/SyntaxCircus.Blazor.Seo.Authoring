using SyntaxCircus.Seo.Analysis;
using Shouldly;
using Xunit;

namespace SyntaxCircus.Blazor.Seo.Authoring.Tests;

public sealed class SeoAuthoringSessionSupersessionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Session_TwoConcurrentChecks_OnlyOneRunSurvives()
    {
        var presenter = new GatedPresenter();
        await using var session = new SeoAuthoringSession(presenter);
        var rendezvous = new Barrier(2);
        var raised = 0;
        // Both callers are held inside their first Changed notification, so any gap between "supersede the previous check"
        // and "register this one" is guaranteed to be crossed by both threads at once.
        session.Changed += () =>
        {
            if (Interlocked.Increment(ref raised) <= 2)
            {
                rendezvous.SignalAndWait(TimeSpan.FromSeconds(5));
            }
        };
        var first = TestDrafts.Draft() with { Title = "First" };
        var second = TestDrafts.Draft() with { Title = "Second" };
        Task? a = null, b = null;

        await Task.WhenAll(
            Task.Run(() => { a = session.CheckAsync(first, true, Token); }, Token),
            Task.Run(() => { b = session.CheckAsync(second, true, Token); }, Token));
        presenter.Gate.SetResult();
        await Task.WhenAll(a!, b!);

        presenter.Finished.Count.ShouldBe(1);
        session.View!.Snippet.Title.ShouldStartWith(presenter.Finished.Single());
    }

    [Fact]
    public async Task Session_WithoutCompanion_AnalyzesOncePerCheckAndRaisesChangedOnce()
    {
        var analyzer = new CountingAnalyzer();
        var presentation = new SeoAuthoringPresentation(new DefaultSeoDraftDocumentBuilder(), analyzer, null);
        await using var session = new SeoAuthoringSession(presentation);
        var published = 0;
        await session.CheckAsync(TestDrafts.Draft(), true, Token);
        session.Changed += () => published++;

        analyzer.Calls = 0;
        await session.CheckAsync(TestDrafts.Draft(), true, Token);

        analyzer.Calls.ShouldBe(1);
        // One notification for superseding the previous check, one for the single published result.
        published.ShouldBe(2);
    }

    [Fact]
    public async Task Session_ThrowingChangedHandler_DoesNotHangDispose()
    {
        var session = new SeoAuthoringSession(TestDrafts.Presentation(null));
        session.Changed += () => throw new InvalidOperationException("subscriber failed");

        try
        {
            await session.CheckAsync(TestDrafts.Draft(), true, Token);
        }
        catch (InvalidOperationException)
        {
        }

        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), Token);
    }

    [Fact]
    public async Task Session_AfterAThrowingChangedHandler_LaterChecksStillWork()
    {
        await using var session = new SeoAuthoringSession(TestDrafts.Presentation(null));
        Action thrower = () => throw new InvalidOperationException("subscriber failed");
        session.Changed += thrower;
        try
        {
            await session.CheckAsync(TestDrafts.Draft(), true, Token);
        }
        catch (InvalidOperationException)
        {
        }

        session.Changed -= thrower;
        await session.CheckAsync(TestDrafts.Draft(), true, Token).WaitAsync(TimeSpan.FromSeconds(5), Token);

        session.View.ShouldNotBeNull();
    }

    private sealed class CountingAnalyzer : IContentSeoAnalyzer
    {
        private readonly ContentSeoAnalyzer _inner = new();
        public int Calls;

        public SeoSnapshot Prepare(SeoDocumentInput input, CancellationToken cancellationToken = default) => _inner.Prepare(input, cancellationToken);

        public Task<SeoAnalysisReport> AnalyzeAsync(SeoSnapshot snapshot, ISeoCompanion? companion = null, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);
            return _inner.AnalyzeAsync(snapshot, companion, cancellationToken);
        }
    }

    private sealed class GatedPresenter : ISeoAuthoringPresentation
    {
        private readonly SeoAuthoringPresentation _inner = TestDrafts.Presentation(null);
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Finished { get; } = [];

        public Task<SeoPreparedDraft> PrepareAsync(SeoAuthoringDraft draft, CancellationToken token) => _inner.PrepareAsync(draft, token);

        public Task<SeoAuthoringPanelViewModel> BaselineAsync(SeoPreparedDraft draft, CancellationToken token) =>
            Task.FromResult(View(draft, pending: true));

        public async Task<SeoAuthoringPanelViewModel> CompleteAsync(SeoPreparedDraft draft, CancellationToken token)
        {
            await Gate.Task.WaitAsync(token);
            lock (Finished)
            {
                Finished.Add(draft.Snippet.Title.Split(' ')[0]);
            }

            return View(draft, pending: false);
        }

        private static SeoAuthoringPanelViewModel View(SeoPreparedDraft draft, bool pending) =>
            new("id", draft.Snippet, [], [], [], pending ? "Checking" : "Ready", pending, false);
    }
}
