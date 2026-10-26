using System.Reflection;

namespace VecturaDB.Tests;

// The AssemblyMetadata of the project of the tests: where the tool and ArgsEcho are built, the cache of VecturaDB that the runs of the
// tests share, and the tag of the release, from Directory.Build.props.
internal static class TestMetadata
{
	public static string Value(string key) => typeof(TestMetadata).Assembly
		.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == key).Value!;

	public static string ToolPath => Path.GetFullPath(Value("ToolPath"));

	public static string ArgsEcho => Path.Combine(Path.GetFullPath(Value("ArgsEchoDirectory")), OperatingSystem.IsWindows() ? "ArgsEcho.exe" : "ArgsEcho");

	public static string TestCache => Path.GetFullPath(Value("TestCache"));
}
