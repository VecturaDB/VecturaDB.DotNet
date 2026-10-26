using System.Runtime.InteropServices;

namespace VecturaDB.Tests;

public class ReleaseTests
{
	// The release the tool runs: the tag of Directory.Build.props, and the file of each of the five platforms with its hash.
	[Test]
	public void TheReleaseOfTheBuild()
	{
		var release = Release.Current;
		Assert.That(release.Tag, Is.EqualTo(TestMetadata.Value("VecturaDBTag")));
		Assert.That(release.Files.Keys, Is.EquivalentTo(new[] { "win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" }));
		Assert.That(release.Files.Values.Select(f => f.Sha256), Has.All.Match("^[0-9a-f]{64}$"));
		Assert.That(release.Files.Values.Select(f => f.Asset), Has.All.StartWith("chroma-"));
		Assert.That(release.DownloadUri.ToString(), Is.EqualTo($"https://github.com/VecturaDB/VecturaDB/releases/download/{release.Tag}/"));
	}

	[TestCase("Windows", Architecture.X64, "win-x64")]
	[TestCase("Windows", Architecture.Arm64, "win-x64")]
	[TestCase("Linux", Architecture.X64, "linux-x64")]
	[TestCase("Linux", Architecture.Arm64, "linux-arm64")]
	[TestCase("OSX", Architecture.X64, "osx-x64")]
	[TestCase("OSX", Architecture.Arm64, "osx-arm64")]
	[TestCase("Linux", Architecture.X86, null)]
	[TestCase("FreeBSD", Architecture.X64, null)]
	public void PlatformOfTheRelease(string os, Architecture architecture, string? rid)
	{
		Assert.That(Platform.Rid(OSPlatform.Create(os.ToUpperInvariant()), architecture), Is.EqualTo(rid));
	}

	// The runners of the CI are among the platforms of the release.
	[Test]
	public void ThisPlatformHasAnExecutable()
	{
		var rid = Platform.Rid();
		Assert.That(rid, Is.Not.Null);
		Assert.That(Release.Current.Files.Keys, Does.Contain(rid));
	}
}
