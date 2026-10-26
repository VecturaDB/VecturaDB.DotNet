using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace VecturaDB.Tests;

// The download into the cache, from a FileServer in place of the GitHub release, with a release whose file is the bytes it serves.
public class ExecutableCacheTests
{
	private const string Rid = "linux-x64";
	private static readonly byte[] Executable = "the executable of the test"u8.ToArray();
	private string _root = null!;

	[SetUp]
	public void CreateRoot() => _root = Directory.CreateTempSubdirectory("vecturadb-cache-").FullName;

	[TearDown]
	public void DeleteRoot() => Directory.Delete(_root, recursive: true);

	private static Release ReleaseOf(byte[] content) => new("v9.9.9-test", new Dictionary<string, ReleaseFile>
	{
		[Rid] = new("chroma-linux-x86_64", Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()),
	});

	private Task<string> GetAsync(Release release, Uri uri) =>
		ExecutableCache.GetAsync(release, Rid, _root, uri, TextWriter.Null, CancellationToken.None);

	// The first run downloads the file into <cache>/<tag>/<rid>/, and the next one takes it from there, without the network.
	[Test]
	public async Task DownloadsOnceThenRunsFromTheCache()
	{
		var release = ReleaseOf(Executable);
		string path;
		await using (var server = new FileServer(Executable))
		{
			path = await GetAsync(release, server.Uri);
			Assert.That(server.Requests, Is.EqualTo(1));
		}
		Assert.That(path, Is.EqualTo(Path.Combine(_root, "v9.9.9-test", Rid, "vecturadb")));
		Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(Executable));
		if (!OperatingSystem.IsWindows())
			Assert.That(File.GetUnixFileMode(path) & UnixFileMode.UserExecute, Is.EqualTo(UnixFileMode.UserExecute));
		Assert.That(await GetAsync(release, new Uri("http://127.0.0.1:1/")), Is.EqualTo(path), "no network needed");
	}

	// A file with another hash: the run fails, and nothing stays in the cache.
	[Test]
	public async Task FileWithAnotherHash()
	{
		await using var server = new FileServer("another file"u8.ToArray());
		var e = Assert.ThrowsAsync<InvalidDataException>(() => GetAsync(ReleaseOf(Executable), server.Uri));
		Assert.That(e!.Message, Does.Contain("SHA-256 hash").And.Contain(server.Uri + "chroma-linux-x86_64"));
		Assert.That(Directory.EnumerateFiles(Path.Combine(_root, "v9.9.9-test", Rid)).Select(Path.GetFileName), Is.EquivalentTo(new[] { ".lock" }));
	}

	// An executable of the cache that changed: the run downloads it again.
	[Test]
	public async Task ChangedExecutableIsDownloadedAgain()
	{
		var release = ReleaseOf(Executable);
		await using var server = new FileServer(Executable);
		var path = await GetAsync(release, server.Uri);
		await File.WriteAllBytesAsync(path, "changed"u8.ToArray());
		Assert.That(await GetAsync(release, server.Uri), Is.EqualTo(path));
		Assert.That(server.Requests, Is.EqualTo(2));
		Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(Executable));
	}

	// Two runs at the same time, with an empty cache: the file is downloaded once.
	[Test]
	public async Task TwoRunsDownloadOnce()
	{
		var release = ReleaseOf(Executable);
		await using var server = new FileServer(Executable);
		var paths = await Task.WhenAll(GetAsync(release, server.Uri), GetAsync(release, server.Uri));
		Assert.That(paths[0], Is.EqualTo(paths[1]));
		Assert.That(server.Requests, Is.EqualTo(1));
	}

	// Without the network, the message says what to do.
	[Test]
	public void NoNetworkSaysWhatToDo()
	{
		var e = Assert.ThrowsAsync<HttpRequestException>(() => GetAsync(ReleaseOf(Executable), new Uri($"http://127.0.0.1:{ClosedPort()}/")));
		Assert.That(e!.Message, Does.Contain("Could not download the VecturaDB executable for linux-x64")
			.And.Contain("VECTURADB_DOWNLOAD_URI").And.Contain("VECTURADB_EXECUTABLE"));
	}

	[Test]
	public void PlatformWithoutAnExecutable()
	{
		var e = Assert.ThrowsAsync<PlatformNotSupportedException>(() =>
			ExecutableCache.GetAsync(ReleaseOf(Executable), "linux-riscv64", _root, null, TextWriter.Null, CancellationToken.None));
		Assert.That(e!.Message, Does.Contain("linux-riscv64"));
	}

	[TestCase("win-x64", "vecturadb.exe")]
	[TestCase("linux-arm64", "vecturadb")]
	[TestCase("osx-arm64", "vecturadb")]
	public void NameOfTheExecutable(string rid, string name) => Assert.That(ExecutableCache.ExecutableName(rid), Is.EqualTo(name));

	private static int ClosedPort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}
}
