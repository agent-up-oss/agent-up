using System.Net;
using LocalInstaller.Sample;

var url = Environment.GetEnvironmentVariable(SampleProduct.EnvironmentPrefix + "_SERVER_URL")
    ?? SampleProduct.ServerUrl + "/";

if (!url.EndsWith('/'))
    url += "/";

using var listener = new HttpListener();
listener.Prefixes.Add(url);
listener.Start();

Console.WriteLine($"{SampleProduct.Name} server listening on {url}");

while (true)
{
    var context = await listener.GetContextAsync();
    var response = context.Response;
    var body = $$"""
        {
          "product": "{{SampleProduct.Name}}",
          "status": "ok"
        }
        """;
    response.ContentType = "application/json";
    await using var output = response.OutputStream;
    await using var writer = new StreamWriter(output);
    await writer.WriteAsync(body);
}
