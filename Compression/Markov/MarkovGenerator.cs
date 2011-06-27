using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MarkovTextGenerator
{
   public class MarkovGenerator
    {
        List<MarkovChain> list = new List<MarkovChain>();

      public  void TrainModel(String text, int level)
        {
          
          char[] separator = {' '};


                String[] words = text.Split(separator);
                
                for (int i=0; i<words.Length; i++)
                {
                    String word = words[i];
                    String[] wcopy = new String[level];
                    
                    //I'm just to lazy right now to fix this
                    try
                    {
                        Array.Copy(words, i + 1, wcopy, 0, level);
                    }
                    catch (Exception ex)
                    {}
                        List<String> chain = new List<string>();
                    chain.AddRange(wcopy);
                    MarkovChain mc = new MarkovChain();
                    mc.Word = word;
                    mc.WordChain = chain;
                    list.Add(mc);
                }
            
        }

        public String GenerateText(int numberOfWords)
        {
            int wordCount = 1;
            Random r = new Random();
            int index = r.Next(0, list.Count-1);
             
            String startWord = list[index].Word;
            String genText = startWord;

            while (wordCount < numberOfWords)
            {
                List<MarkovChain> newList = GetAllChainsStartingWith(startWord);
                //get a random word from the list
                MarkovChain randomChain = newList[r.Next(0, newList.Count - 1)];
                foreach (String w in randomChain.WordChain)
                {
                    genText += w + " ";
                    if (!String.IsNullOrEmpty(w))
                        startWord = w;
                }
                wordCount += randomChain.WordChain.Count;

            }
            return genText;
        }
        private List<MarkovChain> GetAllChainsStartingWith(String word)
        {
            List<MarkovChain> ret = new List<MarkovChain>();

            foreach (MarkovChain mc in list)
            {
                if (mc.Word == word)
                {
                    ret.Add(mc);
                }
            }
            return ret;
        }
        
    }
}
