using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace VecturaDB.Tests;

// The command as a user runs it: dotnet with the tool as it is built, its arguments and the environment variables, with the cache of
// the tests.
public class CommandTests
{
	private const string Collections = "api/v2/tenants/default_tenant/databases/default_database/collections";

	private static ProcessStartInfo Command(IEnumerable<string> args, Dictionary<string, string>? environment = null)
	{
		var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
		start.ArgumentList.Add(TestMetadata.ToolPath);
		foreach (var arg in args)
			start.ArgumentList.Add(arg);
		start.Environment["VECTURADB_CACHE"] = TestMetadata.TestCache;
		foreach (var (name, value) in environment ?? [])
			start.Environment[name] = value;
		return start;
	}

	private static async Task<(int Code, string Output, string Error)> RunAsync(ProcessStartInfo start)
	{
		using var process = Process.Start(start)!;
		var output = process.StandardOutput.ReadToEndAsync();
		var error = process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
		return (process.ExitCode, await output, await error);
	}

	// The arguments reach the executable as they are, an empty one and quotes included, and the exit code is its own: ArgsEcho prints
	// each argument and ends with 7.
	[Test]
	public async Task ArgumentsAndExitCodeOfTheExecutable()
	{
		string[] args = ["run", "--path", "a directory with spaces", "--x=1", "quote\"d", ""];
		var (code, output, error) = await RunAsync(Command(args, new() { ["VECTURADB_EXECUTABLE"] = TestMetadata.ArgsEcho }));
		Assert.That(code, Is.EqualTo(7), error);
		Assert.That(output.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n'), Is.EqualTo(args.Select(a => $"[{a}]")));
	}

	[Test]
	public async Task ExecutableThatIsNotThere()
	{
		var missing = Path.Combine(Path.GetTempPath(), "no-vecturadb-here");
		var (code, _, error) = await RunAsync(Command(["--version"], new() { ["VECTURADB_EXECUTABLE"] = missing }));
		Assert.That(code, Is.EqualTo(1));
		Assert.That(error, Does.Contain($"VECTURADB_EXECUTABLE is {missing}, which is not a file."));
	}

	// VecturaDB of the release, downloaded into the cache of the tests at the first run: the server answers, writes a record and counts
	// it. Then it ends with the command: on Linux and macOS, where the command became VecturaDB, at SIGTERM; on Windows, where it runs
	// in the job object of the command, when only the command is killed.
	[Test]
	[Category("Server")]
	public async Task RunsTheServerAndEndsWithTheCommand()
	{
		var data = Directory.CreateTempSubdirectory("vecturadb-data-").FullName;
		var port = FreePort();
		var log = new ConcurrentQueue<string>();
		using var process = Process.Start(Command(["run", "--path", data, "--host", "127.0.0.1", "--port", port.ToString(CultureInfo.InvariantCulture)]))!;
		process.OutputDataReceived += (_, e) => log.Enqueue(e.Data ?? "");
		process.ErrorDataReceived += (_, e) => log.Enqueue(e.Data ?? "");
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/"), Timeout = TimeSpan.FromSeconds(5) };
		try
		{
			Assert.That(await AnswersWithinAsync(http, process, TimeSpan.FromMinutes(3)), Is.True, () => string.Join("\n", log));
			var created = await http.PostAsJsonAsync(Collections, new { name = "check" });
			created.EnsureSuccessStatusCode();
			var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
			(await http.PostAsJsonAsync($"{Collections}/{id}/add", new { ids = new[] { "a" }, embeddings = new[] { new[] { 1f, 2f } } })).EnsureSuccessStatusCode();
			Assert.That(await http.GetStringAsync($"{Collections}/{id}/count"), Is.EqualTo("1"));
		}
		finally
		{
			Stop(process);
		}
		Assert.That(process.HasExited, Is.True, "the command ended");
		Assert.That(await StillAnswersAfterAsync(http, TimeSpan.FromSeconds(15)), Is.False, "the server ended with the command");
		try
		{
			Directory.Delete(data, recursive: true);
		}
		catch (IOException)
		{
		}
	}

	private static void Stop(Process process)
	{
		if (OperatingSystem.IsWindows())
		{
			process.Kill();
		}
		else
		{
			using var kill = Process.Start("kill", ["-TERM", process.Id.ToString(CultureInfo.InvariantCulture)]);
			kill.WaitForExit();
		}
		process.WaitForExit(15_000);
	}

	// Whether the server answers on /api/v2/heartbeat before the time ends, while the command runs.
	private static async Task<bool> AnswersWithinAsync(HttpClient http, Process process, TimeSpan time)
	{
		var deadline = DateTime.UtcNow + time;
		while (DateTime.UtcNow < deadline && !process.HasExited)
		{
			try
			{
				(await http.GetAsync("api/v2/heartbeat")).EnsureSuccessStatusCode();
				return true;
			}
			catch (HttpRequestException)
			{
			}
			catch (TaskCanceledException)
			{
			}
			await Task.Delay(100);
		}
		return false;
	}

	// Whether the server still answers once the time is over.
	private static async Task<bool> StillAnswersAfterAsync(HttpClient http, TimeSpan time)
	{
		var deadline = DateTime.UtcNow + time;
		while (DateTime.UtcNow < deadline)
		{
			try
			{
				await http.GetAsync("api/v2/heartbeat");
			}
			catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
			{
				return false;
			}
			await Task.Delay(200);
		}
		return true;
	}

	private static int FreePort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}
}
