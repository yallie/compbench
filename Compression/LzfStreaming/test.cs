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

	private static int bufferSize = 200; //5678; //1234;

	private const int INT_SIZE = sizeof(System.Int32);

	static void Compress()
	{
		using (var inputFile = File.OpenRead("bench.dat"))
		using (var outputFile = File.OpenWrite("bench.lzf"))
		{
			var buffer = new byte[bufferSize];
			var output = new byte[buffer.Length * 2];
			var lzf = new LZF();
			var count = (int)inputFile.Length;

			while (count > 0)
			{
				var readCount = inputFile.Read(buffer, 0, buffer.Length);
				if (readCount == 0)
				{
					throw new InvalidOperationException("Cannot read input stream.");
				}

				var writeCount = lzf.Compress(buffer, readCount, output, output.Length);
				if (writeCount == 0)
				{
					throw new InvalidOperationException("Cannot compress input stream.");
				}

				// source size
				var temp = BitConverter.GetBytes(readCount);
				outputFile.Write(temp, 0, INT_SIZE);

				// destination size
				temp = BitConverter.GetBytes(writeCount);
				outputFile.Write(temp, 0, INT_SIZE);

				// data chunk
				outputFile.Write(output, 0, writeCount);
				count -= readCount;
			}
		}
	}

	static void Decompress()
	{
		using (var inputFile = File.OpenRead("bench.lzf"))
		using (var outputFile = File.OpenWrite("bench.out"))
		{
			var lzf = new LZF();
			var count = (int)inputFile.Length;
			var temp = new byte[INT_SIZE * 2];

			while (count > 0)
			{
				inputFile.Read(temp, 0, INT_SIZE * 2); 
				var sourceSize = BitConverter.ToInt32(temp, 0);
				var destSize = BitConverter.ToInt32(temp, INT_SIZE);

				var buffer = new byte[destSize];
				var readCount = inputFile.Read(buffer, 0, destSize);
				if (readCount != destSize)
				{
					throw new InvalidOperationException("Cannot read input stream.");
				}

				var output = new byte[sourceSize + 10];
				var writeCount = lzf.Decompress(buffer, readCount, output, output.Length);
				if (writeCount != sourceSize)
				{
					throw new InvalidOperationException("Cannot decompress input stream.");
				}

				outputFile.Write(output, 0, writeCount);
				count -= (readCount + INT_SIZE * 2);
			}
		}
	}
}