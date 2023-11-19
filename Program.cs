Directory.CreateDirectory(Yoko.YokoPaths.AppDir); // ensure cache folder exists.
EnsureInstalled.Run();

Globals.HTTP.DefaultRequestHeaders.Add("User-Agent", "DotNetSdk");
await Globals.EnsureJDK();
Globals.TryParseProject();

var help = new HelpAction();

List<ICLIAction> actions = new() {
	new HydrateAction(),
	new InitAction(),
	new BuildAction(),
	new TestAction(),
	new ReinstallAction(),
	new UpdateAction(),
	help
};

if (args.Length == 0) {
	await help.Run(Array.Empty<string>());
	return;
}

var action = actions.Find(a => a.Command.Equals(args[0]));

if (action == null) {
	await help.Run(Array.Empty<string>());
	return;
}

await action.Run(args[1..]);