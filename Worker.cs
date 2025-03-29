// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Hosting;
using System.Net.Sockets;

internal class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TcpListener listener = new TcpListener(9000);

        listener.Start();
        while (!stoppingToken.IsCancellationRequested)
        {
            await listener.AcceptSocketAsync(stoppingToken);
        }
    }
}