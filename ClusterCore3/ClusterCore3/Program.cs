using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace ClusterCore3 {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("Cluster Class");
            NetworkStream ns;
            //Console.WriteLine("Specify IP?");
            //string yes = Console.ReadKey().KeyChar.ToString();
            //Console.WriteLine();
            Node node;
            //if (yes.ToLower().Equals("y")) {
            //    Console.WriteLine("IP Please");
            //    try {
            //        IPAddress ip;
            //        string ipS = Console.ReadLine();
            //        IPAddress.TryParse(ipS, out ip);
            //        node = new Node(ip, 11000);
            //    } catch (Exception e) {
            //        Console.WriteLine("Exception\n{0}", e.ToString());
            //        node = new Node(11000);
            //    }
            //} else
            node = new Node(11001);
            //byte[] ip = new byte[4];
            //ip[0] = 10;
            //bool repeat = true;
            //UdpClient test = new UdpClient(11000);
            ////UdpClient udpClient = new UdpClient();
            //while (repeat) {
            //    GetBroad(test);
            //    byte[] bytes = Encoding.UTF8.GetBytes(Console.ReadLine());
            //    Console.WriteLine("Sending {0}", Encoding.UTF8.GetString(bytes));
            //    test.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), 11000));

            //    Console.WriteLine("Press a key to continue...");
            //    string key = Console.ReadKey().KeyChar.ToString();
            //    Console.WriteLine();
            //    Console.WriteLine(key);
            //    if (key.Equals("p")) {
            //        repeat = false;
            //    }
            //}

            ////udpClient.Close();
            //test.Close();
            //Console.WriteLine("Port number");
            //int port = int.Parse(Console.ReadLine());
            //node.SetParallelBody(() => { byte[] b = new byte[1]; });
            //node.JoinCluster();
            node.StartListen();
            bool run = true;
            while (run) {
                string input = Console.ReadLine();
                if (input.Equals("Stop")) {
                    node.StopListen();
                    run = false;
                } else if (input.Equals("List")) {
                    node.ListConnections();
                } else if (input.Equals("Listeners")) {
                    node.ListListeners();
                } else if (input.Equals("Test")) {
                    Console.WriteLine("Testing Broadcast");
                    string message = Console.ReadLine();
                    node.TestBroadcast(message);
                } else if (input.Equals("TCP")) {
                    Console.WriteLine("Testing TCP");
                    string message = Console.ReadLine();
                    node.TestTCP(message);
                }
            }
        }
    }
}
