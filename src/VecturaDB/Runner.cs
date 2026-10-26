using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VecturaDB;

// Runs the executable with the arguments of the command, and gives its exit code.
internal static class Runner
{
	public static int Run(string executable, IReadOnlyList<string> args) =>
		OperatingSystem.IsWindows() ? RunChild(executable, args) : Exec(executable, args);

	// Linux and macOS: this process becomes VecturaDB, with execv, so that its signals, its exit code and its terminal are those of
	// VecturaDB. execv returns only when it fails.
	private static int Exec(string executable, IReadOnlyList<string> args)
	{
		Console.Out.Flush();
		Console.Error.Flush();
		var argv = new string?[args.Count + 2];
		argv[0] = executable;
		for (var i = 0; i < args.Count; i++)
			argv[i + 1] = args[i];
		execv(executable, argv);
		throw new IOException($"Could not run {executable}: execv failed with the error {Marshal.GetLastPInvokeError()}.");
	}

	// Windows: VecturaDB as a child process in a job object that ends it with this process. Ctrl+C reaches both: this process lets it
	// pass, and waits for the exit code of VecturaDB.
	private static int RunChild(string executable, IReadOnlyList<string> args)
	{
		var start = new ProcessStartInfo(executable) { UseShellExecute = false };
		foreach (var arg in args)
			start.ArgumentList.Add(arg);
		Console.CancelKeyPress += (_, e) => e.Cancel = true;
		using var process = Process.Start(start) ?? throw new IOException($"Could not start {executable}.");
		WindowsJob.TryAssign(process, out _);
		process.WaitForExit();
		return process.ExitCode;
	}

	// On Linux and macOS, LPStr is UTF-8.
	[DllImport("libc", SetLastError = true)]
	private static extern int execv([MarshalAs(UnmanagedType.LPStr)] string path,
		[MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string?[] argv);
}
