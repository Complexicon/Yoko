using System.Diagnostics;
using Spectre.Console;
using Yoko;

class TestAction : ICLIAction {
	public string Command => "test";

	public async Task Run(string[] args) {

		if(Globals.Config == null) {
			AnsiConsole.MarkupLine("[red]No Project Present![/]");
			return;
		}

		await new BuildAction().Run(args);

		var serverJar = await Globals.GetServerPath(Globals.Config.MinecraftVersion);

		var javaHome = Globals.IsJDK8(Globals.Config.MinecraftVersion) ? Globals.JDK8Path : Globals.JDK17Path;
		var runtime = Path.Join(javaHome, "bin", "java" + (Globals.IsWindows ? ".exe" : ""));

		var java = new Subprocess(runtime) {
			WorkingDirectory = YokoPaths.LocalServer,
			PipeStdio = true,
		};

		async void srcChanged(object sender, FileSystemEventArgs e) {

			FileSystemWatcher? watcher = null;
			try {
				watcher = (FileSystemWatcher)sender;
				watcher.EnableRaisingEvents = false;

				AnsiConsole.MarkupLine($"[yellow]{e.FullPath} {e.ChangeType}[/]");
				await new BuildAction().Run(args);
				java.CurrentProcess?.StandardInput.WriteLine("reload confirm");

			} finally {
				if (watcher != null) {
					watcher.EnableRaisingEvents = true;
				}
			}

		}

		var fsWatcher = new FileSystemWatcher(Path.Join("src", "main", "java"), "*.java") {
			IncludeSubdirectories = true,
			EnableRaisingEvents = true,
			//NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
		};

		fsWatcher.Created += srcChanged;
		fsWatcher.Deleted += srcChanged;
		fsWatcher.Changed += srcChanged;

		await java.RunWithArgs("-agentlib:jdwp=transport=dt_socket,server=y,suspend=y,address=5005", "-jar", serverJar, "nogui");

	}
}