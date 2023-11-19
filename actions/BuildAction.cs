using Spectre.Console;
using Yoko;

class BuildAction : ICLIAction {
	public string Command => "build";

	public async Task Run(string[] args) {

		if(Globals.Config == null) {
			AnsiConsole.MarkupLine("[red]No Project Present![/]");
			return;
		}

		if (!Directory.Exists(YokoPaths.LocalAppDir)) {
			AnsiConsole.MarkupLine("[red]Project is not Hydrated![/]");

			if (!AnsiConsole.Confirm("[yellow]Do you want to Hydrate it now?")) Environment.Exit(0);
			
			await new HydrateAction().Run(Array.Empty<string>()); // hydrate calls build automatically
			return;
		}

		Directory.CreateDirectory(YokoPaths.CompilerOut);

		Console.WriteLine("building plugin...");

		var (apiJar, _) = await Globals.GetAPIPath(Globals.Config.MinecraftVersion);
		var javaHome = Globals.IsJDK8(Globals.Config.MinecraftVersion) ? Globals.JDK8Path : Globals.JDK17Path;

		var compiler = new Subprocess(Path.Join(javaHome, "bin", "javac" + (Globals.IsWindows ? ".exe" : "")));
		var packager = new Subprocess(Path.Join(javaHome, "bin", "jar" + (Globals.IsWindows ? ".exe" : "")));

		List<string> classpath = new() { apiJar };

		var sourceFiles = Directory.EnumerateFiles(YokoPaths.SourceDir, "*.java", SearchOption.AllDirectories);//.Select(f => Path.GetRelativePath(sourceLocation, f));

		var toBuild = new List<string>();

		// compare build times like in gnu make
		foreach (var sourceFile in sourceFiles) {
			var srcLastChange = new FileInfo(sourceFile).LastWriteTime;

			var relativePath = Path.GetRelativePath(YokoPaths.SourceDir, sourceFile);
			var classFile = Path.Join(YokoPaths.CompilerOut, relativePath[..^5] + ".class");

			if(!File.Exists(classFile)) {
				toBuild.Add(sourceFile);
				continue;
			}

			var buildTime = new FileInfo(classFile).LastWriteTime;

			if(srcLastChange > buildTime) toBuild.Add(sourceFile);

		}

		if(toBuild.Count > 0) {

			var compileOK = await compiler.RunWithArgsEcho(
				"-encoding",
				"UTF-8",
				"-cp",
				string.Join(Globals.IsWindows ? ';' : ':', classpath),
				"-sourcepath",
				YokoPaths.SourceDir,
				"-d",
				YokoPaths.CompilerOut,
				string.Join(' ', toBuild)
			);

			if(!compileOK) {
				AnsiConsole.MarkupLine("[red]Build failed.[/]");
				Environment.Exit(compiler.LastExitCode);
			}

		} else {
			Console.WriteLine("up to date, skipping build step.");
		}

		// copy plugin.yml
		File.Copy(Path.Join(YokoPaths.SourceDir, "..", "resources", "plugin.yml"), Path.Join(YokoPaths.CompilerOut, "plugin.yml"), true);

		Console.WriteLine("packing plugin...");

		var packOK = await packager.RunWithArgsEcho("cf", YokoPaths.PluginJar, "-C", YokoPaths.CompilerOut, ".");

		if (!packOK) {
			AnsiConsole.MarkupLine("[red] Build failed.[/]");
			Environment.Exit(packager.LastExitCode);
		}

		// hack for file locking
		var pluginBin = File.ReadAllBytes(YokoPaths.PluginJar);
		var pluginDevServer = new FileStream(YokoPaths.LocalServerPluginJar, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
		pluginDevServer.Write(pluginBin, 0, pluginBin.Length);
		pluginDevServer.Flush();
		pluginDevServer.Close();

		AnsiConsole.MarkupLine("[lime]Build complete.[/]");
	}
}