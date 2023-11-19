class ReinstallAction : ICLIAction {
	public string Command => "reinstall";

	public Task Run(string[] args) {
		return Task.CompletedTask;
	}
}