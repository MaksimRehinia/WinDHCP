using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinDHCP.Library
{
    public class ReceivedMessage
    {        
        private static bool m_AllowAny = true;
        private object m_LeaseSync = new object();        

        public static bool AllowAny
        {
            get { return m_AllowAny; }
            set { m_AllowAny = value; }
        }

        public void DhcpDiscover(DhcpServer server, DhcpMessage message)
        {
            byte[] addressRequestData = message.GetOptionData(DhcpOption.AddressRequest);
            if (addressRequestData == null && message.ClientAddress != null)
            {
                addressRequestData = message.ClientAddress;
            }
            else if (addressRequestData == null && message.ClientAddress == null)
            {
                addressRequestData = new byte[] { 0, 0, 0, 0 };
            }

            InternetAddress addressRequest = new InternetAddress(addressRequestData);

            if (addressRequest.CompareTo(server.StartAddress) < 0 || addressRequest.CompareTo(server.EndAddress) > 0)
            {
                addressRequestData = message.ClientAddress;
                addressRequest = new InternetAddress(addressRequestData);
            }

            // Assume we're on the ethernet network
            byte[] hardwareAddressData = new byte[6];
            Array.Copy(message.ClientHardwareAddress, hardwareAddressData, 6);
            PhysicalAddress clientHardwareAddress = new PhysicalAddress(hardwareAddressData);

            AddressLease offer = null;

            // If this client is explicitly allowed, or they are not denied and the allow any flag is set
            if (Acls.Acl.ContainsKey(clientHardwareAddress) && Acls.Acl[clientHardwareAddress] ||
                !Acls.Acl.ContainsKey(clientHardwareAddress) && m_AllowAny)
            {
                if (server.Reservations.ContainsKey(clientHardwareAddress))
                {
                    offer = new AddressLease(clientHardwareAddress, server.Reservations[clientHardwareAddress], DateTime.Now.Add(server.LeaseDuration));
                }
                else
                {
                    lock (this.m_LeaseSync)
                    {
                        if (!addressRequest.IsEmpty)
                        {
                            if (server.InactiveLeases.ContainsKey(addressRequest))
                            {
                                offer = server.InactiveLeases[addressRequest];
                                server.InactiveLeases.Remove(addressRequest);
                                server.ActiveLeases.Add(addressRequest, offer);
                            }
                            else if (server.ActiveLeases.ContainsKey(addressRequest) && server.ActiveLeases[addressRequest].Owner.Equals(clientHardwareAddress))
                            {
                                offer = server.ActiveLeases[addressRequest];
                            }
                            else if(server.InactiveLeases.Count > 0)
                            {
                                offer = server.InactiveLeases.Values[0];
                                server.InactiveLeases.Remove(offer.Address);
                                server.ActiveLeases.Add(offer.Address, offer);
                            }
                        }
                        else if (server.InactiveLeases.Count > 0)
                        {
                            server.InactiveLeases.Values[0].Expiration = DateTime.Now.Add(server.OfferTimeout);
                            offer = server.InactiveLeases.Values[0];
                            server.InactiveLeases.Remove(offer.Address);
                            server.ActiveLeases.Add(offer.Address, offer);
                        }
                    }
                }
            }

            if (offer == null)
            {
                SendMessage.Nak(server, message);
            }
            else
            {
                offer.Acknowledged = false;
                offer.Expiration = DateTime.Now.Add(server.OfferTimeout);
                offer.SessionId = message.SessionId;
                offer.Owner = clientHardwareAddress;
                SendMessage.Offer(server, message, offer);
            }
        }

        public void DhcpRequest(DhcpServer server, DhcpMessage message)
        {
            byte[] dhcpAddress = message.GetOptionData(DhcpOption.DhcpAddress);
            if (dhcpAddress == null || !dhcpAddress.Equals(server.DhcpInterfaceAddress))
            {
                return;
            }

            byte[] addressRequestData = message.GetOptionData(DhcpOption.AddressRequest);
            if (addressRequestData == null)
            {
                addressRequestData = message.ClientAddress;
            }

            InternetAddress addressRequest = new InternetAddress(addressRequestData);

            if (addressRequest.IsEmpty)
            {
                SendMessage.Nak(server, message);
                return;
            }

            // Assume we're on an ethernet network
            byte[] hardwareAddressData = new byte[6];
            Array.Copy(message.ClientHardwareAddress, hardwareAddressData, 6);
            PhysicalAddress clientHardwareAddress = new PhysicalAddress(hardwareAddressData);

            AddressLease assignment = null;
            bool ack = false;

            // If this client is explicitly allowed, or they are not denied and the allow any flag is set
            if (Acls.Acl.ContainsKey(clientHardwareAddress) && Acls.Acl[clientHardwareAddress] ||
                !Acls.Acl.ContainsKey(clientHardwareAddress) && m_AllowAny)
            {
                if (server.Reservations.ContainsKey(clientHardwareAddress))
                {
                    assignment = new AddressLease(clientHardwareAddress, server.Reservations[clientHardwareAddress], DateTime.Now.Add(server.LeaseDuration));
                    if (addressRequest.Equals(assignment.Address))
                    {
                        ack = true;
                    }
                }
                else
                {
                    lock (this.m_LeaseSync)
                    {
                        if (server.ActiveLeases.ContainsKey(addressRequest) &&
                            (server.ActiveLeases[addressRequest].Owner.Equals(clientHardwareAddress) || server.ActiveLeases[addressRequest].SessionId == message.SessionId))
                        {
                            assignment = server.ActiveLeases[addressRequest];
                            assignment.Acknowledged = true;
                            assignment.Owner = clientHardwareAddress;
                            assignment.Expiration = DateTime.Now.Add(server.LeaseDuration);
                            ack = true;
                        }
                    }
                }
            }

            if (ack)
            {
                SendMessage.Ack(server, message, assignment);
            }
            else
            {
                SendMessage.Nak(server, message);
            }
        }
    }
}
