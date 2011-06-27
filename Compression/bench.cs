// Written by Y [28-06-11]
// csc bench.cs LZF.cs QuickLZ.cs 

// Generating test data...
// Binary: 111190 bytes.
// Textual: 100000 bytes.
// Compression: LZF-binary, size: 111190 -> 74333, elapsed: 00:00:22.1782598
// Compression: QuickLZ-binary, size: 111190 -> 85842, elapsed: 00:00:16.0261312
// Compression: LZF-text, size: 100000 -> 71201, elapsed: 00:00:20.2352268
// Compression: QuickLZ-text, size: 100000 -> 59138, elapsed: 00:00:08.1507199

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Lzf;

class Program
{
	const int iterations = 5000;

	static void Main()
	{
		Console.WriteLine("Generating test data...");

		var sample1 = GeneratePseudoBinaryData(new Random(1234));
		Console.WriteLine("Binary: {0} bytes.", sample1.Length);

		var sample2 = GeneratePseudoTextualData(new Random(1234));
		Console.WriteLine("Textual: {0} bytes.", sample2.Length);

		File.WriteAllBytes("bench1.dat", sample1);
		File.WriteAllBytes("bench2.dat", sample2);

		Benchmark("LZF-binary", iterations, sample1, LzfCompress, LzfDecompress);
		Benchmark("QuickLZ-binary", iterations, sample1, QuickLZCompress, QuickLZDecompress);

		Benchmark("LZF-text", iterations, sample2, LzfCompress, LzfDecompress);
		Benchmark("QuickLZ-text", iterations, sample2, QuickLZCompress, QuickLZDecompress);
	}

	static LZF lzf = new LZF();

	static byte[] LzfCompress(byte[] data)
	{
		var output = new byte[data.Length * 2];
		var size = lzf.Compress(data, data.Length, output, output.Length);
		var dest = new byte[size];
		Array.Copy(output, dest, size);
		return dest;
	}

	static byte[] QuickLZCompress(byte[] data)
	{
		return QuickLZ.compress(data, 1);
	}

	static void LzfDecompress(byte[] data)
	{
		// todo
	}

	static void QuickLZDecompress(byte[] data)
	{
		// todo
	}

	static void Benchmark(string name, int iterations, byte[] inputData, Func<byte[], byte[]> compress, Action<byte[]> decompress)
	{
		var sw = new Stopwatch();
		sw.Start();
		byte[] data = new byte[0];

		for (var i = 0; i < iterations; i++)
			data = compress(inputData);

		sw.Stop();
		Console.WriteLine("Compression: {0}, size: {1} -> {2}, elapsed: {3}", name, inputData.Length, data.Length, sw.Elapsed);
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

	static byte[] GeneratePseudoBinaryData(Random rnd)
	{
		return GenerateData(rnd, 20000, 30, 0, Byte.MaxValue);
	}

	static byte[] GeneratePseudoTextualData(Random rnd)
	{
		return Encoding.Default.GetBytes(new MarkovChainGenerator().Generate(rnd, 100000));
	}

	// Thanks to Visar Shehu for an easy Markov chain example code:
	// http://phalanx.spartansoft.org/2010/03/30/markov-chain-generator-in-c/
	class MarkovChainGenerator
	{
		class MarkovChain
		{
			public char Letter { get; set; }

			public List<char> Chain { get; set; }

			public const // some pseudo-english text
				string Sample = "enbere, vers i ther to y bro the gence, and or eep t tur theavin lover ppearised of die?\" " +
				"\"th \"a maden mive one s yould be led thy, by thite, whorteror tay use ting, and bled pheart per a ll," +
				"had he s saits h airoad is t or up hed at hith\" saittle wome nenbermer of and th, it moiceso rundrept " +
				"ttle the scrow!\" was knowithdvancheers tore humbuch ful r abojecte and sll than be to be to hat ht ha " +
				"verades, thich and by t he a non. \" \"doke unfas, re wasaid eleciden air a d in steand s, wheir for up" +
				"were, butributenburyiced fielowed dond with aid, fros so hear donistrupte of foug usutsethat overy bry " +
				"tour d pull mays the causearthe sanothis stmas exand, to d, hather wouch gnitil thow ws of gled thing \"" +
				"is wheriarssocid sck hing,\" \"hey's, colow hin; feas car he \"even sundress ccepting to ympatartlk-er; " +
				"for, wento man yo man oldistrson do n ther obe aber irits is his mrs. somet; orsost he un, an't sclais " +
				"fable havendin wit.\" \"why doorooge thoment as the had to th here ile oined in genily us ma";
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
}

