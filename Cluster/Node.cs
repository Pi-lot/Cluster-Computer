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
    class Node {
        // Lists to track connections and interfaces to listen on, as well as
        // broadcast client.
        private List<Socket> connections = new List<Socket>();
        private List<Socket> interfaceListeners = new List<Socket>();
        private List<IPEndPoint> broadcasts = new List<IPEndPoint>();
        private List<Socket> helpers = new List<Socket>();
        private List<Task> socketListens = new List<Task>();
        private UdpClient broadcastClient;
        private bool listen = true;
        private bool helper = false;
        private Action ParallelBody;
        private Action SeqBody;

        /// <summary>
        /// Contructor that creates a Cluster Node using the specified port on
        /// Ethernet Devices
        /// </summary>
        /// <param name="port">Port for the Cluster to listen on</param>
        public Node(int port) {
            NetworkInterface[] nf = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface n in nf) {
                foreach (UnicastIPAddressInformation ui in n.GetIPProperties().UnicastAddresses) {
                    if (ui.Address.AddressFamily == AddressFamily.InterNetwork && n.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                        !n.Name.Equals("wlan0") && !n.Name.Equals("docker0")) {
                        try {
                            Console.WriteLine(n.Name);
                            Console.WriteLine(ui.Address);
                            byte[] ip = new byte[4];
                            string[] ips = ui.Address.ToString().Split('.');
                            for (int i = 0; i < ip.Length; i++) {
                                ip[i] = Byte.Parse(ips[i]);
                            }
                            IPAddress iPAddress = new IPAddress(ip);
                            IPEndPoint endpoint = new IPEndPoint(iPAddress, port);
                            Socket socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                            socket.Bind(endpoint);
                            socket.Listen(100);
                            interfaceListeners.Add(socket);
                            byte[] broad = new byte[4];
                            if (ui.Address.ToString().Equals(iPAddress.ToString())) {
                                int index = 0;
                                foreach (string ipa in ui.IPv4Mask.ToString().Split('.')) {
                                    if (ipa == "0")
                                        broad[index] = 255;
                                    else
                                        broad[index] = ip[index];
                                    index++;
                                }
                            }
                            IPAddress broadcast = new IPAddress(broad);
                            broadcasts.Add(new IPEndPoint(broadcast, port));
                        } catch (Exception e) {
                            Console.WriteLine(e.ToString());
                        }
                    }
                }
            }

            broadcastClient = new UdpClient(port);
            broadcastClient.EnableBroadcast = true;

            if (interfaceListeners.Count <= 0)
                throw new Exception("No Ethernet Interfaces found. Please check your device or specify interface/interface type to listen on.");
        }

        /// <summary>
        /// Method for Node to join the Cluster network
        /// </summary>
        public void JoinCluster() {
            GoOnline();
            GetBroadcasts();
        }

        public int Test(Action action) {
            return 1;
        }

        /// <summary>
        /// Method to bring Node online of the Cluster network
        /// Consists of broadcasting Join commmand, then awaiting connections of interfaces
        /// </summary>
        public void GoOnline() {
            for (int i = 0; i < broadcasts.Count; i++) {
                string join = "Join:" + interfaceListeners[i].LocalEndPoint;
                byte[] joinCommand = Encoding.UTF8.GetBytes(join);
                Console.WriteLine("Broadcasting online on: {0}", broadcasts[i].ToString());
                broadcastClient.Client.SendTo(joinCommand, broadcasts[i]);
                Console.WriteLine("Awaiting Connection");
                AcceptConnections(interfaceListeners[i]);
            }
        }

        private async void AcceptConnections(Socket socket) {
            while (listen) {
                connections.Add(await socket.AcceptAsync());
                connections.Distinct();
                Console.WriteLine("Connected to {0}", connections[connections.Count - 1].RemoteEndPoint);
                ConnectionListen(connections[connections.Count - 1]);
            }
        }

        private async void ConnectionListen(Socket socket) {
            await Task.Run(() => SocketListen(socket));

            if (listen)
                ConnectionListen(socket);
        }

        public void SetParallelBody(Action body) {
            ParallelBody = body;
        }

        public void SetSeqBody(Action body) {
            SeqBody = body;
        }

        public byte[] ParallelMethod(Action body) {
            if (body == null)
                throw new NotImplementedException("No Parallel method set");
            return new byte[1];
        }

        public byte[] SingleMethod(Action body) {
            if (body == null)
                throw new NotImplementedException("No Seq method set");
            return new byte[1];
        }

        public void SocketListen(Socket socket) {
            if (listen)
                try {
                    byte[] buffer = new byte[1024];
                    int bytes = socket.Receive(buffer);
                    if (bytes > 0) {
                        string command = Encoding.UTF8.GetString(buffer, 0, bytes);
                        Console.WriteLine("Connection with {0} recieved: {1}", socket.RemoteEndPoint, command);

                        switch (command) {
                            case "Close":
                                Console.WriteLine("Closing Socket");
                                string[] s = socket.RemoteEndPoint.ToString().Split(':');
                                Tuple<byte[], int> ipP = FormatIPPort(s[0], s[1]);
                                DisconnectFromNode(ipP.Item1);
                                break;
                            case "Single":
                                socket.Send(new byte[1]);
                                Console.WriteLine("Single Node problem, running single compute");
                                buffer = new byte[4];
                                bytes = socket.Receive(buffer);
                                while (bytes != 4) {
                                    bytes += socket.Receive(buffer, bytes, (4 - bytes), SocketFlags.None);
                                }
                                socket.Send(SingleMethod(SeqBody));
                                SocketListen(socket);
                                break;
                            case "Parallel":
                                socket.Send(new byte[1]);
                                Console.WriteLine("Muli Node problem, running parallel compute");
                                Console.WriteLine("Requesting helpers from cluster");
                                for (int i = 0; i < broadcasts.Count; i++) {
                                    string help = Encoding.UTF8.GetBytes("Parallel-Help:") + interfaceListeners[i].LocalEndPoint.ToString();
                                    broadcastClient.Client.SendTo(Encoding.UTF8.GetBytes(help), broadcasts[i]);
                                }
                                buffer = new byte[4];
                                bytes = socket.Receive(buffer);
                                while (bytes != 4) {
                                    bytes += socket.Receive(buffer, bytes, (4 - bytes), SocketFlags.None);
                                }
                                Console.WriteLine("Muli Node problem, running parallel compute");
                                byte[] myResult = ParallelMethod(ParallelBody);
                                List<byte[]> helperResults = new List<byte[]>();
                                foreach (Socket h in helpers) {
                                    buffer = new byte[4];
                                    bytes = socket.Receive(buffer);
                                    while (bytes != 4) {
                                        bytes += socket.Receive(buffer, bytes, (4 - bytes), SocketFlags.None);
                                    }
                                    int size = BitConverter.ToInt32(buffer, 0);
                                    buffer = new byte[1024];
                                    bytes = 0;
                                    while (bytes < size) {
                                        byte[] dataChunk = new byte[h.Receive(buffer)];
                                        bytes += dataChunk.Length;
                                        for (int i = 0; i < dataChunk.Length; i++)
                                            dataChunk[i] = buffer[i];
                                        helperResults.Add(dataChunk);
                                    }
                                }
                                socket.Send(myResult);
                                SocketListen(socket);
                                break;
                            case "Helper":
                                Console.WriteLine("Helper available on {0}", socket.RemoteEndPoint);
                                helpers.Add(socket);
                                SocketListen(socket);
                                break;
                        }
                    }
                } catch (ObjectDisposedException e) {
                    Console.WriteLine("Object Disposed");
                } catch (SocketException se) {
                    Console.WriteLine(se.ToString());
                    Console.WriteLine(socket.RemoteEndPoint);
                }
        }

        /// <summary>
        /// Method to list the current connections
        /// </summary>
        public void ListConnections() {
            Console.WriteLine("Listing Connections");
            if (connections.Count > 0)
                foreach (Socket socket in connections)
                    Console.WriteLine(socket.RemoteEndPoint);
            else
                Console.WriteLine("No connections to lists");
        }

        public void ListListeners() {
            Console.WriteLine("Listing listeners");
            foreach (Socket socket in interfaceListeners)
                Console.WriteLine(socket.LocalEndPoint);
        }

        /// <summary>
        /// Method to Disconnect the Node from the Cluster and close all sockets and broadcasters
        /// </summary>
        public void Close() {
            Console.WriteLine("Closing Node");
            listen = false;
            for (int i = 0; i < broadcasts.Count; i++) {
                string leave = "Leave:" + interfaceListeners[0].LocalEndPoint;
                byte[] leaveCommand = Encoding.UTF8.GetBytes(leave);
                Console.WriteLine("Broadcasting leave on: {0}", broadcasts[0].ToString());
                broadcastClient.Client.SendTo(leaveCommand, broadcasts[0]);
                broadcasts.RemoveAt(0);
                interfaceListeners[0].Close();
                interfaceListeners.RemoveAt(0);
            }
            for (int i = 0; i < connections.Count; i++) {
                connections[0].Shutdown(SocketShutdown.Both);
                connections[0].Close();
                connections.RemoveAt(0);
            }
            broadcastClient.Close();
            Console.WriteLine("Closed");
        }

        /// <summary>
        /// Method to connect to a Node within the Cluster
        /// </summary>
        /// <param name="ipAddress">IP of the Node to connect to</param>
        /// <param name="port">Port for socket to connect on</param>
        public void ConnectToNode(byte[] ipAddress, int port) {
            IPAddress ip = new IPAddress(ipAddress);
            IPEndPoint ep = new IPEndPoint(ip, port);

            bool me = false;

            foreach (Socket socket in interfaceListeners) {
                if (socket.LocalEndPoint.Equals(ep))
                    me = true;
            }

            if (!me) {
                Socket socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                connections.Add(socket);
                socket.Connect(ep);
                Console.WriteLine("Connection made with {0}", ep.ToString());
            } else {
                Console.WriteLine("Found myself");
            }
        }

        /// <summary>
        /// Method to disconnect from a Node in the Cluster
        /// </summary>
        /// <param name="ipAddress">IP address of the Node to disconnect from</param>
        public void DisconnectFromNode(byte[] ipAddress) {
            IPAddress ip = new IPAddress(ipAddress);
            for (int i = connections.Count - 1; i >= 0; i--)
                if (connections[i].RemoteEndPoint.ToString().Split(':')[0].Equals(ip.ToString())) {
                    connections[i].Shutdown(SocketShutdown.Both);
                    connections[i].Close();
                    connections.RemoveAt(i);
                }
            Console.WriteLine("Connection closed with {0}", ip.ToString());
        }

        /// <summary>
        /// Method to return a Tuple to convert IP and port to byte array and int, respectively.
        /// </summary>
        /// <param name="ipAddress">IP address string</param>
        /// <param name="port">Port String</param>
        /// <returns>Tuple containing IP address as byte[] and port as int</returns>
        public Tuple<byte[], int> FormatIPPort(string ipAddress, string port) {
            byte[] ip = new byte[4];
            string[] addressS = ipAddress.Split('.');
            for (int i = 0; i < ip.Length; i++) {
                Byte.TryParse(addressS[i], out ip[i]);
            }

            int p;
            int.TryParse(port, out p);

            return new Tuple<byte[], int>(ip, p);
        }

        /// <summary>
        /// Method to listen for broadcasts on the interfaces.
        /// </summary>
        public async void GetBroadcasts() {
            Console.WriteLine("Recieving Broadcasts");
            while (listen) {
                UdpReceiveResult recieve = await broadcastClient.ReceiveAsync();
                byte[] data = recieve.Buffer;
                string[] command = Encoding.UTF8.GetString(data).Split(":");
                switch (command[0]) {
                    case "Join":
                        Console.WriteLine("Join Request recieved from: {0} ip on: {1} port", command[1], command[2]);
                        Tuple<byte[], int> ipPort = FormatIPPort(command[1], command[2]);
                        ConnectToNode(ipPort.Item1, ipPort.Item2);
                        break;
                    case "Leave":
                        Console.WriteLine("{0} leaving Cluster", command[1]);
                        ipPort = FormatIPPort(command[1], command[2]);
                        DisconnectFromNode(ipPort.Item1);
                        break;
                    case "Entry-Request":
                        for (int i = 0; i < broadcasts.Count; i++)
                            if (broadcasts[i].Address.ToString().Equals(command[1])) {
                                string entryResponse = "Entry:" + interfaceListeners[i].LocalEndPoint;
                                broadcastClient.Client.SendTo(Encoding.UTF8.GetBytes(entryResponse), broadcasts[i]);
                            }
                        break;
                    case "Parallel-Help":
                        Console.WriteLine("Helper request from {0} for parallel problem", command[1]);
                        for (int i = 0; i < connections.Count; i++)
                            if (connections[i].RemoteEndPoint.ToString().Split(':')[0].Equals(command[1])) {
                                connections[i].Send(Encoding.UTF8.GetBytes("Helper"));
                            }
                        break;
                }
                Console.WriteLine(Encoding.UTF8.GetString(data));
            }
        }
    }
}
