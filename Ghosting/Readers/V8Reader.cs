using System;
using Microsoft.Extensions.Logging;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class V8Reader : V6Reader
{
    public V8Reader(IServiceProvider provider, ILogger<V8Reader> logger) : base(provider, logger) { }
    protected override int ExpectedVersion => 8;
}
