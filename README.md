# Cluster-Computer
Project for distributed computing in C#. Use in other programs will be similar to how it's used within Program.cs

Created using .NET Core 2.2 in C# and being run mainly on Raspberry Pi 3B+'s.

Currently being limited to just use the Ethernet ports on the computer that is being run. Functionality for using any found network device will be added later, as well as defining the interface/s to use.

All the management for communication between the devices is being done within the Node class. To used create a new Node on the port of your choice. The Node will remain offline until the JoinCluster() method is call, where it will remain online until the Close() method is called.

Sequential and Parallel programming methods are passed to the nodes in a similar way to Parallel.For() in the System.Threading.
