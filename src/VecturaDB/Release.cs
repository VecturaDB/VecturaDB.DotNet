using System.Reflection;

namespace VecturaDB;

// The release of VecturaDB that this tool runs, from the AssemblyMetadata that Executables.props gives the assembly: its tag, and for
// each platform the name of the file in the release and its SHA-256 hash.
internal sealed class Release
{
	private const string Prefix = "VecturaDB.";

	public Release(string tag, IReadOnlyDictionary<string, ReleaseFile> files)
	{
		Tag = tag;
		Files = files;
	}

	public static Release Current { get; } = FromAssembly(typeof(Release).Assembly);

	// Like v1.0.0-preview.3.
	public string Tag { get; }

	public string Version => Tag.TrimStart('v');

	// The file of each platform, by its runtime identifier.
	public IReadOnlyDictionary<string, ReleaseFile> Files { get; }

	// The address of the files of the GitHub release, with a slash at the end.
	public Uri DownloadUri => new($"https://github.com/VecturaDB/VecturaDB/releases/download/{Tag}/");

	public static Release FromAssembly(Assembly assembly)
	{
		var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
			.Where(a => a.Key.StartsWith(Prefix, StringComparison.Ordinal) && a.Value is not null)
			.ToDictionary(a => a.Key[Prefix.Length..], a => a.Value!, StringComparer.Ordinal);
		var files = new Dictionary<string, ReleaseFile>(StringComparer.Ordinal);
		foreach (var key in metadata.Keys.Where(k => k.StartsWith("Asset:", StringComparison.Ordinal)))
		{
			var rid = key["Asset:".Length..];
			if (metadata.TryGetValue("Sha256:" + rid, out var hash))
				files[rid] = new ReleaseFile(metadata[key], hash.ToLowerInvariant());
		}
		return new Release(metadata.TryGetValue("Tag", out var tag) ? tag : "unknown", files);
	}
}

// A file of the release: its name, like chroma-linux-x86_64, and the SHA-256 hash of its content, in lower case.
internal sealed record ReleaseFile(string Asset, string Sha256);
