namespace VecturaDB;

// Which VecturaDB executable the command runs: VECTURADB_EXECUTABLE when it is set, or the one of this platform from the cache of the
// user, VECTURADB_CACHE or the default one, downloaded at the first run from VECTURADB_DOWNLOAD_URI or the GitHub release.
internal static class Launcher
{
	public static async Task<string> ExecutableAsync(TextWriter log, CancellationToken cancellationToken)
	{
		if (Environment.GetEnvironmentVariable("VECTURADB_EXECUTABLE") is { Length: > 0 } given)
		{
			return File.Exists(given)
				? Path.GetFullPath(given)
				: throw new FileNotFoundException($"VECTURADB_EXECUTABLE is {given}, which is not a file.");
		}
		var release = Release.Current;
		var rid = Platform.Rid();
		if (rid is null || !release.Files.ContainsKey(rid))
		{
			throw new PlatformNotSupportedException(
				$"VecturaDB {release.Version} has no executable for {System.Runtime.InteropServices.RuntimeInformation.OSDescription} {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}: its release has {string.Join(", ", release.Files.Keys.Order(StringComparer.Ordinal))}. Set VECTURADB_EXECUTABLE to an executable of VecturaDB for this machine.");
		}
		if (Platform.IsMusl())
			throw new PlatformNotSupportedException("Linux with musl, like Alpine, is not supported: the executables of VecturaDB need glibc 2.17 or later.");
		var downloadUri = Environment.GetEnvironmentVariable("VECTURADB_DOWNLOAD_URI") is { Length: > 0 } uri
			? new Uri(uri.EndsWith('/') ? uri : uri + "/")
			: null;
		return await ExecutableCache.GetAsync(release, rid, ExecutableCache.DefaultRoot(), downloadUri, log, cancellationToken).ConfigureAwait(false);
	}
}
