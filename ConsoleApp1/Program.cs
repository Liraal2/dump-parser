using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            XElement dump = XElement.Load(Path.Combine(Directory.GetCurrentDirectory(), "dump.xml"));
            IEnumerable<string> articles = dump.Descendants().Where(e => e.Name.LocalName == "text").Select(e=>e.Value);

            var ignored = new List<string>{ "File" };
            foreach (string i in ignored) articles = articles.ToList().Select(a => Regex.Replace(a, string.Format(@"\[\[{0}:.+\|", i), ""));
            
            var cleanArticles = articles;
            cleanArticles = cleanArticles.ToList().Select(a => Regex.Replace(a, @"\[\[|\]\]|{.+}|<.+>|\||===.+===", "")+"|");
            File.WriteAllLines("clean.csv", cleanArticles.ToArray());
            
            var markedArticles = articles;
            markedArticles = markedArticles.ToList().Select(a => Regex.Replace(a, @"{.+}", "") + "|");
            var lemmatizer = new LemmaSharp.LemmatizerPrebuiltCompact(LemmaSharp.LanguagePrebuilt.English);
            markedArticles = markedArticles.ToList().Select(a => Regex.Replace(a, @"\[\[.+?\]\]", delegate (Match m)
            {
                return "{"+lemmatizer.Lemmatize(Regex.Replace(m.Value, @"\[\[|\]\]|.*\|", "")) +"}";
            }));
            markedArticles = markedArticles.ToList().Select(a => Regex.Replace(a, @"\[\[|\]\]|<.+>|\||===.+===", "")+"|");
            File.WriteAllLines("marked.csv", markedArticles.ToArray());
        }
    }
}
