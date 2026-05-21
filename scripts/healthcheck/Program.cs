// Container HEALTHCHECK probe. Usage: dotnet HealthCheck.dll <url>
// Exits 0 if the URL returns a 2xx within the timeout, 1 otherwise.
// Kept dependency-free so it runs framework-dependent on the runtime image.

var url = args.Length > 0 ? args[0] : "http://localhost:8080/api/health";

try
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    using var resp = await http.GetAsync(url);
    if (resp.IsSuccessStatusCode)
    {
        return 0;
    }

    await Console.Error.WriteLineAsync($"healthcheck: {url} returned {(int)resp.StatusCode}");
    return 1;
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"healthcheck: {url} unreachable: {ex.Message}");
    return 1;
}
