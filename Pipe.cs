namespace Yoko {
	internal class Pipe {
		private readonly Stream input;
		private Stream? output;
		private Action<byte[]>? through;

		async void ReadLoop() {
			byte[] buffer = new byte[1024];

			while(input.CanRead) {
				int readAmt = await input.ReadAsync(buffer);

				if (readAmt == 0) { // 0 bytes read = end of stream
					output?.Close();
					break;
				}

				through?.Invoke(buffer[..readAmt]);

				if (output == null) continue;

				await output.WriteAsync(buffer, 0, readAmt).ContinueWith((r) => output.Flush());
			}

		}

		private Pipe(Stream input) {
			this.input = input;
			ReadLoop();
		}

		public static Pipe From(Stream input) {
			return new Pipe(input);
		}

		public Pipe Through(Action<byte[]> handler) {
			through = handler;
			return this;
		}

		public Pipe To(Stream output) {
			this.output = output;
			return this;
		}

	}
}
