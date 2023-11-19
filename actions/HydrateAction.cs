using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Spectre.Console;
using Spectre.Console.Rendering;
using Yoko;
using Yoko.Templates;

class HydrateAction : ICLIAction {
	public string Command => "hydrate";

	[RequiresUnreferencedCode("Calls System.Text.Json.Nodes.JsonObject.JsonObject(JsonNodeOptions?)")]
	public async Task Run(string[] args) {

		if (Globals.Config == null) {
			AnsiConsole.MarkupLine("[red]No Project Present![/]");
			return;
		}

		AnsiConsole.MarkupLine($"[lime]Hydrating Project [fuchsia]{Globals.Config.Name}[/] for Minecraft [blue]{Globals.Config.MinecraftVersion}[/][/]...");

		var serverPath = await Globals.GetServerPath(Globals.Config.MinecraftVersion);
		var (apiPath, srcPath) = await Globals.GetAPIPath(Globals.Config.MinecraftVersion);

		var targetJDK = Globals.IsJDK8(Globals.Config.MinecraftVersion) ? Globals.JDK8Path : Globals.JDK17Path;
		var javaExecutable = Path.Join(targetJDK, "bin", "java" + (Globals.IsWindows ? ".exe" : ""));

		if (Directory.Exists(YokoPaths.LocalAppDir)) {
			Directory.Delete(YokoPaths.LocalAppDir, true);
		}

		var serverDir = Directory.CreateDirectory(YokoPaths.LocalServer);

		AnsiConsole.MarkupLine("[grey]Setting up Development Server...[/]");

		if (!AnsiConsole.Confirm("[yellow]Do you agree to the Minecraft EULA? (read at [underline blue]https://account.mojang.com/documents/minecraft_eula[/])[/]")) {
			AnsiConsole.MarkupLine("[red]You need to agree to the EULA to use the Development Server[/]");
			Environment.Exit(0);
		}

		File.WriteAllText(Path.Join(serverDir.FullName, "eula.txt"), "eula=true");

		var java = new Subprocess(javaExecutable) {
			WorkingDirectory = serverDir.FullName,
			PipeStdio = true,
			DontPipeStdin = true
		};

		java.OnStdOutLine += (string line) => {
			if (line.Contains("For help, type \"help\""))
				java.CurrentProcess?.StandardInput.WriteLine("stop");
		};

		await java.RunWithArgs("-jar", serverPath, "nogui");

		AnsiConsole.MarkupLine("[grey]Configuring VSCode...[/]");

		var vscodeDir = Directory.CreateDirectory(".vscode");

		JsonObject? settings;
		var settingsFile = Path.Join(vscodeDir.FullName, "settings.json");

		if (File.Exists(settingsFile)) {
			settings = JsonNode.Parse(File.ReadAllText(settingsFile))?.AsObject();
		} else {
			settings = JsonNode.Parse(Templates.vscode_settings)?.AsObject();
		}

		settings["java.project.referencedLibraries"] = new JsonObject {
			["include"] = new JsonArray { apiPath },
			["sources"] = new JsonObject {
				[apiPath] = srcPath
			}
		};

		// TODO: dependencies.

		var runtimes = new JsonArray { null };

		if (Globals.IsJDK8(Globals.Config.MinecraftVersion)) runtimes[0] = new JsonObject { ["name"] = "JavaSE-1.8", ["path"] = Globals.JDK8Path };
		else if (Globals.Config.MinecraftVersion <= Version.Parse("1.17.1")) runtimes[0] = new JsonObject { ["name"] = "JavaSE-16", ["path"] = Globals.JDK17Path };
		else runtimes[0] = new JsonObject { ["name"] = "JavaSE-17", ["path"] = Globals.JDK17Path};

		settings["java.configuration.runtimes"] = runtimes;

		File.WriteAllText(settingsFile, settings.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

		var tasksTemplate = JsonNode.Parse(Templates.vscode_tasks);
		tasksTemplate["tasks"][0]["command"] = $"\"{Environment.ProcessPath}\" test";

		File.WriteAllText(Path.Join(vscodeDir.FullName, "launch.json"), Templates.vscode_launch);
		File.WriteAllText(Path.Join(vscodeDir.FullName, "tasks.json"), tasksTemplate.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

		await new BuildAction().Run(Array.Empty<string>());

		AnsiConsole.MarkupLine("[lime]Your Project is now fully configured![/]");
	}
}
