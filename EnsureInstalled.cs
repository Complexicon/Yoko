using Spectre.Console;
using Yoko;

class EnsureInstalled {

	static void AddToPath(string path) {

	}

	public static void Run() {
		if (Helpers.GetFullPath(Globals.AppName) != null) return; // already installed
		if (!AnsiConsole.Confirm($"[yellow]It looks like the [green]'{Globals.AppName.ToLower()}'[/] tool isn't installed. Do you want to install it?[/]", false)) return;
		Install();
	}

	public static void Install() {
		if (Environment.ProcessPath == null) return; // ????
		var dir = Directory.CreateDirectory(Path.Join(YokoPaths.AppDir, "bin"));
		File.Copy(Environment.ProcessPath, Path.Join(dir.FullName, Globals.AppName + (Globals.IsWindows ? ".exe" : "")));
		AddToPath(dir.FullName);
	}
}