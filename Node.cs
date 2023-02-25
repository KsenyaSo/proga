using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TextPreprocessing
{
    class Node
    {
        public readonly string symbol;
        public readonly int number;
        public readonly int freq;
        public readonly Node bit0;
        public readonly Node bit1;
        public readonly Node bit2;
        public readonly Node bit3;
        public readonly Node bit4;

        public Node(string symbol, int freq, int number)
        {
            this.symbol = symbol;
            this.freq = freq;
            this.number = number;
        }

        public Node(Node bit0, Node bit1, int freq)
        {
            this.bit0 = bit0;
            this.bit1 = bit1;
            this.freq = freq;
        }

        public Node (Node bit0, Node bit1, Node bit2, int freq)
        {
            this.bit0 = bit0;
            this.bit1 = bit1;
            this.bit2 = bit2;
            this.freq = freq;
        }

        public Node (Node bit0, Node bit1, Node bit2, Node bit3, Node bit4, int freq)
        {
            this.bit0 = bit0;
            this.bit1 = bit1;
            this.bit2 = bit2;
            this.bit3 = bit3;
            this.bit4 = bit4;
            this.freq = freq;
        }
    }
}
