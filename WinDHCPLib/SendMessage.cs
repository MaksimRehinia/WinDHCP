using System;
using System.Net;
using System.Diagnostics;
using System.Text;

namespace WinDHCP.Library
{
    static class SendMessage
    {
        public static void Offer (DhcpServer server, DhcpMessage message, AddressLease offer)
        {
            Trace.TraceInformation("Sending Dhcp Offer.");

            DhcpMessage response = new DhcpMessage();
            response.Operation = DhcpOperation.BootReply;
            response.Hardware = HardwareType.Ethernet;
            response.NextServerAddress = server.DhcpInterfaceAddress.GetAddressBytes();
            response.HardwareAddressLength = 6;
            response.SecondsElapsed = message.SecondsElapsed;
            response.SessionId = message.SessionId;
            response.Flags = message.Flags;

            response.AssignedAddress = offer.Address.ToArray();
            response.ClientHardwareAddress = message.ClientHardwareAddress;

            response.AddOption(DhcpOption.DhcpMessageType, (byte)DhcpMessageType.Offer);
            response.AddOption(DhcpOption.AddressRequest, offer.Address.ToArray());

            byte[] addressLease = message.GetOptionData(DhcpOption.AddressTime);
            if (addressLease != null && addressLease.Length != 0)
                server.LeaseDuration = TimeSpan.FromSeconds(BitConverter.ToInt32(DhcpMessage.ReverseByteOrder(addressLease), 0));
            if (server.LeaseDuration.TotalSeconds < 60 || server.LeaseDuration.CompareTo(TimeSpan.FromDays(30)) > 0)
                server.LeaseDuration = TimeSpan.FromDays(1);

            SendMessage.AddDhcpOptions(server, response);

            server.LeaseDuration = TimeSpan.FromDays(1);

            byte[] paramList = message.GetOptionData(DhcpOption.ParameterList);
            if (paramList != null)
            {
                response.OptionOrdering = paramList;
            }

            SendMessage.Reply(server, response);
            Trace.TraceInformation("Dhcp Offer Sent.");
        }

        public static void Ack(DhcpServer server, DhcpMessage message, AddressLease lease)
        {
            Trace.TraceInformation("Sending Dhcp Acknowledge.");

            DhcpMessage response = new DhcpMessage();
            response.Operation = DhcpOperation.BootReply;
            response.Hardware = HardwareType.Ethernet;
            response.HardwareAddressLength = 6;
            response.SecondsElapsed = message.SecondsElapsed;
            response.SessionId = message.SessionId;
            response.AssignedAddress = lease.Address.ToArray();
            response.ClientHardwareAddress = message.ClientHardwareAddress;

            response.AddOption(DhcpOption.DhcpMessageType, (byte)DhcpMessageType.Ack);
            response.AddOption(DhcpOption.AddressRequest, lease.Address.ToArray());

            byte[] addressLease = message.GetOptionData(DhcpOption.AddressTime);
            if (addressLease != null && addressLease.Length != 0)
                server.LeaseDuration = TimeSpan.FromSeconds(BitConverter.ToInt32(DhcpMessage.ReverseByteOrder(addressLease), 0));
            if (server.LeaseDuration.TotalSeconds < 60 || server.LeaseDuration.CompareTo(TimeSpan.FromDays(30)) > 0)
                server.LeaseDuration = TimeSpan.FromDays(1);

            SendMessage.AddDhcpOptions(server, response);

            server.LeaseDuration = TimeSpan.FromDays(1);

            SendMessage.Reply(server, response);
            Trace.TraceInformation("Dhcp Acknowledge Sent.");
        }

        public static void Nak(DhcpServer server, DhcpMessage message)
        {
            Trace.TraceInformation("Sending Dhcp Negative Acknowledge.");

            DhcpMessage response = new DhcpMessage();
            response.Operation = DhcpOperation.BootReply;
            response.Hardware = HardwareType.Ethernet;
            response.HardwareAddressLength = 6;
            response.SecondsElapsed = message.SecondsElapsed;
            response.SessionId = message.SessionId;

            response.ClientHardwareAddress = message.ClientHardwareAddress;

            response.AddOption(DhcpOption.DhcpMessageType, (byte)DhcpMessageType.Nak);

            SendMessage.Reply(server, response);
            Trace.TraceInformation("Dhcp Negative Acknowledge Sent.");
        }

        private static void Reply(DhcpServer server, DhcpMessage response)
        {
            response.AddOption(DhcpOption.DhcpAddress, server.DhcpInterfaceAddress.GetAddressBytes());

            byte[] sessionId = BitConverter.GetBytes(response.SessionId);

            try
            {
                server.DhcpSocket.SendTo(response.ToArray(), new IPEndPoint(IPAddress.Broadcast, DhcpServer.DHCPCLIENTPORT));
            }
            catch (Exception ex)
            {
                DhcpServer.TraceException("Error Sending Dhcp Reply", ex);
                throw;
            }
        }

        private static void AddDhcpOptions(DhcpServer server, DhcpMessage response)
        {
            response.AddOption(DhcpOption.AddressTime, DhcpMessage.ReverseByteOrder(BitConverter.GetBytes((int)server.LeaseDuration.TotalSeconds)));
            response.AddOption(DhcpOption.Router, server.Gateway.ToArray());
            response.AddOption(DhcpOption.SubnetMask, server.Subnet.ToArray());

            if (!string.IsNullOrEmpty(server.DnsSuffix))
            {
                response.AddOption(DhcpOption.DomainNameSuffix, Encoding.ASCII.GetBytes(server.DnsSuffix));
            }

            if (server.DnsServers.Count > 0)
            {
                byte[] dnsServerAddresses = new byte[server.DnsServers.Count * 4];
                for (int i = 0; i < server.DnsServers.Count; i++)
                {
                    server.DnsServers[i].ToArray().CopyTo(dnsServerAddresses, i * 4);
                }

                response.AddOption(DhcpOption.DomainNameServer, dnsServerAddresses);
            }
        }
    }
}
