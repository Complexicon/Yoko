namespace Yoko;

public class YokoPaths {

	static string DotDir = $".{Globals.AppName}";

	public static string AppDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DotDir);
	public static string LocalAppDir => Path.Combine(Environment.CurrentDirectory, DotDir);

	public static string APIJarDir => Path.Combine(AppDir, "api");
	public static string ServerJarDir => Path.Combine(AppDir, "server");

	public static string LocalServer => Path.Combine(LocalAppDir, "server");
	public static string CompilerOut => Path.Combine(LocalAppDir, "classes");
	public static string PluginJar => Path.Combine(LocalAppDir, "plugin.jar");
	public static string LocalServerPluginJar => Path.Combine(LocalServer, "plugins", "plugin.jar");
	public static string SourceDir => Path.Combine(Environment.CurrentDirectory, "src", "main", "java");

}
