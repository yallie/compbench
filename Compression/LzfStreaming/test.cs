// csc test.cs lzf.cs

using System;
using System.IO;
using Lzf;

class Program
{
	static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Compress();
		}
		else
		{
			Decompress();
		}
	}

	private const int BUFFER_SIZE = 8192;

	private const int SHORT_SIZE = sizeof(short);

	static void Compress()
	{
		using (var inputFile = File.OpenRead("bench.dat"))
		using (var outputFile = File.Create("bench.lzf"))
		{
			var buffer = new byte[BUFFER_SIZE];
			var output = new byte[buffer.Length * 2];
			var lzf = new LZF();

			while (true)
			{
				var readCount = (short)inputFile.Read(buffer, 0, buffer.Length);
				if (readCount == 0)
				{
					break;
				}

				var writeCount = (short)lzf.Compress(buffer, readCount, output, output.Length);
				if (writeCount == 0)
				{
					throw new InvalidOperationException("Cannot compress input stream.");
				}

				// source size
				var temp = BitConverter.GetBytes(readCount);
				outputFile.Write(temp, 0, SHORT_SIZE);

				// destination size
				temp = BitConverter.GetBytes(writeCount);
				outputFile.Write(temp, 0, SHORT_SIZE);

				// data chunk
				outputFile.Write(output, 0, writeCount);
			}
		}
	}

	static void Decompress()
	{
		using (var inputFile = File.OpenRead("bench.lzf"))
		using (var outputFile = File.Create("bench.out"))
		{
			var buffer = new byte[BUFFER_SIZE * 2];
			var output = new byte[BUFFER_SIZE];
			var temp = new byte[SHORT_SIZE * 2];
			var lzf = new LZF();

			while (true)
			{
				// read chunk sizes
				if (inputFile.Read(temp, 0, SHORT_SIZE * 2) == 0)
				{
					break;
				}

				var sourceSize = BitConverter.ToInt16(temp, 0);
				var destSize = BitConverter.ToInt16(temp, SHORT_SIZE);

				var readCount = inputFile.Read(buffer, 0, destSize);
				if (readCount != destSize)
				{
					throw new InvalidOperationException("Cannot read input stream.");
				}

				var writeCount = lzf.Decompress(buffer, readCount, output, output.Length);
				if (writeCount != sourceSize)
				{
					throw new InvalidOperationException("Cannot decompress input stream.");
				}

				outputFile.Write(output, 0, writeCount);
			}
		}
	}
}