using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace SyntaxCircus.Blazor.Seo.Authoring.Tests;

public sealed class SeoAuthoringSessionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Session_NewTypingRejectsASlowStaleResult()
    {
        var presenter = new Presenter();
        await using var session = new SeoAuthoringSession(presenter);
        var first = TestDrafts.Draft();
        var old = session.CheckAsync(first, true, Token);
        session.View!.ExtraPending.ShouldBeTrue();
        var second = first with { Identity = first.Identity with { InputGeneration = 2 }, Title = "Newer typing" };
        var current = session.CheckAsync(second, true, Token);
        presenter.Replies[1].SetResult(View("new", second.Title));
        await current;
        presenter.Replies[0].SetResult(View("old", first.Title));
        await old;

        session.View!.Snippet.Title.ShouldBe("Newer typing");
    }

    [Fact]
    public async Task Session_Invalidate_PreventsLateResultsFromReappearing()
    {
        var presenter = new Presenter();
        await using var session = new SeoAuthoringSession(presenter);
        var work = session.CheckAsync(TestDrafts.Draft(), true, Token);
        session.Invalidate(true);
        presenter.Replies[0].SetResult(View("late", "Private title"));
        await work;

        session.View.ShouldBeNull();
    }

    [Fact]
    public async Task Session_InvalidateWithoutClear_MarksViewOutdated()
    {
        var presenter = new Presenter();
        await using var session = new SeoAuthoringSession(presenter);
        var work = session.CheckAsync(TestDrafts.Draft(), true, Token);

        session.Invalidate(false);
        await work;

        session.View!.Outdated.ShouldBeTrue();
        session.View.ExtraPending.ShouldBeFalse();
    }

    [Fact]
    public async Task Session_DebouncesRapidChecks_To750ms()
    {
        var clock = new FakeTimeProvider();
        var presenter = new Presenter();
        await using var session = new SeoAuthoringSession(presenter, clock);
        var draft = TestDrafts.Draft();

        _ = session.CheckAsync(draft, false, Token);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        _ = session.CheckAsync(draft, false, Token);
        clock.Advance(TimeSpan.FromMilliseconds(250));
        var last = session.CheckAsync(draft, false, Token);
        clock.Advance(TimeSpan.FromMilliseconds(749));
        presenter.PrepareCalls.ShouldBe(0);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await presenter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);

        presenter.PrepareCalls.ShouldBe(1);
        presenter.Replies[0].SetResult(View("done", "Current title"));
        await last;
    }

    [Fact]
    public async Task Session_Immediate_BypassesDebounce()
    {
        var clock = new FakeTimeProvider();
        var presenter = new Presenter();
        await using var session = new SeoAuthoringSession(presenter, clock);

        var work = session.CheckAsync(TestDrafts.Draft(), true, Token);
        await presenter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);

        presenter.PrepareCalls.ShouldBe(1);
        presenter.Replies[0].SetResult(View("done", "Current title"));
        await work;
    }

    [Fact]
    public async Task Session_CancelledWaitAndDisposalOfOneEditor_DoNotAffectAnother()
    {
        var presenter = new Presenter();
        var first = new SeoAuthoringSession(presenter);
        await using var second = new SeoAuthoringSession(presenter);
        var a = first.CheckAsync(TestDrafts.Draft(), true, Token);
        var b = second.CheckAsync(TestDrafts.Draft(), true, Token);
        var disposing = first.DisposeAsync().AsTask();
        presenter.Replies[0].SetResult(View("old", "First"));
        await a;
        await disposing;
        presenter.Replies[1].SetResult(View("current", "Second"));
        await b;

        second.View!.Snippet.Title.ShouldBe("Second");
    }

    [Fact]
    public async Task Session_PresenterFailure_RetainsDraftWithUnavailableStatus()
    {
        await using var session = new SeoAuthoringSession(new FailingPresenter());

        await session.CheckAsync(TestDrafts.Draft(), true, Token);

        session.View!.Status.ShouldContain("temporarily unavailable");
        session.View.Snippet.Title.ShouldBe("Current title");
    }

    private static SeoAuthoringPanelViewModel View(string id, string title, bool pending = false)
        => new(id, new SeoSnippetViewModel(title, null, ""), [], [], [], pending ? "Checking" : "Ready", pending, false);

    private sealed class FailingPresenter : ISeoAuthoringPresentation
    {
        public Task<SeoPreparedDraft> PrepareAsync(SeoAuthoringDraft draft, CancellationToken token) => throw new InvalidOperationException("boom");
        public Task<SeoAuthoringPanelViewModel> BaselineAsync(SeoPreparedDraft draft, CancellationToken token) => throw new InvalidOperationException();
        public Task<SeoAuthoringPanelViewModel> CompleteAsync(SeoPreparedDraft draft, CancellationToken token) => throw new InvalidOperationException();
    }

    private sealed class Presenter : ISeoAuthoringPresentation
    {
        private readonly SeoAuthoringPresentation _inner = TestDrafts.Presentation(null);
        private int _prepareCalls;
        public int PrepareCalls => Volatile.Read(ref _prepareCalls);
        public List<TaskCompletionSource<SeoAuthoringPanelViewModel>> Replies = [];
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SeoPreparedDraft> PrepareAsync(SeoAuthoringDraft draft, CancellationToken token)
        {
            Interlocked.Increment(ref _prepareCalls);
            return _inner.PrepareAsync(draft, token);
        }

        public Task<SeoAuthoringPanelViewModel> BaselineAsync(SeoPreparedDraft draft, CancellationToken token)
            => Task.FromResult(View(draft.Snapshot!.Id, draft.Snippet.Title, true));

        public Task<SeoAuthoringPanelViewModel> CompleteAsync(SeoPreparedDraft draft, CancellationToken token)
        {
            var reply = new TaskCompletionSource<SeoAuthoringPanelViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (Replies) Replies.Add(reply);
            Started.TrySetResult();
            return reply.Task;
        }
    }
}
