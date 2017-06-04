using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace WinDHCP.Library
{
    public class DhcpServer
    {
        private const int DHCPPORT = 67;
        internal const int DHCPCLIENTPORT = 68;
        private const int DHCPMESSAGEMAXSIZE = 1024;

        private TimeSpan m_OfferTimeout = TimeSpan.FromSeconds(60);
        private TimeSpan m_LeaseDuration = TimeSpan.FromDays(1);

        private Dictionary<InternetAddress, AddressLease> m_ActiveLeases = new Dictionary<InternetAddress, AddressLease>();
        private SortedList<InternetAddress, AddressLease> m_InactiveLeases = new SortedList<InternetAddress, AddressLease>();
        private NetworkInterface m_DhcpInterface;
        private IPAddress m_DhcpInterfaceAddress;

        private InternetAddress m_StartAddress = new InternetAddress(192, 168, 100, 10);
        private InternetAddress m_EndAddress = new InternetAddress(192, 168, 100, 150);
        private InternetAddress m_Subnet = new InternetAddress(255, 255, 255, 0);
        private InternetAddress m_Gateway = new InternetAddress(192, 168, 100, 1);
        private string m_DnsSuffix;
        private List<InternetAddress> m_DnsServers = new List<InternetAddress>();                
        private Dictionary<PhysicalAddress, InternetAddress> m_Reservations = new Dictionary<PhysicalAddress, InternetAddress>();

        private object m_LeaseSync = new object();        
        private ReaderWriterLock m_AbortLock = new ReaderWriterLock();
        private Socket m_DhcpSocket;
        private bool m_Abort = false;

        private Timer m_CleanupTimer;        

        internal Socket DhcpSocket
        {
            get { return this.m_DhcpSocket; }
            set { this.m_DhcpSocket = value; }
        }

        public TimeSpan OfferTimeout
        {
            get { return this.m_OfferTimeout; }
            set { this.m_OfferTimeout = value; }
        }

        public TimeSpan LeaseDuration
        {
            get { return this.m_LeaseDuration; }
            set { this.m_LeaseDuration = value; }
        }

        public NetworkInterface DhcpInterface
        {
            get { return this.m_DhcpInterface; }
            set { this.m_DhcpInterface = value; }
        }

        internal IPAddress DhcpInterfaceAddress
        {
            get { return this.m_DhcpInterfaceAddress; }
            set { this.m_DhcpInterfaceAddress = value; }
        }

        internal Dictionary<InternetAddress, AddressLease> ActiveLeases
        {
            get { return m_ActiveLeases; }
            set { m_ActiveLeases = value; }
        }

        internal SortedList<InternetAddress, AddressLease> InactiveLeases
        {
            get { return m_InactiveLeases; }
            set { m_InactiveLeases = value; }
        }

        public InternetAddress StartAddress
        {
            get { return this.m_StartAddress; }
            set { this.m_StartAddress = value; }
        }

        public InternetAddress EndAddress
        {
            get { return this.m_EndAddress; }
            set { this.m_EndAddress = value; }
        }

        public InternetAddress Subnet
        {
            get { return this.m_Subnet; }
            set { this.m_Subnet = value; }
        }

        public InternetAddress Gateway
        {
            get { return this.m_Gateway; }
            set { this.m_Gateway = value; }
        }

        public string DnsSuffix
        {
            get { return this.m_DnsSuffix; }
            set { this.m_DnsSuffix = value; }
        }

        public List<InternetAddress> DnsServers
        {
            get { return this.m_DnsServers; }
        }       

        public Dictionary<PhysicalAddress, InternetAddress> Reservations
        {
            get { return this.m_Reservations; }
        }

        public DhcpServer()
        {
        }       

        public void Start()
        {
            Trace.TraceInformation("Dhcp Server Starting...");

            this.m_ActiveLeases.Clear();
            this.m_ActiveLeases = Serializer.RestoreActiveLeases();
            if (m_ActiveLeases == null)
                m_ActiveLeases = new Dictionary<InternetAddress, AddressLease>();
            this.m_InactiveLeases.Clear();

            for (InternetAddress address = this.m_StartAddress.Copy(); address.CompareTo(this.m_EndAddress) <= 0; address = address.NextAddress())
            {
                if (this.m_ActiveLeases.ContainsKey(address) && this.m_ActiveLeases[address].Expiration < DateTime.Now)
                {
                    this.m_ActiveLeases.Remove(address);
                    this.m_InactiveLeases.Add(address, new AddressLease(null, address, DateTime.MinValue));
                }
                else if (!this.m_ActiveLeases.ContainsKey(address))
                {
                    this.m_InactiveLeases.Add(address, new AddressLease(null, address, DateTime.MinValue));
                }                
            }

            if (this.m_DhcpInterface == null)
            {
                Trace.TraceInformation("Enumerating Network Interfaces.");
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        this.m_DhcpInterface = nic;
                    }
                    else if ((nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet || nic.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet || nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) && nic.OperationalStatus == OperationalStatus.Up)
                    {
                        Trace.TraceInformation("Using Network Interface \"{0}\".", nic.Name);
                        this.m_DhcpInterface = nic;
                        break;
                    }
                }

#if TRACE
                if (this.m_DhcpInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    Trace.TraceInformation("Active Ethernet Network Interface Not Found. Using Loopback.");
                }
#endif
            }
            
            foreach (UnicastIPAddressInformation interfaceAddress in this.m_DhcpInterface.GetIPProperties().UnicastAddresses)
            {
                if (interfaceAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    this.m_DhcpInterfaceAddress = interfaceAddress.Address;
                }
            }

            if (this.m_DhcpInterfaceAddress == null)
            {
                Trace.TraceError("Unabled to Set Dhcp Interface Address. Check the networkInterface property of your config file.");
                throw new InvalidOperationException("Unabled to Set Dhcp Interface Address.");
            }

            this.m_Abort = false;

            //timer to check expiration dates of leases(time in milliseconds)
            this.m_CleanupTimer = new Timer(new TimerCallback(this.CleanUp), null, 60000, 30000);

            this.m_DhcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.m_DhcpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            this.m_DhcpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            //this.m_DhcpSocket.Bind(new IPEndPoint(this.m_DhcpInterfaceAddress, DhcpPort));
            //this.m_DhcpSocket.Bind(new IPEndPoint(IPAddress.Parse("192.168.100.6"), DhcpPort));
            this.m_DhcpSocket.Bind(new IPEndPoint(IPAddress.Parse("192.168.100.6"), DHCPPORT));

            this.Listen();

            Trace.TraceInformation("Dhcp Service Started.");
        }

        public void Stop()
        {
            this.m_AbortLock.AcquireWriterLock(Timeout.Infinite);

            try
            {
                this.m_Abort = true;

                this.m_CleanupTimer.Dispose();

                this.m_DhcpSocket.Shutdown(SocketShutdown.Both);
                this.m_DhcpSocket.Close();
                this.m_DhcpSocket = null;

                if (m_ActiveLeases != null && m_ActiveLeases.Count != 0)
                    Serializer.SaveActiveLeases(m_ActiveLeases);
            }
            finally
            {
                this.m_AbortLock.ReleaseLock();
            }
        }

        private void Listen()
        {
            byte[] messageBufer = new byte[DHCPMESSAGEMAXSIZE];
            EndPoint source = new IPEndPoint(IPAddress.Any, DHCPCLIENTPORT);

            this.m_AbortLock.AcquireReaderLock(Timeout.Infinite);

            try
            {
                if (this.m_Abort)
                {
                    return;
                }

                Trace.TraceInformation("Listening For Dhcp Request.");
                this.m_DhcpSocket.BeginReceiveFrom(messageBufer, 0, DHCPMESSAGEMAXSIZE, SocketFlags.None, ref source, new AsyncCallback(this.OnReceive), messageBufer);                
            }
            finally
            {
                this.m_AbortLock.ReleaseLock();
            }
        }

        private void CleanUp(object state)
        {
            lock (this.m_LeaseSync)
            {
                List<AddressLease> toRemove = new List<AddressLease>();

                foreach (AddressLease lease in this.m_ActiveLeases.Values)
                {
                    if (!lease.Acknowledged && lease.Expiration < DateTime.Now.Add(this.m_OfferTimeout) ||
                        lease.Acknowledged && lease.Expiration < DateTime.Now)
                    {
                        toRemove.Add(lease);
                    }
                }

                foreach (AddressLease lease in toRemove)
                {
                    this.m_ActiveLeases.Remove(lease.Address);
                    lease.Acknowledged = false;
                    this.m_InactiveLeases.Add(lease.Address, lease);
                }
            }
        }

        private void OnReceive(IAsyncResult result)
        {
            DhcpReceivedData data = new DhcpReceivedData((byte[])result.AsyncState);
            data.Result = result;

            if (!this.m_Abort)
            {
                Trace.TraceInformation("Dhcp Messages Received, Queued for Processing.");

                // Queue this request for processing
                ThreadPool.QueueUserWorkItem(new WaitCallback(this.CompleteRequest), data);

                this.Listen();
            }
        }

        private void CompleteRequest(object state)
        {
            Trace.TraceInformation("Received message");
            DhcpReceivedData messageData = (DhcpReceivedData)state;
            EndPoint source = new IPEndPoint(IPAddress.Any, DHCPCLIENTPORT);

            this.m_AbortLock.AcquireReaderLock(Timeout.Infinite);

            try
            {
                if (this.m_Abort)
                {
                    return;
                }

                messageData.BufferSize = this.m_DhcpSocket.EndReceiveFrom(messageData.Result, ref source);               
            }
            finally
            {
                this.m_AbortLock.ReleaseLock();
            }

            DhcpMessage message;

            try
            {
                message = new DhcpMessage(messageData.MessageBuffer);
            }
            catch (ArgumentException ex)
            {
                TraceException("Error Parsing Dhcp Message", ex);
                return;
            }
            catch (InvalidCastException ex)
            {
                TraceException("Error Parsing Dhcp Message", ex);
                return;
            }
            catch (IndexOutOfRangeException ex)
            {
                TraceException("Error Parsing Dhcp Message", ex);
                return;
            }
            catch (Exception ex)
            {
                TraceException("Error Parsing Dhcp Message", ex);
                throw;
            }

            if (message.Operation == DhcpOperation.BootRequest)
            {
                byte[] messageTypeData = message.GetOptionData(DhcpOption.DhcpMessageType);

                if (messageTypeData != null && messageTypeData.Length == 1)
                {
                    DhcpMessageType messageType = (DhcpMessageType)messageTypeData[0];
                    ReceivedMessage receivedMessage = new ReceivedMessage();

                    switch (messageType)
                    {
                        case DhcpMessageType.Discover:
                            Trace.TraceInformation("{0} Dhcp DISCOVER Message Received.", Thread.CurrentThread.ManagedThreadId);
                            receivedMessage.DhcpDiscover(this, message);
                            Trace.TraceInformation("{0} Dhcp DISCOVER Message Processed.", Thread.CurrentThread.ManagedThreadId);
                            break;
                        case DhcpMessageType.Request:
                            Trace.TraceInformation("{0} Dhcp REQUEST Message Received.", Thread.CurrentThread.ManagedThreadId);
                            receivedMessage.DhcpRequest(this, message);
                            Trace.TraceInformation("{0} Dhcp REQUEST Message Processed.", Thread.CurrentThread.ManagedThreadId);
                            break;
                        default:
                            Trace.TraceWarning("Unknown Dhcp Message ({0}) Received, Ignoring.", messageType.ToString());
                            break;
                    }
                }
                else
                {
                    Trace.TraceWarning("Unknown Dhcp Data Received, Ignoring.");
                }
            }
        }

        internal static void TraceException(string prefix, Exception ex)
        {
            Trace.TraceError("{0}: ({1}) - {2}\r\n{3}", prefix, ex.GetType().Name, ex.Message, ex.StackTrace);

            if (ex.InnerException != null)
            {
                TraceException("    Inner Exception", ex.InnerException);
            }
        }        
    }
}
