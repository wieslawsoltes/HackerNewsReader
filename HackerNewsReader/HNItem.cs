using System.Collections.Generic;

namespace HackerNewsReader
{
    public class HNItem
    {
        public int id { get; set; }
        public string title { get; set; }
        public string by { get; set; }
        public long time { get; set; }
        public int score { get; set; }
        public string url { get; set; }
        public string text { get; set; }
        public List<int> kids { get; set; }
        public bool deleted { get; set; }
        public bool dead { get; set; }
    }
}
