namespace LocalAgent.Service.Config;

public sealed class ServiceOptions
{
    public HttpOptions Http { get; set; } = new();
    public bool AllowConcurrentRuns { get; set; } = false;
}

public sealed class HttpOptions
{
    public string[] Urls { get; set; } = ["http://0.0.0.0:5070"];
}
