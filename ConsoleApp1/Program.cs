using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading;

namespace ConsoleApp1
{
    class Program
    {
        public static string article { get; private set; }

        static void Main(string[] args)
        {
            XElement dump = XElement.Load(Path.Combine(Directory.GetCurrentDirectory(), "dump.xml"));
            IEnumerable<string> articles = dump.Descendants().Where(e => e.Name.LocalName == "text").Select(e=>e.Value);//.ToList().GetRange(0,3);

            var ignored = new List<string>{ "File", "Wikipedia", "Category", "Special", "Image", "User",
                "http", "https", "template","talk", "help", "module", "mediaWiki", "jpg", "px", "span" };
            foreach (string i in ignored) articles = articles.ToList().Select(a => Regex.Replace(a, string.Format(@"\[\[{0}:.+\|", i), ""));

            var alsoIgnored = new List<string>{ "Category" };
            foreach (string i in alsoIgnored) articles = articles.ToList().Select(a => Regex.Replace(a, string.Format(@"\[\[{0}.+?\]\]", i), ""));
            var temp = articles.ToArray();
            var thread = new Thread(() => {
                var cleanArticles = articles;
                cleanArticles = cleanArticles.ToList().Select(a => Regex.Replace(a, @"\[\[.+?\]\]\w*|'''.+'''|''.+''", delegate (Match m)
                {
                    var val = Regex.Replace(m.Value, @"\[\[", "").Split(new char[]{'|'},System.StringSplitOptions.RemoveEmptyEntries).First();
                    var fragments = val.Split(new string[]{"]]" },System.StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    return fragments == null || fragments.Length == 0 ? "" : fragments.Replace(".", "<|>");
                }));
                cleanArticles = cleanArticles.ToList().Select(a =>
                    Regex.Replace(a, @"\[\[|\]\]|{.+}|<.+>|===.+===|==.+==|\r|\n|\r\n|,|\(|\)|-|&lt;.*&gt;|\[http.*\]|'''|''|\*", ""));
                cleanArticles = cleanArticles.ToList().Select(a => Regex.Replace(a, @"\.(?=[0-9])", "<")+'|');
                var cleanArticlesArray = string.Join("", cleanArticles.ToArray()).Split('.');
                for (int i = 0; i < cleanArticlesArray.Length; i++) cleanArticlesArray[i] = cleanArticlesArray[i].Replace("<|>", ".");
                File.WriteAllLines("clean.csv", cleanArticlesArray);
            });
            thread.Start();

            var markedArticles = articles;
            markedArticles = markedArticles.ToList().Select(a => Regex.Replace(a, @"{.+}", "") + "|");
            var lemmatizer = new LemmaSharp.LemmatizerPrebuiltCompact(LemmaSharp.LanguagePrebuilt.English);

            var linkForms = Enumerable.Range(0,10).Select((i)=>new HashSet<string>()).ToArray();
            markedArticles = markedArticles.ToList().Select(a => Regex.Replace(a, @"\[\[.+?\]\]\w*|'''.+'''|''.+''", delegate (Match m)
            {
                var len = m.Value.Split().Count();
                var val = Regex.Replace(m.Value, @"\[\[", "").Split(new char[]{'|'},System.StringSplitOptions.RemoveEmptyEntries).First();
                var fragments = val.Split(new string[]{"]]" },System.StringSplitOptions.RemoveEmptyEntries);
                if (fragments == null || fragments.Length == 0) return "";
                linkForms[len < 10 ? len - 1 : 9].Add(fragments.First().ToLower());
                if(fragments.Length>1) linkForms[len < 10 ? len - 1 : 9].Add(fragments[1].ToLower());
                return "{"+lemmatizer.Lemmatize(fragments.First().Replace(".", "<|>")) + "}";
            }));
            markedArticles = markedArticles.ToList().Select(a => 
                Regex.Replace(a, @"\[\[|\]\]|<.+>|===.+===|==.+==|\r|\n|\r\n|,|\(|\)|-|\[http.*\]|&lt;.*?&gt;.*?&lt;/.*?&gt;|'''|''|\*", ""));
            markedArticles = markedArticles.ToList().Select(a => Regex.Replace(a, @"\.(?=[0-9])", "<")+'|');
            var markedArticlesArray = string.Join("", markedArticles.ToArray()).Split('.');
            for (int i = 0; i < markedArticlesArray.Length; i++) markedArticlesArray[i] = markedArticlesArray[i].Replace("<|>", ".") + '|';
            markedArticles = null;
            dump = null;
            articles = null;
            ///magic happens here
            for (int? a = 0; a < markedArticlesArray.Count(); a++)
                ThreadPool.QueueUserWorkItem(delegate (object p)
                {
                    var art = (p as int?).Value;
                    markedArticlesArray[art] = Regex.Replace(markedArticlesArray[art], @"\?|\!|:|-|=", " ");
                    var articleList = markedArticlesArray[art].Split(' ').ToList();
                    foreach (var subset in linkForms.Reverse())
                    {
                        if (subset.Count == 0) continue;
                        var len = System.Array.IndexOf(linkForms, subset)+1;
                        var validityCounter = 0;
                        for (int i = 0; i < articleList.Count - len + 1; i++)
                        {
                            var substring = string.Join(" ", articleList.GetRange(i, len));
                            validityCounter = validityCounter + articleList.ElementAt(i).Split('{').Length - articleList.ElementAt(i).Split('}').Length;
                            if (validityCounter == 0 && subset.Contains(substring.ToLower()))
                            {
                                var marked = '{'+lemmatizer.Lemmatize(substring)+'}';
                                var list = articleList.GetRange(0, i);
                                list.AddRange(marked.Split(' '));
                                if (i + len < articleList.Count) list.AddRange(articleList.GetRange(i + len, articleList.Count - i - len));
                                articleList = list;
                                if (len > 1) validityCounter++;
                            }
                        }
                    }
                    markedArticlesArray[art] = string.Join(" ", articleList);
                }, a);

            File.WriteAllLines("marked.csv", markedArticlesArray);
            thread.Join();
        }
    }
}
