using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using System.Net.Http;

namespace Edemly.Client.Realtime
{
    public static class HubConnectionFactory
    {
        public static HubConnection Create(string hubUrl, string? token, bool webSocketsOnly = false)
        {
            return new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);

                    if (webSocketsOnly)
                    {
                        options.Transports = HttpTransportType.WebSockets;
                        options.SkipNegotiation = true;

                        options.WebSocketConfiguration = ws =>
                            ws.KeepAliveInterval = HubSettings.WebSocketKeepAliveInterval;

                        options.HttpMessageHandlerFactory = _ =>
                            new HttpClientHandler
                            {
                                AutomaticDecompression =
                                    DecompressionMethods.GZip |
                                    DecompressionMethods.Deflate
                            };
                    }
                })
                .WithAutomaticReconnect(HubSettings.ReconnectDelays)
                .Build();
        }
    }
}