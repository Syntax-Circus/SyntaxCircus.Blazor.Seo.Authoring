namespace SyntaxCircus.Blazor.Seo.Authoring;

/// <summary>
/// Per-editor live-check state. Each <see cref="CheckAsync"/> supersedes the previous one; results from a superseded
/// or invalidated check are never published. Non-immediate checks wait <see cref="DebounceDelay"/> first.
/// </summary>
public sealed class SeoAuthoringSession(ISeoAuthoringPresentation presentation, TimeProvider? clock = null) : IAsyncDisposable
{
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(750);

    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private long _generation;
    private bool _disposed;
    private Task _work = Task.CompletedTask;

    public SeoAuthoringPanelViewModel? View { get; private set; }

    public event Action? Changed;

    public Task CheckAsync(SeoAuthoringDraft draft, bool immediate, CancellationToken token)
    {
        Invalidate(false);
        CancellationTokenSource source;
        long generation;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            source = CancellationTokenSource.CreateLinkedTokenSource(token);
            _pending = source;
            generation = _generation;
        }

        // Started outside the gate so presentation.Prepare and Changed handlers never run while it is held.
        var work = RunAsync(draft, immediate, generation, source);
        lock (_gate)
        {
            _work = work;
        }

        return work;
    }

    public void Invalidate(bool clear)
    {
        CancellationTokenSource? old;
        lock (_gate)
        {
            ++_generation;
            old = _pending;
            _pending = null;
            View = clear
                ? null
                : View is { } view
                    ? view with { Outdated = true, ExtraPending = false, Status = "Draft changed. These results are outdated." }
                    : null;
        }

        try
        {
            old?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        Changed?.Invoke();
    }

    private async Task RunAsync(SeoAuthoringDraft draft, bool immediate, long generation, CancellationTokenSource source)
    {
        using (source)
        {
            try
            {
                var token = source.Token;
                if (!immediate)
                {
                    await Task.Delay(DebounceDelay, clock ?? TimeProvider.System, token);
                }

                token.ThrowIfCancellationRequested();
                var prepared = await presentation.PrepareAsync(draft, token);
                Publish(await presentation.BaselineAsync(prepared, token).WaitAsync(token));
                if (prepared.Snapshot is not null)
                {
                    Publish(await presentation.CompleteAsync(prepared, token).WaitAsync(token));
                }
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                lock (_gate)
                {
                    if (!_disposed && generation == _generation)
                    {
                        View = View is { } view
                            ? view with { ExtraPending = false, Status = "Basic results retained. Extra writing checks are temporarily unavailable." }
                            : new SeoAuthoringPanelViewModel(null, new SeoSnippetViewModel(draft.Title, null, draft.MetaDescription ?? ""),
                                [], [], [], "Checks are temporarily unavailable. Your draft is retained.", false, true);
                    }
                }

                Changed?.Invoke();
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_pending, source))
                    {
                        _pending = null;
                    }
                }
            }
        }

        void Publish(SeoAuthoringPanelViewModel view)
        {
            lock (_gate)
            {
                if (_disposed || generation != _generation || source.IsCancellationRequested)
                {
                    return;
                }

                View = view;
            }

            Changed?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Invalidate(true);
        Changed = null;
        await _work;
    }
}
