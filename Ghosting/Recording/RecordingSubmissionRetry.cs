using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TNRD.Zeepkist.GTR.Api;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

internal static class RecordingSubmissionRetry
{
    internal static bool ShouldRetry(int status) => status == 408 || status == 429 || status >= 500;
    internal static int DelayMilliseconds(int attempt) => 500 << System.Math.Min(2, System.Math.Max(0, attempt));

    internal static async Task UploadAsync(
        string json,
        Func<string, CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken token,
        Func<int, CancellationToken, Task> delay = null)
    {
        delay ??= Task.Delay;
        for (int attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            HttpResponseMessage response;
            try { response = await send(json, token).ConfigureAwait(false); }
            catch (HttpRequestException) when (attempt < 3 && !token.IsCancellationRequested)
            {
                await delay(DelayMilliseconds(attempt), token).ConfigureAwait(false);
                continue;
            }
            catch (OperationCanceledException) when (attempt < 3 && !token.IsCancellationRequested)
            {
                await delay(DelayMilliseconds(attempt), token).ConfigureAwait(false);
                continue;
            }

            using (response)
            {
                if (attempt >= 3 || !ShouldRetry((int)response.StatusCode))
                {
                    await ApiResponseErrors.EnsureSuccessWithBodyAsync(response).ConfigureAwait(false);
                    return;
                }
            }
            await delay(DelayMilliseconds(attempt), token).ConfigureAwait(false);
        }
    }
}
