using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VecturaDB.Tests;

// A server of one file on 127.0.0.1, for the tests of the download: it answers each request with the bytes of Content, and counts the
// requests.
internal sealed class FileServer : IAsyncDisposable
{
	private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
	private readonly CancellationTokenSource _stop = new();
	private readonly Task _loop;
	private int _requests;

	public FileServer(byte[] content)
	{
		Content = content;
		_listener.Start();
		_loop = Task.Run(LoopAsync);
	}

	public byte[] Content { get; set; }

	public Uri Uri => new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");

	public int Requests => Volatile.Read(ref _requests);

	public async ValueTask DisposeAsync()
	{
		await _stop.CancelAsync();
		_listener.Stop();
		try
		{
			await _loop;
		}
		catch (OperationCanceledException)
		{
		}
		_stop.Dispose();
	}

	private async Task LoopAsync()
	{
		while (!_stop.IsCancellationRequested)
		{
			TcpClient client;
			try
			{
				client = await _listener.AcceptTcpClientAsync(_stop.Token);
			}
			catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
			{
				return;
			}
			_ = Task.Run(() => ServeAsync(client));
		}
	}

	private async Task ServeAsync(TcpClient client)
	{
		using (client)
		{
			var stream = client.GetStream();
			var buffer = new byte[4096];
			var received = new StringBuilder();
			while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
			{
				var read = await stream.ReadAsync(buffer);
				if (read == 0)
					return;
				received.Append(Encoding.ASCII.GetString(buffer, 0, read));
			}
			Interlocked.Increment(ref _requests);
			var content = Content;
			await stream.WriteAsync(Encoding.ASCII.GetBytes(
				$"HTTP/1.1 200 OK\r\nContent-Length: {content.Length}\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n"));
			await stream.WriteAsync(content);
		}
	}
}
