using Spectre.Console;
using static System.Net.WebRequestMethods;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Formats.Tar;
using Yoko;

class JDKHelper {

	async static Task<(Stream, bool, string)> DownloadJDK(int version) {
		var builds = JsonNode.Parse(await Globals.HTTP.GetStringAsync($"https://api.adoptium.net/v3/assets/latest/{version}/hotspot?image_type=jdk&vendor=eclipse"))?.AsArray();

		if (builds == null) {
			// TODO: HANDLE
			return (null, false, "");
		}

		var jdkPackageInfo = builds.First(v => {
			bool osCheck = false;

			string os = ((string?)v["binary"]?["os"]) ?? "";
			string arch = ((string?)v["binary"]?["architecture"]) ?? "";

			if (os == "mac") osCheck = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
			else if (os == "windows") osCheck = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
			else if (os == "linux") osCheck = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

			bool archCheck = false;

			if (arch == "x64") archCheck = RuntimeInformation.OSArchitecture == Architecture.X64;
			else if (arch == "aarch64") archCheck = RuntimeInformation.OSArchitecture == Architecture.Arm64;
			else if (arch == "x86") archCheck = RuntimeInformation.OSArchitecture == Architecture.X86;
			else if (arch == "arm") archCheck = RuntimeInformation.OSArchitecture == Architecture.Arm;

			return osCheck && archCheck;
		});


		string? link = jdkPackageInfo?["binary"]?["package"]?["link"]?.ToString();
		string? name = jdkPackageInfo?["binary"]?["package"]?["name"]?.ToString();

		return (new MemoryStream(await Globals.DownloadFile(link, name)), name.EndsWith("tar.gz"), jdkPackageInfo?["release_name"]?.ToString());
	}

	static void Unpack(Stream data, string path, bool isGzip) { /*, out string baseDir*/
		AnsiConsole.MarkupLine("[teal]Unpacking JDK...[/]");

		if(isGzip) {
			TarFile.ExtractToDirectory(new GZipStream(data, CompressionMode.Decompress), path, true);
			//baseDir = ""; // TODO
		} else {
			var archive = new ZipArchive(data);
			//baseDir = archive.Entries[0].FullName;
			archive.ExtractToDirectory(path);
		}

	}

	public static async Task<string> InstallJDK8() {
		var tmpdir = Path.GetTempPath();
		var (stream, isGzip, baseDir) = await DownloadJDK(8);
		Unpack(stream, tmpdir, isGzip);
		var finPath = Path.Join(YokoPaths.AppDir, "jdk8");
		Directory.Move(Path.Join(tmpdir, baseDir), finPath);
		return finPath;
	}

	public static async Task<string> InstallJDK17() {
		var tmpdir = Path.GetTempPath();
		var (stream, isGzip, baseDir) = await DownloadJDK(17);
		Unpack(stream, tmpdir, isGzip);
		var finPath = Path.Join(YokoPaths.AppDir, "jdk17");
		Directory.Move(Path.Join(tmpdir, baseDir), finPath);
		return finPath;
	}

}