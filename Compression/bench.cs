//--------------------------------------------------------------------------------------------
// C# compression algorithms benchmark:
//
// * C# LZF: http://csharplzfcompression.codeplex.com
// * QuickLZ 1.5.0 final: http://quicklz.com
// * DeflateStream: http://msdn.microsoft.com/library/system.io.compression.deflatestream.aspx
// * iROLZ: http://ezcodesample.com/rolz/rolz_article.html
//
// Written Jun 28, 2011 by yallie@yandex.ru 
//--------------------------------------------------------------------------------------------

// csc.exe bench.cs LZF.cs QuickLZ.cs iROLZ.cs MiniLZOPort.cs

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Management;
using Lzf;
using BrainTechLLC;

class Program
{
	const int iterations = 100; //5000;
	const int seed = 1234;

	static void Main()
	{
		SystemInfo.DisplaySysInfo();
		Console.WriteLine("Generating test data...");

		var sample1 = GenerateBinaryData(new Random(seed));
		Console.WriteLine("Binary: {0} bytes.", sample1.Length);

		var sample2 = GenerateTextualData(new Random(seed));
		Console.WriteLine("Textual: {0} bytes.", sample2.Length);

		var sample3 = GenerateUncompressibleData(new Random(seed));
		Console.WriteLine("Uncompressible: {0} bytes.", sample3.Length);

		// save files for reference
		File.WriteAllBytes("bench1.dat", sample1);
		File.WriteAllBytes("bench2.dat", sample2);
		File.WriteAllBytes("bench3.dat", sample3);

		Console.WriteLine();

		// save compressed data and measure speed
		Benchmark("LZF, binary data", iterations, sample1, "bench1.lzf", LzfCompress, LzfDecompress);
		Benchmark("QuickLZ, binary data", iterations, sample1, "bench1.qlz", QuickLZCompress, QuickLZDecompress);
		Benchmark("DeflateStream, binary data", iterations, sample1, "bench1.dfl", DeflateStreamCompress, DeflateStreamDecompress);
		Benchmark("MiniLZO, binary data", iterations, sample1, "bench1.mlz", MiniLzoCompress, MiniLzoDecompress);
//		Benchmark("iROLZ, binary data", iterations, sample1, "bench1.rlz", IrolzCompress, IrolzDecompress);

		Benchmark("LZF, textual data", iterations, sample2, "bench2.lzf", LzfCompress, LzfDecompress);
		Benchmark("QuickLZ, textual data", iterations, sample2, "bench2.qlz", QuickLZCompress, QuickLZDecompress);
		Benchmark("DeflateStream, textual data", iterations, sample2, "bench2.dfl", DeflateStreamCompress, DeflateStreamDecompress);
		Benchmark("MiniLZO, textual data", iterations, sample2, "bench2.mlz", MiniLzoCompress, MiniLzoDecompress);
//		Benchmark("iROLZ, textual data", iterations, sample2, "bench2.rlz", IrolzCompress, IrolzDecompress);

		Benchmark("LZF, uncompressible data", iterations, sample3, "bench3.lzf", LzfCompress, LzfDecompress);
		Benchmark("QuickLZ, uncompressible data", iterations, sample3, "bench3.qlz", QuickLZCompress, QuickLZDecompress);
		Benchmark("DeflateStream, uncompressible data", iterations, sample3, "bench3.dfl", DeflateStreamCompress, DeflateStreamDecompress);
		Benchmark("MiniLZO, uncompressible data", iterations, sample3, "bench3.mlz", MiniLzoCompress, MiniLzoDecompress);
//		Benchmark("iROLZ, uncompressible data", iterations, sample3, "bench3.rlz", IrolzCompress, IrolzDecompress);
	}

	static void Benchmark(string name, int iterations, byte[] inputData, string outFileName, Func<byte[], CompressionResult> compress, Func<CompressionResult, int> decompress)
	{
		Console.WriteLine("Benchmark name: {0}", name);

		var sw = new Stopwatch();
		sw.Start();
		var data = new CompressionResult();

		for (var i = 0; i < iterations; i++)
			data = compress(inputData);

		sw.Stop();
		data.Save(outFileName);

		var speed = iterations * inputData.Length / (sw.Elapsed.TotalMilliseconds > 0 ? sw.Elapsed.TotalMilliseconds : 1) / 1000;
		Console.WriteLine("Compression:    size: {1,6} -> {2,6}, elapsed: {3}, speed: {4:#0.000} mb/s", name, inputData.Length, data.Length, sw.Elapsed, speed);

		sw = new Stopwatch();
		sw.Start();
		var length = 0;

		for (var i = 0; i < iterations; i++)
			length = decompress(data);

		sw.Stop();

		speed = iterations * data.Length / (sw.Elapsed.TotalMilliseconds > 0 ? sw.Elapsed.TotalMilliseconds : 1) / 1000;
		Console.WriteLine("Decompression:  size: {1,6} -> {2,6}, elapsed: {3}, speed: {4:#0.000} mb/s", name, data.Length, length, sw.Elapsed, speed);
		Console.WriteLine();	
	}

	class CompressionResult
	{
		public byte[] Data { get; set; }

		public int Length { get; set; }

		public int SourceLength { get; set; }

		public void Save(string fileName)
		{
			using (var fs = File.Create(fileName))
			{
				fs.Write(Data, 0, Length);
				fs.Close();
			}
		}
	}

	static CompressionResult MiniLzoCompress(byte[] data)
	{
		var output = MiniLZO.Compress(data);
		return new CompressionResult
		{
			Data = output,
			Length = output.Length,
			SourceLength = data.Length
		};
	}

	static int MiniLzoDecompress(CompressionResult data)
	{
		var output = MiniLZO.Decompress(data.Data);
		return output.Length;
	}

	static CompressionResult IrolzCompress(byte[] data)
	{
		var output = iROLZ.IrolzCompress(data);
		return new CompressionResult
		{
			Data = output,
			Length = output.Length,
			SourceLength = data.Length
		};
	}

	static int IrolzDecompress(CompressionResult data)
	{
		var output = iROLZ.IrolzDecompress(data.Data);
		return output.Length;
	}

	static LZF lzf = new LZF();

	static CompressionResult LzfCompress(byte[] data)
	{
		var output = new byte[data.Length * 2];
		var size = lzf.Compress(data, data.Length, output, output.Length);
		return new CompressionResult
		{
			Data = output,
			Length = size,
			SourceLength = data.Length
		};
	}

	static int LzfDecompress(CompressionResult data)
	{
		var output = new byte[data.Length * 2];
		return lzf.Decompress(data.Data, data.Length, output, output.Length);
	}

	static CompressionResult QuickLZCompress(byte[] data)
	{
		var output = QuickLZ.compress(data, 1);
		return new CompressionResult
		{
			Data = output,
			Length = output.Length,
			SourceLength = data.Length
		};
	}

	static int QuickLZDecompress(CompressionResult data)
	{
		var output = QuickLZ.decompress(data.Data);
		return output.Length;
	}

	static CompressionResult DeflateStreamCompress(byte[] data)
	{
		using (var output = new MemoryStream())
		using (var ds = new DeflateStream(output, CompressionMode.Compress))
		{
			ds.Write(data, 0, data.Length);
			ds.Close();

			var outData = output.ToArray();
			return new CompressionResult
			{
				Data = outData,
				Length = outData.Length,
				SourceLength = data.Length
			};
		}
	}

	static int DeflateStreamDecompress(CompressionResult data)
	{
		using (var input = new MemoryStream(data.Data))
		using (var ds = new DeflateStream(input, CompressionMode.Decompress))
		{
			var br = new BinaryReader(ds);
			var output = br.ReadBytes(data.SourceLength + 100);
			return output.Length;
		}
	}

	static byte[] GenerateBinaryData(Random rnd)
	{
		return GenerateData(rnd, 20000, 30, 0, Byte.MaxValue);
	}

	static byte[] GenerateTextualData(Random rnd)
	{
		return Encoding.Default.GetBytes(new MarkovChainGenerator().Generate(rnd, 100000));
	}

	static byte[] GenerateUncompressibleData(Random rnd)
	{
		return GenerateData(rnd, 200000, 2, 0, Byte.MaxValue);
	}

	static byte[] GenerateData(Random rnd, int iterations, int maxRepeats, byte min, byte max)
	{
		var result = new List<byte>();

		for (var i = 0; i < iterations; i++)
		{
			var c = min + rnd.Next(max);

			for (var j = 0; j < rnd.Next(maxRepeats); j++)
				result.Add((byte)c);
		}
		
		return result.ToArray();
	}

	// Thanks to Visar Shehu for an easy Markov chain example code:
	// http://phalanx.spartansoft.org/2010/03/30/markov-chain-generator-in-c/
	class MarkovChainGenerator
	{
		class MarkovChain
		{
			public char Letter { get; set; }

			public List<char> Chain { get; set; }

			public static string // some pseudo-english text generated from Christmas Carol by Charles Dickens 
				Sample = @"enbere, vers i ther to y bro the gence, and or eep t tur theavin lover ppearised of die?`
				`th `a maden mive one s yould be led thy, by thite, whorteror tay use ting, and bled pheart per a ll,
				had he s saits h airoad is t or up hed at hith` saittle wome nenbermer of and th, it moiceso rundrept 
				ttle the scrow!` was knowithdvancheers tore humbuch ful r abojecte and sll than be to be to hat ht ha 
				verades, thich and by t he a non. ` `doke unfas, re wasaid eleciden air a d in steand s, wheir for up
				were, butributenburyiced fielowed dond with aid, fros so hear donistrupte of foug usutsethat overy bry 
				tour d pull mays the causearthe sanothis stmas exand, to d, hather wouch gnitil thow ws of gled thing `
				is wheriarssocid sck hing,` `hey's, colow hin; feas car he `even sundress ccepting to ympatartlk-er; 
				for, wento man yo man oldistrson do n ther obe aber irits is his mrs. somet; orsost he un, an't sclais 
				fable havendin wit.` `why doorooge thoment as the had to th here ile oined in genily us ma".Replace("`", "\"");
		}

		List<MarkovChain> chains = new List<MarkovChain>();

		public MarkovChainGenerator()
		{
			Train(MarkovChain.Sample, 4);
		}

		public void Train(String text, int level)
		{
			text = Regex.Replace(text, @"\s+", " ").ToLower();

			for (var i = 0; i < text.Length - level - 1; i++)
			{
				var c = text[i];
				var chain = new List<char>();
    
            for (var j = 0; j < level; j++)
            	chain.Add(text[j + i + 1]);

				chains.Add(new MarkovChain
				{
					Letter = c,
					Chain = chain
				});
			}
		}

		public string Generate(Random rnd, int numChars)
		{
			var sb = new StringBuilder();
			var index = rnd.Next(chains.Count);
			var startChar = chains[index].Letter;

			while (sb.Length < numChars)
			{
				var list = chains.Where(c => c.Letter == startChar).ToList();
				var rndChain = list[rnd.Next(list.Count)];

				foreach (char c in rndChain.Chain)
				{
					sb.Append(c);
					startChar = c;
				}
			}

			return sb.ToString();
		}
	}

	public class SystemInfo
	{
		public string CpuName { get; private set; }

		public string OperatingSystem { get { return Environment.OSVersion.VersionString; } }

		public bool Is64Bit { get { return Environment.Is64BitProcess; } }

		public SystemInfo()
		{
			var searcher = new ManagementObjectSearcher("select * from Win32_Processor");
			foreach (ManagementObject mo in searcher.Get())
			{
				CpuName = Regex.Replace(mo["Name"].ToString(), @"\s+", " ").Trim();
				break;
			}
		}

		public static void DisplaySysInfo()
		{
			var si = new SystemInfo();
			Console.WriteLine("Cpu: {0}", si.CpuName);
			Console.WriteLine("Operating System: {0}", si.OperatingSystem);
			Console.WriteLine("Running in 64-bit process: {0}", si.Is64Bit ? "yes" : "no");
			Console.WriteLine();
		}
	}
}
