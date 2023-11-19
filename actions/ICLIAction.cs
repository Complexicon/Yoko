interface ICLIAction {
	public abstract string Command { get; }
	public abstract Task Run(string[] args);
}