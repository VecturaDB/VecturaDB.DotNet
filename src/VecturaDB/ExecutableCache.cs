using System.Security.Cryptography;

namespace VecturaDB;

// The executables of VecturaDB in the cache of the user, in <cache>/<tag>/<rid>/vecturadb, vecturadb.exe on Windows: downloaded from
// the release at the first run, and checked against the SHA-256 hash of the release after the download and before each run. A lock
// file of the directory keeps a second run from downloading at the same time: it waits, then finds the file there.
internal static class ExecutableCache
{
	private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(10);

	private static readonly Lazy<HttpClient> Http = new(() =>
	{
		var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd($"VecturaDB-dotnet/{typeof(ExecutableCache).Assembly.GetName().Version}");
		return client;
	});

	// VECTURADB_CACHE, or the cache of the user: %LOCALAPPDATA%\VecturaDB on Windows, ~/Library/Caches/VecturaDB on macOS,
	// $XDG_CACHE_HOME/vecturadb or ~/.cache/vecturadb on Linux.
	public static string DefaultRoot()
	{
		if (Environment.GetEnvironmentVariable("VECTURADB_CACHE") is { Length: > 0 } cache)
			return Path.GetFullPath(cache);
		if (OperatingSystem.IsWindows())
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VecturaDB");
		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (OperatingSystem.IsMacOS())
			return Path.Combine(home, "Library", "Caches", "VecturaDB");
		return Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg
			? Path.Combine(xdg, "vecturadb")
			: Path.Combine(home, ".cache", "vecturadb");
	}

	public static string ExecutableName(string rid) => rid.StartsWith("win-", StringComparison.Ordinal) ? "vecturadb.exe" : "vecturadb";

	// The executable of the platform, downloaded when the cache does not have it with its hash. downloadUri, with a slash at the end,
	// takes the place of the GitHub release.
	public static async Task<string> GetAsync(Release release, string rid, string root, Uri? downloadUri, TextWriter log,
		CancellationToken cancellationToken)
	{
		if (!release.Files.TryGetValue(rid, out var file))
			throw new PlatformNotSupportedException($"VecturaDB {release.Version} has no executable for {rid}.");
		var directory = Path.Combine(root, release.Tag, rid);
		var path = Path.Combine(directory, ExecutableName(rid));
		if (await HasHashAsync(path, file.Sha256, cancellationToken).ConfigureAwait(false))
			return path;
		Directory.CreateDirectory(directory);
		using (await LockAsync(Path.Combine(directory, ".lock"), cancellationToken).ConfigureAwait(false))
		{
			// A second run waited for the first, which downloaded the file.
			if (await HasHashAsync(path, file.Sha256, cancellationToken).ConfigureAwait(false))
				return path;
			var source = new Uri(downloadUri ?? release.DownloadUri, file.Asset);
			log.WriteLine($"vecturadb: downloading VecturaDB {release.Version} for {rid} from {source}");
			var temporary = Path.Combine(directory, $".download-{Guid.NewGuid():N}");
			try
			{
				await DownloadAsync(source, temporary, release, rid, cancellationToken).ConfigureAwait(false);
				var hash = await HashFileAsync(temporary, cancellationToken).ConfigureAwait(false);
				if (!string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidDataException(
						$"The file downloaded from {source} has the SHA-256 hash {hash}, not {file.Sha256}: it is not the executable of VecturaDB {release.Version} for {rid}.");
				}
				if (!OperatingSystem.IsWindows())
				{
					File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
						| UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
				}
				File.Move(temporary, path, overwrite: true);
			}
			finally
			{
				if (File.Exists(temporary))
					File.Delete(temporary);
			}
		}
		return path;
	}

	public static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.Asynchronous);
		return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
	}

	private static async Task<bool> HasHashAsync(string path, string hash, CancellationToken cancellationToken) =>
		File.Exists(path) && string.Equals(await HashFileAsync(path, cancellationToken).ConfigureAwait(false), hash, StringComparison.OrdinalIgnoreCase);

	private static async Task DownloadAsync(Uri source, string destination, Release release, string rid, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await Http.Value.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();
			await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.Asynchronous);
			await response.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
		{
			throw new HttpRequestException(
				$"Could not download the VecturaDB executable for {rid} from {source}: {e.Message} Without the network: set VECTURADB_DOWNLOAD_URI to a mirror with the files of the release {release.Tag}, or VECTURADB_EXECUTABLE to an executable of VecturaDB.", e);
		}
	}

	// The lock of a directory of the cache: a file that one process at a time opens, released when it is disposed.
	private static async Task<FileStream> LockAsync(string path, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + LockTimeout;
		while (true)
		{
			try
			{
				return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
			}
			catch (IOException) when (DateTime.UtcNow < deadline)
			{
				await Task.Delay(200, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
