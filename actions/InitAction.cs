using System.Text.Json.Nodes;
using System.Xml.Linq;
using Spectre.Console;
using Yoko.Templates;

class InitAction : ICLIAction {
	public string Command => "init";


	public async Task Run(string[] args) {

		var doReinit =
			(File.Exists("project.json") && AnsiConsole.Confirm($"[yellow]It looks like the Project [teal]'{Globals.Config?.Name}'[/] is Already Present. Do you want to Reinitialize your Project?[/]", false)) || 
			(Directory.Exists("src") && AnsiConsole.Confirm($"[yellow]It looks like source code is already present. Are you sure you want to Reinitialize your Project?[/]", false));

		if(doReinit) {
			try { Directory.Delete("src", true); } catch { }
			try { File.Delete("project.json"); } catch { }
		}

		var spigotVersions = JsonNode.Parse(await Globals.HTTP.GetStreamAsync("https://api.papermc.io/v2/projects/paper"))?
			["versions"].AsArray()
			.Select(v => v.ToString())
			.Where(v => !v.Contains('-'))
			.Select(Version.Parse)
			.ToArray();

		//var spigotVersions =
		//	XDocument.Load(await Globals.HTTP.GetStreamAsync(VersionURL))
		//	.Element("metadata")?
		//	.Element("versioning")?
		//	.Element("versions")?
		//	.Descendants()
		//	.Select(v => v.Value.Replace("-R0.1-SNAPSHOT", ""))
		//	.Where(v => !v.Contains('-'))
		//	.Select(Version.Parse)
		//	.ToArray();

		var spigotVersion = AnsiConsole.Prompt(
			new SelectionPrompt<Version>()
			.Title("Choose your desired [green]Minecraft Version[/]")
			.PageSize(10)
			.MoreChoicesText("[grey](Move up and down to reveal more)[/]")
			.AddChoices(spigotVersions)
		);

		var name = AnsiConsole.Ask<string>("[teal]Whats the name of your Plugin?[/] :");
		var groupID = AnsiConsole.Ask<string>("[teal]Whats your GroupID?[/] [gray](example: dev.cmplx)[/] :");

		var pluginYml = Templates.plugin
			.Replace("${NAME}", name)
			.Replace("${GROUP}", groupID)
			.Replace("${MCVERSION}", spigotVersion.ToString(2));

		var mainJava = Templates.Main
			.Replace("${NAME}", name)
			.Replace("${GROUP}", groupID);

		var resDir = Directory.CreateDirectory(Path.Join("src", "main", "resources"));
		File.WriteAllText(Path.Join(resDir.FullName, "plugin.yml"), pluginYml);

		var srcDir = Directory.CreateDirectory(Path.Join("src", "main", "java", Path.Join(groupID.Split('.')), name));
		File.WriteAllText(Path.Join(srcDir.FullName, "Main.java"), mainJava);

		File.WriteAllText("project.json", new JsonObject {
			["name"] = name,
			["minecraftVersion"] = spigotVersion.ToString(),
			["mvnRepos"] = new JsonArray(),
			["dependencies"] = new JsonArray(),
		}.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

		Globals.TryParseProject();

		await new HydrateAction().Run(Array.Empty<string>());
	}
}