using VecturaDB;

// vecturadb [arguments]: runs the VecturaDB executable of the release of this tool, with the arguments as they are, and ends with its
// exit code. The first run downloads the executable into the cache of the user.
try
{
	var executable = await Launcher.ExecutableAsync(Console.Error, CancellationToken.None);
	return Runner.Run(executable, args);
}
catch (Exception e) when (e is IOException or HttpRequestException or InvalidDataException or PlatformNotSupportedException
	or UnauthorizedAccessException or TaskCanceledException)
{
	Console.Error.WriteLine($"vecturadb: {e.Message}");
	return 1;
}
