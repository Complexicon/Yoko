using System.Diagnostics;

class Helpers {
	public static string[]? GetFullPath(string binName) {

		ProcessStartInfo startInfo = new() {
			RedirectStandardError = true,
			RedirectStandardOutput = true,
		};

		if (Globals.IsWindows) {
			startInfo.FileName = "where";
			startInfo.Arguments = binName;
		} else {
			startInfo.FileName = "sh";
			startInfo.Arguments = $"-c \"which -a {binName}\"";
		}

		try {
			using Process? p = Process.Start(startInfo);

			if (p == null) return null;

			p.WaitForExit();

			if (p.ExitCode != 0)
				return null;

			return p.StandardOutput.ReadToEnd().Split('\n').Select(v => v.Trim()).Where(v => v.Length != 0).ToArray();
		} catch {
			return null;
		}

	}
}