using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Spectre.Console;
using Yoko;

class Globals {

	static readonly HttpClient httpClient = new();

	public static HttpClient HTTP => httpClient;

	public static string AppName => "yoko";

	public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	static string jdk8Path = null;
	static string jdk17Path = null;

	public static string JDK8Path => jdk8Path;
	public static string JDK17Path => jdk17Path;

	public class ProjectConfig {
		public required string Name;
		public required Version MinecraftVersion;
		public required List<string> MavenRepos;
		public required List<string> Dependencies;
	}

	static ProjectConfig? cfg;

	public static ProjectConfig? Config => cfg;

	public static async Task<byte[]> DownloadFile(string url, string title) {
		var resp = await HTTP.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
		resp.EnsureSuccessStatusCode();
		byte[] buffer = new byte[(int)resp.Content.Headers.ContentLength];

		int read = 0;

		await AnsiConsole.Progress()
			.Columns(new ProgressColumn[] {
				new TaskDescriptionColumn(),    // Task description
				new ProgressBarColumn(),        // Progress bar
				new PercentageColumn(),         // Percentage
				new RemainingTimeColumn(),      // Remaining time
				new SpinnerColumn(),            // Spinner
			})
			.StartAsync(async context => {
				var task = context.AddTask($"[teal]{title}[/]", true, (double)resp.Content.Headers.ContentLength);

				var stream = resp.Content.ReadAsStream();

				while (true) {
					int bytesRead = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
					if (bytesRead == 0) {
						return;
					}

					read += bytesRead;
					task.Increment(bytesRead);
				}

			});

		return buffer;

	}

	public async static Task EnsureJDK() {

		if (Directory.Exists(Path.Join(YokoPaths.AppDir, "jdk8"))) jdk8Path = Path.Join(YokoPaths.AppDir, "jdk8");
		if (Directory.Exists(Path.Join(YokoPaths.AppDir, "jdk17"))) jdk17Path = Path.Join(YokoPaths.AppDir, "jdk17");
		if(jdk8Path != null && jdk17Path != null) return; // use jdk provided by yoko

		var jdkPaths = Helpers.GetFullPath("java");

		if(jdkPaths != null) { // JDK8 and JDK17 aren't installed

			foreach (var path in jdkPaths) {
				var p = Process.Start(new ProcessStartInfo { FileName = path, Arguments = "-version", RedirectStandardOutput = true, RedirectStandardError = true });
				p?.WaitForExit();

				var verString = p.StandardError.ReadLine() ?? "";

				if (!verString.ToLower().Contains("jdk")) continue; // this path isn't a jdk

				if (verString.Contains("\"1.8.")) { // oldschool version string
					jdk8Path = Path.GetFullPath(Path.Join(path, "..", ".."));
					continue; // JDK8 Is installed
				}

				var version = Version.Parse(verString[(verString.IndexOf('"') + 1)..verString.LastIndexOf('"')]);
				if (version.Major >= 17) {
					jdk17Path = Path.GetFullPath(Path.Join(path, "..", ".."));
					continue; // JDK17 Is installed
				}

			}

		}

		if(jdk8Path == null) {
			if (!AnsiConsole.Confirm("[yellow]JDK 8 was not detected. Do you want to install it automatically?[/]")) {
				AnsiConsole.MarkupLine("[fuchsia]Please visit [underline blue]https://adoptium.net/[/] to download it yourself.[/]");
				Environment.Exit(0);
			}

			jdk8Path = await JDKHelper.InstallJDK8();
		}

		if(jdk17Path == null) {
			if (!AnsiConsole.Confirm("[yellow]JDK 17 was not detected. Do you want to install it automatically?[/]")) {
				AnsiConsole.MarkupLine("[fuchsia]Please visit [underline blue]https://adoptium.net/[/] to download it yourself.[/]");
				Environment.Exit(0);
			}

			jdk17Path = await JDKHelper.InstallJDK17();
		}
	}

	public static void TryParseProject() {
		if (!File.Exists("project.json")) return;


		var jsonCfg = JsonNode.Parse(File.ReadAllText("project.json"));

		cfg = new() { // dirty ahh parsing
			Name = jsonCfg?["name"]?.ToString() ?? "",
			MinecraftVersion = Version.Parse(jsonCfg?["minecraftVersion"]?.ToString()),
			MavenRepos = jsonCfg?["mvnRepos"].AsArray().Select(v => v.ToString()).ToList(),
			Dependencies = jsonCfg?["dependencies"].AsArray().Select(v => v.ToString()).ToList(),
		};

	}

	public static async Task<string> GetServerPath(Version v) {
		var dir = Directory.CreateDirectory(YokoPaths.ServerJarDir);
		var checkPath = Path.Join(dir.FullName, $"server-{v}.jar");
		if (File.Exists(checkPath)) return checkPath;

		AnsiConsole.Markup("[yellow]A server with this Minecraft version is not cached! Downloading it...[/]");

		var latestBuild = JsonNode.Parse(await HTTP.GetStreamAsync($"https://api.papermc.io/v2/projects/paper/versions/{v}"))?["builds"]?[0]?.ToString();
		var fileName = JsonNode.Parse(await HTTP.GetStreamAsync($"https://api.papermc.io/v2/projects/paper/versions/{v}/builds/{latestBuild}"))?["downloads"]?["application"]?["name"]?.ToString();

		File.WriteAllBytes(checkPath, await DownloadFile($"https://api.papermc.io/v2/projects/paper/versions/{v}/builds/{latestBuild}/downloads/{fileName}", fileName));

		return await GetServerPath(v);
	}

	static readonly Version JDK8Supported = Version.Parse("1.16.5");

	public static bool IsJDK8(Version v) {
		return v <= JDK8Supported;
	}

	public static async Task<(string, string)> GetAPIPath(Version v) {
		var dir = Directory.CreateDirectory(YokoPaths.APIJarDir);
		var checkPath = Path.Join(dir.FullName, $"api-{v}.jar");
		var checkPathSrc = Path.Join(dir.FullName, $"src-{v}.jar");
		if (File.Exists(checkPath) && File.Exists(checkPathSrc)) return (checkPath, checkPathSrc);

		AnsiConsole.Markup("[yellow]The APIs for this version are not cached! Downloading them...[/]");

		var mvnUrl = $"https://hub.spigotmc.org/nexus/content/repositories/snapshots/org/spigotmc/spigot-api/{v}-R0.1-SNAPSHOT/maven-metadata.xml";

		var fileName = XDocument.Load(await HTTP.GetStreamAsync(mvnUrl))
			.Element("metadata")?
			.Element("versioning")?
			.Element("snapshotVersions")?
			.Descendants()?
			.Where(v => v.Element("extension")?.Value == "jar" && v.Element("classifier") == null)?
			.First()?
			.Element("value")?
			.Value;

		bool shaded = true;//v <= new Version(1, 11, 2);

		var apiUrl = $"https://hub.spigotmc.org/nexus/content/repositories/snapshots/org/spigotmc/spigot-api/{v}-R0.1-SNAPSHOT/spigot-api-{fileName}{(shaded ? "-shaded" : "")}.jar";
		var apiSrcUrl = $"https://hub.spigotmc.org/nexus/content/repositories/snapshots/org/spigotmc/spigot-api/{v}-R0.1-SNAPSHOT/spigot-api-{fileName}-sources.jar";

		File.WriteAllBytes(checkPath, await DownloadFile(apiUrl, $"spigot-api-{v}.jar"));
		File.WriteAllBytes(checkPathSrc, await DownloadFile(apiSrcUrl, $"spigot-srcdoc-{v}.jar"));

		return await GetAPIPath(v);
	}
}