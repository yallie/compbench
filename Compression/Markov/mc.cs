using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

class Program
{
	static void Main()
	{
		string textFile = "carol-gen.txt";
		string outFile = "carol-gen2.txt";

		// train
		var text = File.ReadAllText(textFile);
		var gen = new MarkovChainGenerator();
		gen.Train(text, 4);

		// generate
		text = gen.Generate(new Random(1234), 100000);
		File.WriteAllText(outFile, text);
	}

	class MarkovChainGenerator
	{
		class MarkovChain
		{
			public char Letter { get; set; }

			public List<char> Chain { get; set; }

			// some pseudo-english text
			public const string Sample = "enbere, vers i ther to y bro the gence, and or eep t tur theavin lover ppearised of die?\" " +
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
				var list = GetChainsStartingWith(startChar);
				var rndChain = list[rnd.Next(list.Count)];

				foreach (char c in rndChain.Chain)
				{
					sb.Append(c);
					startChar = c;
				}
			}

			return sb.ToString();
		}

		public List<MarkovChain> GetChainsStartingWith(char startChar)
		{
			return chains.Where(c => c.Letter == startChar).ToList();
		}
	}
}

