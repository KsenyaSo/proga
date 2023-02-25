using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextPrerocessing
{
    class SentencesFano
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime dttm { get; set; }
        public int Count { get; set; }
        public int CountUnic { get; set; }
        public double entropy2{ get; set; }
        public double CountElementary2 { get; set; }
        public double InfoCount2 { get; set; }
        public double entropy3 { get; set; }
        public double CountElementary3 { get; set; }
        public double InfoCount3 { get; set; }
        public double entropy5 { get; set; }
        public double CountElementary5 { get; set; }
        public double InfoCount5 { get; set; }
        public SentencesFano (string Name, DateTime date, int count, int countUnic, 
                                double entropy2, double countEl2, double info2, 
                                double entropy3, double countEl3, double info3, 
                                double entropy5, double countEl5, double info5)
        {
            this.Name = Name;
            this.dttm = date;
            this.Count = count;
            this.CountUnic = countUnic;
            this.entropy2 = entropy2;
            this.CountElementary2 = countEl2;
            this.InfoCount2 = info2;
            this.entropy3 = entropy3;
            this.CountElementary3 = countEl3;
            this.InfoCount3 = info3;
            this.entropy5 = entropy5;
            this.CountElementary5 = countEl5;
            this.InfoCount5 = info5;
        }

    }
}
