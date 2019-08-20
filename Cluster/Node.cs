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
        private List<TcpClient> connections = new List<TcpClient>();
        private List<Tuple<IPEndPoint, int>> interfaceAddresses = new List<Tuple<IPEndPoint, int>>();
        private List<Socket> helpers = new List<Socket>();
        private UdpClient broadcastClient;
        private TcpListener listener;
        private bool listen = true;
        private bool helper = false;
        private int port;

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
                        !n.Name.Equals("wlan0") && !n.Name.Equals("docker0") && !n.Name.StartsWith("Virtual")) {
                        try {
                            Console.WriteLine(n.Name);
                            Console.WriteLine(ui.Address);
                            byte[] ip = new byte[4];
                            string[] ips = ui.Address.ToString().Split('.');
                            for (int i = 0; i < ip.Length; i++) {
                                ip[i] = Byte.Parse(ips[i]);
                            }
                            IPAddress iPAddress = new IPAddress(ip);
                            IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, port);
                            try {
                                interfaceAddresses.Add(new Tuple<IPEndPoint, int>(iPEndPoint, ui.PrefixLength));

                            } catch (PlatformNotSupportedException pnse) {
                                Console.WriteLine("Finding prefix length as .PrefixLength doesn't work");
                                byte[] subnet = ui.IPv4Mask.GetAddressBytes();
                                int prefixLength = 0;

                                foreach (byte element in subnet)
                                    if (element.Equals(0))
                                        prefixLength++;

                                interfaceAddresses.Add(new Tuple<IPEndPoint, int>(iPEndPoint, prefixLength *= 8));
                            }
                        } catch (Exception e) {
                            Console.WriteLine(e.ToString());
                        }
                    }
                }
            }

            this.port = port;
            broadcastClient = new UdpClient(port);
            broadcastClient.EnableBroadcast = true;
            listener = new TcpListener(IPAddress.Any, port);
            listener.ExclusiveAddressUse = false;

            if (interfaceAddresses.Count <= 0)
                throw new Exception("No Ethernet Interfaces found. Please check your device or specify interface/interface type to listen on.");
        }

        public Node(IPAddress ip, int subnet, int port) {
            try {
                Console.WriteLine(ip);
                IPEndPoint iPEndPoint = new IPEndPoint(ip, port);
                interfaceAddresses.Add(new Tuple<IPEndPoint, int>(iPEndPoint, subnet));
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }

            this.port = port;
            broadcastClient = new UdpClient(port);
            broadcastClient.EnableBroadcast = true;
            listener = new TcpListener(IPAddress.Any, port);

            if (interfaceAddresses.Count <= 0)
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
            listener.Start();

            for (int i = 0; i < interfaceAddresses.Count; i++) {
                string join = "Join:" + interfaceAddresses[i].Item1.ToString();
                byte[] joinCommand = Encoding.UTF8.GetBytes(join);
                byte[] broad = interfaceAddresses[i].Item1.Address.GetAddressBytes();
                for (int j = 0; j < ((24 - interfaceAddresses[i].Item2) / 8) + 1; j++) {
                    broad[broad.Length - j - 1] = 255;
                }
                IPAddress broadIP = new IPAddress(broad);
                broadcastClient.Send(joinCommand, joinCommand.Length, new IPEndPoint(broadIP, port));
                Console.WriteLine("Using interface: {0}", interfaceAddresses[i].Item1.ToString());
                Console.WriteLine("Awaiting Connection");
            }

            AcceptConnections(listener);
        }

        private async void AcceptConnections(TcpListener socket) {
            while (listen) {
                TcpClient client = await socket.AcceptTcpClientAsync();
                //if (connections.Count > 0) {
                //    bool connected = false;
                //    foreach (TcpClient connection in connections) {
                //        Console.WriteLine("{0} compared to {1}", connection.Client.RemoteEndPoint, client.Client.RemoteEndPoint);
                //        if (connection.Client.RemoteEndPoint.ToString().Split(":")[0].Equals(client.Client.RemoteEndPoint.ToString().Split(":")[0]))
                //            connected = true;
                //    }

                //    if (!connected) {
                //        connections.Add(client);
                //        ConnectionListen(client);
                //    } else {
                //        Console.WriteLine("Found duplicate connection");
                //        NetworkStream stream = client.GetStream();
                //        byte[] discard = Encoding.UTF8.GetBytes("Discard");
                //        stream.Write(discard, 0, discard.Length);
                //        client.Dispose();
                //    }
                //} else
                connections.Add(client);
                connections.Distinct();
                Console.WriteLine("Connected to {0}", connections[connections.Count - 1].Client.RemoteEndPoint);
                ConnectionListen(connections[connections.Count - 1]);
            }
        }

        private async void ConnectionListen(TcpClient socket) {
            NetworkStream stream = socket.GetStream();
            byte[] buffer = new byte[1024];

            while (listen) {
                int bytes = await stream.ReadAsync(buffer, 0, buffer.Length);
                byte[] data = new byte[bytes];
                for (int i = 0; i < bytes; i++) {
                    data[i] = buffer[i];
                }
                if (bytes == 0)
                    break;
                Console.WriteLine("Recived {0} bytes from {1} reading {2}", bytes, socket.Client.RemoteEndPoint,
                    Encoding.ASCII.GetString(data));
                if (Encoding.UTF8.GetString(data).Equals("Discard")) {
                    connections.Remove(socket);
                    socket.Dispose();
                }
            }
        }

        public void TestBroadcast(string message) {
            broadcastClient.Send(Encoding.UTF8.GetBytes(message), Encoding.UTF8.GetBytes(message).Length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), port));
        }

        public void TestTCP(string message) {
            Console.WriteLine("Testing TCP");
            foreach (TcpClient client in connections) {
                byte[] data = Encoding.UTF8.GetBytes(message);
                Console.WriteLine("Sending ({0}) to {1}", message, client.Client.RemoteEndPoint);
                client.GetStream().Write(data, 0, data.Length);
            }
        }

        public byte[] ParallelCompute(Action body) {
            if (body == null)
                throw new NotImplementedException("No Parallel method set");
            return new byte[1];
        }

        public byte[] SingleCompute(Action body) {
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
                                //socket.Send(SingleMethod(SeqBody));
                                SocketListen(socket);
                                break;
                            case "Parallel":
                                socket.Send(new byte[1]);
                                Console.WriteLine("Muli Node problem, running parallel compute");
                                Console.WriteLine("Requesting helpers from cluster");
                                //for (int i = 0; i < broadcasts.Count; i++) {
                                //    string help = Encoding.UTF8.GetBytes("Parallel-Help:") + interfaceListeners[i].LocalEndpoint.ToString();
                                //    broadcasts[i].Client.SendTo(Encoding.UTF8.GetBytes(help), broadcasts[i].Client.LocalEndPoint);
                                //}
                                //buffer = new byte[4];
                                //bytes = socket.Receive(buffer);
                                //while (bytes != 4) {
                                //    bytes += socket.Receive(buffer, bytes, (4 - bytes), SocketFlags.None);
                                //}
                                //Console.WriteLine("Muli Node problem, running parallel compute");
                                //byte[] myResult = ParallelMethod(ParallelBody);
                                //List<byte[]> helperResults = new List<byte[]>();
                                //foreach (Socket h in helpers) {
                                //    buffer = new byte[4];
                                //    bytes = socket.Receive(buffer);
                                //    while (bytes != 4) {
                                //        bytes += socket.Receive(buffer, bytes, (4 - bytes), SocketFlags.None);
                                //    }
                                //    int size = BitConverter.ToInt32(buffer, 0);
                                //    buffer = new byte[1024];
                                //    bytes = 0;
                                //    while (bytes < size) {
                                //        byte[] dataChunk = new byte[h.Receive(buffer)];
                                //        bytes += dataChunk.Length;
                                //        for (int i = 0; i < dataChunk.Length; i++)
                                //            dataChunk[i] = buffer[i];
                                //        helperResults.Add(dataChunk);
                                //    }
                                //}
                                //socket.Send(myResult);
                                //SocketListen(socket);
                                Console.WriteLine("Needs Implementation");
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
                foreach (TcpClient socket in connections)
                    Console.WriteLine(socket.Client.RemoteEndPoint);
            else
                Console.WriteLine("No connections to lists");
        }

        public void ListListeners() {
            Console.WriteLine("Listing listeners");
            foreach (Tuple<IPEndPoint, int> endPoint in interfaceAddresses)
                Console.WriteLine(endPoint.Item1);
        }

        /// <summary>
        /// Method to Disconnect the Node from the Cluster and close all sockets and broadcasters
        /// </summary>
        public void Close() {
            Console.WriteLine("Closing Node");
            listen = false;

            string leave = "Leave:";
            for (int i = 0; i < interfaceAddresses.Count; i++) {
                leave += interfaceAddresses[0].Item1.ToString() + ",";
                interfaceAddresses.RemoveAt(0);
            }
            leave = leave.Remove(leave.LastIndexOf(","));
            Console.WriteLine("Broadcasting leave on: {0}", leave);
            listener.Stop();

            byte[] leaveCommand = Encoding.UTF8.GetBytes(leave);
            broadcastClient.Send(leaveCommand, leaveCommand.Length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), port));
            broadcastClient.Close();
            for (int i = 0; i < connections.Count; i++) {
                connections[0].Client.Shutdown(SocketShutdown.Both);
                connections[0].Close();
                connections.RemoveAt(0);
            }
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

            foreach (Tuple<IPEndPoint, int> endPoint in interfaceAddresses) {
                if (endPoint.Item1.Equals(ep)) {
                    me = true;
                    Console.WriteLine("Found myself ({0})", endPoint.Item1.ToString());
                }
            }

            if (!me) {
                bool add = true;

                if (connections.Count > 0)
                    foreach (TcpClient client in connections)
                        if (ep.Equals(client.Client.RemoteEndPoint))
                            add = false;
                if (add)
                    try {
                        TcpClient handler = new TcpClient(AddressFamily.InterNetwork);
                        handler.Connect(ep);
                        connections.Add(handler);
                        Console.WriteLine("Connection made with {0} using local ip {1}", handler.Client.RemoteEndPoint.ToString(),
                            handler.Client.LocalEndPoint.ToString());
                    } catch (SocketException se) {
                        Console.WriteLine("Socket Exception connecting to {1}: \n{0}", se.ToString(), ep);
                    }
            }
        }

        /// <summary>
        /// Method to disconnect from a Node in the Cluster
        /// </summary>
        /// <param name="ipAddress">IP address of the Node to disconnect from</param>
        public void DisconnectFromNode(byte[] ipAddress) {
            IPAddress ip = new IPAddress(ipAddress);
            for (int i = connections.Count - 1; i >= 0; i--)
                if (connections[i].Client.RemoteEndPoint.ToString().Split(':')[0].Equals(ip.ToString())) {
                    connections[i].Client.Shutdown(SocketShutdown.Both);
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
                Console.WriteLine("Recieved {0}", Encoding.UTF8.GetString(data));
                string[] command = Encoding.UTF8.GetString(data).Split(":");
                switch (command[0]) {
                    case "Join":
                        Console.WriteLine("Join Request recieved from: {0}", command[1]);
                        Tuple<byte[], int> iPPort = FormatIPPort(command[1], command[2]);
                        ConnectToNode(iPPort.Item1, iPPort.Item2);
                        break;
                    case "Leave":
                        Console.WriteLine("{0} leaving Cluster", command[1]);
                        Tuple<byte[], int> ipPort = FormatIPPort(command[1], command[2]);
                        DisconnectFromNode(ipPort.Item1);
                        break;
                    case "Entry-Request":
                        //for (int i = 0; i < broadcasts.Count; i++)
                        //    if (broadcasts[i].Client.LocalEndPoint.ToString().Split(":")[0].Equals(command[1])) {
                        //        string entryResponse = "Entry:" + interfaceListeners[i].LocalEndpoint;
                        //        broadcasts[i].Client.SendTo(Encoding.UTF8.GetBytes(entryResponse), broadcasts[i].Client.LocalEndPoint);
                        //    }
                        Console.WriteLine("Needs Implementation");
                        break;
                    case "Parallel-Help":
                        Console.WriteLine("Helper request from {0} for parallel problem", command[1]);
                        for (int i = 0; i < connections.Count; i++)
                            if (connections[i].Client.RemoteEndPoint.ToString().Split(':')[0].Equals(command[1])) {
                                byte[] message = Encoding.UTF8.GetBytes("Helper");
                                connections[i].GetStream().Write(message, 0, message.Length);
                            }
                        break;
                }
                Console.WriteLine(Encoding.UTF8.GetString(data));
            }
        }
    }
}
