using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DatabaseAPI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DatabaseAPI.Tests.Services;

public class ClamAvScannerTests
{
    private static ClamAvScanner CreateScanner(int port, bool enabled = true) =>
        new(
            Options.Create(new ClamAvOptions
            {
                Enabled = enabled,
                Host = IPAddress.Loopback.ToString(),
                Port = port,
                TimeoutSeconds = 5
            }),
            NullLogger<ClamAvScanner>.Instance);

    private static Task<TcpListener> StartFakeClamdAsync(string response)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        _ = Task.Run(async () =>
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync();
                await using var stream = client.GetStream();

                var command = new byte["zINSTREAM\0".Length];
                await stream.ReadExactlyAsync(command);

                var lengthBuffer = new byte[4];
                while (true)
                {
                    await stream.ReadExactlyAsync(lengthBuffer);
                    var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                    if (length == 0) break;

                    var payload = new byte[length];
                    await stream.ReadExactlyAsync(payload);
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes(response + "\0"));
            }
            catch
            {
                // The test assertion reports the scanner result; the fake
                // server failing to accept is not interesting on its own.
            }
        });

        return Task.FromResult(listener);
    }

    [Fact]
    public async Task DisabledScanner_AcceptsWithoutContactingClamd()
    {
        var result = await CreateScanner(1, enabled: false).ScanAsync([1, 2, 3]);

        Assert.True(result.Clean);
    }

    [Fact]
    public async Task CleanResponse_IsAccepted()
    {
        var listener = await StartFakeClamdAsync("stream: OK");
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var result = await CreateScanner(port).ScanAsync("hello"u8.ToArray());

            Assert.True(result.Clean);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task FoundResponse_IsRejectedWithThreatName()
    {
        var listener = await StartFakeClamdAsync("stream: Eicar-Test-Signature FOUND");
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var result = await CreateScanner(port).ScanAsync("hello"u8.ToArray());

            Assert.False(result.Clean);
            Assert.Equal("Eicar-Test-Signature", result.ThreatName);
        }
        finally
        {
            listener.Stop();
        }
    }
}
