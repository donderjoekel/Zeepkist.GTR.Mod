using System;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Connectivity;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Patching.Patches;
using UnityEngine;
using ZeepSDK.Controls;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.Dialogs;

internal sealed class LaLigaCensorshipDialogService : IEagerService, IDisposable
{
    private const string NoticeSeenKey = "TNRD.Zeepkist.GTR.HasSeenLaLigaCensorshipNotice1";

    private readonly SpainRoutingService _spainRoutingService;
    private readonly ConfigService _configService;
    private readonly ILogger<LaLigaCensorshipDialogService> _logger;
    private LaLigaCensorshipDialog _dialog;
    private bool _checking;
    private bool _disposed;

    public LaLigaCensorshipDialogService(
        SpainRoutingService spainRoutingService,
        ConfigService configService,
        ILogger<LaLigaCensorshipDialogService> logger)
    {
        _spainRoutingService = spainRoutingService;
        _configService = configService;
        _logger = logger;
        MainMenuUi_Awake.Postfixed += OnMainMenuAwake;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        MainMenuUi_Awake.Postfixed -= OnMainMenuAwake;
        RemoveDialog();
    }

    private void OnMainMenuAwake()
    {
        if (_disposed || _checking || _dialog != null || HasSeenNotice())
            return;

        _checking = true;
        DetectAndShowAsync().Forget(exception =>
            _logger.LogError(exception, "Failed to show Spain connection dialog"));
    }

    private async UniTask DetectAndShowAsync()
    {
        try
        {
            SpainTraceResult traceResult = await _spainRoutingService.GetTraceResultAsync();
            if (_disposed || HasSeenNotice() || !traceResult.IsConfirmedSpain)
                return;

            await UniTask.SwitchToMainThread();
            await UniTask.WaitUntil(() => _disposed || ControlsApi.MenuInputOverride.Value);

            if (_disposed || HasSeenNotice())
                return;

            _dialog = new LaLigaCensorshipDialog(
                _configService.UseAlternativeDomainsInSpain.Value,
                ApplyDecision);
            UIApi.AddZeepGUIDrawer(_dialog);
            PlayerPrefs.SetInt(NoticeSeenKey, 1);
            PlayerPrefs.Save();
        }
        finally
        {
            _checking = false;
        }
    }

    private void ApplyDecision(bool useAlternativeDomains)
    {
        _configService.UseAlternativeDomainsInSpain.Value = useAlternativeDomains;
        RemoveDialog();
    }

    private void RemoveDialog()
    {
        if (_dialog == null)
            return;

        UIApi.RemoveZeepGUIDrawer(_dialog);
        _dialog.Dispose();
        _dialog = null;
    }

    private static bool HasSeenNotice() => PlayerPrefs.GetInt(NoticeSeenKey, 0) == 1;
}
