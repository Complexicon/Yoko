using System.Diagnostics;

namespace Yoko;

public class Subprocess {
	public string Executable { get; }
	
	int lastExitCode = 0;
	public int LastExitCode => lastExitCode; 

	public List<string> LastOutput { get; }
	public List<string> LastError { get; }

	public string WorkingDirectory { get; internal set; }
	public bool PipeStdio { get; internal set; }

	public delegate void LineEventHandler(string line);
	public event LineEventHandler? OnStdOutLine;
	public event LineEventHandler? OnStdErrLine;

	Process? currentProcess;
	public Process? CurrentProcess => currentProcess;

	public bool DontPipeStdin { get; internal set; }

	public Subprocess(string executable) {
		Executable = executable;
		WorkingDirectory = Environment.CurrentDirectory;
		LastError = new();
		LastOutput = new();
	}

	public async Task<bool> RunWithArgsEcho(params string[] args) {
		var startInfo = new ProcessStartInfo {
			FileName = Executable,
			WorkingDirectory = WorkingDirectory,
		};

		foreach(var arg in args) startInfo.ArgumentList.Add(arg);

		currentProcess = Process.Start(startInfo);

		if(currentProcess == null) return false;

		await currentProcess.WaitForExitAsync();

		lastExitCode = currentProcess.ExitCode;
		
		return currentProcess.ExitCode == 0;
	}

	static async Task ProcessStdio(StreamReader reader, List<string> logTarget, LineEventHandler? target) {
		while(!reader.EndOfStream) {
			var line = await reader.ReadLineAsync();
			if(line == null) return;
			logTarget.Add(line);
			target?.Invoke(line);
		}
	}

	byte[] stdoutThroughBuf = Array.Empty<byte>();
	byte[] stderrThroughBuf = Array.Empty<byte>();

	void StdoutThroughHandler(byte[] data) {
		var tmp = new MemoryStream();
		var reader = new StreamReader(tmp);
		tmp.Write(stdoutThroughBuf, 0, stdoutThroughBuf.Length);
		tmp.Write(data, 0, data.Length);
		tmp.Position -= data.Length;

		string? line;

		while((line = reader.ReadLine()) != null) {
			LastOutput.Add(line);
			OnStdOutLine?.Invoke(line);
		}

		stdoutThroughBuf = tmp.ToArray();

	}

	void StderrThroughHandler(byte[] data) {
		var tmp = new MemoryStream();
		var reader = new StreamReader(tmp);
		tmp.Write(stderrThroughBuf, 0, stderrThroughBuf.Length);
		tmp.Write(data, 0, data.Length);

		string? line;

		while((line = reader.ReadLine()) != null) {
			LastError.Add(line);
			OnStdErrLine?.Invoke(line);
		}

		stderrThroughBuf = tmp.ToArray();
	}

	public async Task<bool> RunWithArgs(params string[] args) {
		var startInfo = new ProcessStartInfo {
			FileName = Executable,
			RedirectStandardError = true,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			WorkingDirectory = WorkingDirectory,
		};

		foreach(var arg in args) startInfo.ArgumentList.Add(arg);

		currentProcess = Process.Start(startInfo);

		if(currentProcess == null) return false;

		LastError.Clear();
		LastOutput.Clear();


		if(PipeStdio) {
			Pipe.From(currentProcess.StandardOutput.BaseStream).To(Console.OpenStandardOutput()).Through(StdoutThroughHandler);
			Pipe.From(currentProcess.StandardError.BaseStream).To(Console.OpenStandardError()).Through(StderrThroughHandler);	
			if(!DontPipeStdin) Pipe.From(Console.OpenStandardInput()).To(currentProcess.StandardInput.BaseStream);
		} else {
			_ = ProcessStdio(currentProcess.StandardOutput, LastOutput, OnStdOutLine);
			_ = ProcessStdio(currentProcess.StandardError, LastError, OnStdErrLine);
		}

		await currentProcess.WaitForExitAsync();

		lastExitCode = currentProcess.ExitCode;
		
		return currentProcess.ExitCode == 0;
	}

	internal void TrySignalStop() {
		currentProcess?.Kill();
	}
}
