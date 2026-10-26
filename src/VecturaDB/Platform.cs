using System.Runtime.InteropServices;

namespace VecturaDB;

// The platform of this process, as the releases of VecturaDB have it.
internal static class Platform
{
	// win-x64, also on Windows on Arm, which runs it through emulation; linux-x64, linux-arm64, osx-x64 and osx-arm64; null elsewhere.
	public static string? Rid() => Rid(
		OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsLinux() ? OSPlatform.Linux : OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.FreeBSD,
		RuntimeInformation.OSArchitecture);

	public static string? Rid(OSPlatform os, Architecture architecture) => (os, architecture) switch
	{
		_ when os == OSPlatform.Windows && architecture is Architecture.X64 or Architecture.Arm64 => "win-x64",
		_ when os == OSPlatform.Linux && architecture == Architecture.X64 => "linux-x64",
		_ when os == OSPlatform.Linux && architecture == Architecture.Arm64 => "linux-arm64",
		_ when os == OSPlatform.OSX && architecture == Architecture.X64 => "osx-x64",
		_ when os == OSPlatform.OSX && architecture == Architecture.Arm64 => "osx-arm64",
		_ => null,
	};

	// Linux with musl, like Alpine, where the executables of VecturaDB, built for glibc, do not start.
	public static bool IsMusl() => OperatingSystem.IsLinux()
		&& (File.Exists("/etc/alpine-release") || (Directory.Exists("/lib") && Directory.EnumerateFiles("/lib", "ld-musl-*").Any()));
}
