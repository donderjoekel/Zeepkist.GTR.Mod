using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.UI.Tournaments;

public class TournamentsWindowState
{
    private readonly TournamentGraphqlService _graphqlService;
    private readonly ILogger<TournamentsWindowState> _logger;
    private CancellationTokenSource _fetchCts;

    public bool IsOpen { get; private set; }
    public bool IsLoading { get; private set; }
    public string Error { get; private set; }
    public IReadOnlyList<TournamentViewModel> Tournaments { get; private set; } = Array.Empty<TournamentViewModel>();
    public int? SelectedId { get; private set; }

    public TournamentViewModel SelectedTournament
    {
        get
        {
            if (SelectedId == null)
                return null;

            foreach (TournamentViewModel tournament in Tournaments)
            {
                if (tournament.Id == SelectedId.Value)
                    return tournament;
            }

            return null;
        }
    }

    public TournamentsWindowState(
        TournamentGraphqlService graphqlService,
        ILogger<TournamentsWindowState> logger)
    {
        _graphqlService = graphqlService;
        _logger = logger;
    }

    public void Open()
    {
        IsOpen = true;
        Refresh();
    }

    public void Close()
    {
        IsOpen = false;
        CancelFetch();
    }

    public void Select(int id)
    {
        SelectedId = id;
    }

    public void Refresh()
    {
        FetchAsync().Forget();
    }

    private async UniTaskVoid FetchAsync()
    {
        CancelFetch();
        _fetchCts = new CancellationTokenSource();
        CancellationToken ct = _fetchCts.Token;

        IsLoading = true;
        Error = null;

        try
        {
            Result<IReadOnlyList<TournamentViewModel>> result =
                await _graphqlService.GetActiveTournaments(ct);
            if (ct.IsCancellationRequested)
                return;

            if (result.IsFailed)
            {
                Error = result.Errors.Count > 0
                    ? result.Errors[0].Message
                    : "Failed to load tournaments";
                Tournaments = Array.Empty<TournamentViewModel>();
                SelectedId = null;
                return;
            }

            Tournaments = result.Value ?? Array.Empty<TournamentViewModel>();
            if (SelectedId != null && SelectedTournament == null)
                SelectedId = null;
            if (SelectedId == null && Tournaments.Count > 0)
                SelectedId = Tournaments[0].Id;
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unexpected error loading tournaments");
            Error = e.Message;
            Tournaments = Array.Empty<TournamentViewModel>();
            SelectedId = null;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private void CancelFetch()
    {
        if (_fetchCts == null)
            return;

        _fetchCts.Cancel();
        _fetchCts.Dispose();
        _fetchCts = null;
    }
}
