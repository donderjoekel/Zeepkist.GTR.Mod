using System;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Connectivity;

internal interface ISpainRoutingState
{
    Task<SpainTraceResult> GetTraceResultAsync();
    bool IsProductionRequest(ServiceEndpoint endpoint, Uri requestUri);
    Uri GetAlternativeRequestUri(ServiceEndpoint endpoint, Uri requestUri);
    void PromoteAlternativeDomains();
}
