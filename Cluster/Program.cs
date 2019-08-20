using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net.NetworkInformation;

namespace Cluster {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("Cluster Class");
            Node node = new Node(11000);
            //node.SetParallelBody(() => { byte[] b = new byte[1]; });
            node.JoinCluster();
            bool run = true;
            while (run) {
                string input = Console.ReadLine();
                if (input.Equals("Stop")) {
                    node.Close();
                    run = false;
                } else if (input.Equals("List")) {
                    node.ListConnections();
                } else if (input.Equals("Listeners")) {
                    node.ListListeners();
                } else if (input.Equals("Test")) {
                    string message = Console.ReadLine();
                    node.TestBroadcast(message);
                } else if (input.Equals("TCP")) {
                    string message = Console.ReadLine();
                    node.TestTCP(message);
                }
            }
        }
    }
}
