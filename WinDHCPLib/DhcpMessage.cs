using System;
using System.Collections.Generic;

namespace WinDHCP.Library
{    
    public enum DhcpOperation : byte
    {
        BootRequest = 0x01,
        BootReply
    }

    public enum HardwareType : byte
    {
        Ethernet = 0x01,        
    }

    public enum DhcpMessageType
    {
        Discover = 0x01,//+
        Offer,//+
        Request,//+
        Decline,
        Ack,//+
        Nak,//+
        Release,
        Inform,
        ForceRenew,
        LeaseQuery,
        LeaseUnassigned,
        LeaseUnknown,
        LeaseActive
    }

    public enum DhcpOption : byte
    {
        Pad = 0x00,
        SubnetMask = 0x01,
        TimeOffset = 0x02,
        Router = 0x03,
        TimeServer = 0x04,
        NameServer = 0x05,
        DomainNameServer = 0x06,
        Hostname = 0x0C,
        DomainNameSuffix = 0x0F,
        AddressRequest = 0x32,
        AddressTime = 0x33,
        DhcpMessageType = 0x35,
        DhcpAddress = 0x36,
        ParameterList = 0x37,
        DhcpMessage = 0x38,
        DhcpMaxMessageSize = 0x39,
        ClassId = 0x3C,
        ClientId = 0x3D,
        AutoConfig = 0x74,
        End = 0xFF
    }

    public class DhcpMessage
    {
        private const uint DhcpOptionsMagicNumber = 1669485411;//99.130.83.99
        private const uint WinDhcpOptionsMagicNumber = 1666417251;//99.83.130.99
        private const int DhcpMinimumMessageSize = 236;

        private DhcpOperation m_Operation = DhcpOperation.BootRequest;
        private HardwareType m_Hardware = HardwareType.Ethernet;
        private byte m_HardwareAddressLength;
        private byte m_Hops;//number of retranslation agents
        private int m_SessionId;
        private ushort m_SecondsElapsed;
        private ushort m_Flags;
        private byte[] m_ClientAddress = new byte[4];
        private byte[] m_AssignedAddress = new byte[4];
        private byte[] m_NextServerAddress = new byte[4];
        private byte[] m_RelayAgentAddress = new byte[4];
        private byte[] m_ClientHardwareAddress = new byte[16];
        private byte[] m_OptionOrdering = new byte[] {};

        private int m_OptionDataSize = 0;
        private Dictionary<DhcpOption, byte[]> m_Options = new Dictionary<DhcpOption, byte[]>();

        public DhcpMessage()
        {
        }

        public DhcpMessage(byte[] data)
        {
            int offset = 0;
            this.m_Operation = (DhcpOperation)data[offset++];
            this.m_Hardware = (HardwareType)data[offset++];
            this.m_HardwareAddressLength = data[offset++];
            this.m_Hops = data[offset++];

            this.m_SessionId = BitConverter.ToInt32(data, offset);
            offset += 4;

            byte[] secondsElapsed = new byte[2];
            Array.Copy(data, offset, secondsElapsed, 0, 2);
            this.m_SecondsElapsed = BitConverter.ToUInt16(ReverseByteOrder(secondsElapsed), 0);
            offset += 2;

            this.m_Flags = BitConverter.ToUInt16(data, offset);
            offset += 2;

            Array.Copy(data, offset, this.m_ClientAddress, 0, 4);
            offset += 4;
            Array.Copy(data, offset, this.m_AssignedAddress, 0, 4);
            offset += 4;
            Array.Copy(data, offset, this.m_NextServerAddress, 0, 4);
            offset += 4;
            Array.Copy(data, offset, this.m_RelayAgentAddress, 0, 4);
            offset += 4;
            Array.Copy(data, offset, this.m_ClientHardwareAddress, 0, 16);
            offset += 16;

            offset += 192; // Skip server host name and boot file

            if (offset + 4 < data.Length &&
                (BitConverter.ToUInt32(data, offset) == DhcpOptionsMagicNumber || BitConverter.ToUInt32(data, offset) == WinDhcpOptionsMagicNumber))
            {
                offset += 4;
                bool end = false;

                while (!end && offset < data.Length)
                {
                    DhcpOption option = (DhcpOption)data[offset];
                    offset++;

                    int optionLen;

                    switch (option)
                    {
                        case DhcpOption.Pad:
                            continue;
                        case DhcpOption.End:
                            end = true;
                            continue;
                        default:
                            optionLen = (int)data[offset];
                            offset++;
                            break;
                    }

                    byte[] optionData = new byte[optionLen];

                    Array.Copy(data, offset, optionData, 0, optionLen);
                    offset += optionLen;

                    this.m_Options.Add(option, optionData);
                    this.m_OptionDataSize += optionLen;
                }
            }
        }

        public DhcpOperation Operation
        {
            get { return this.m_Operation; }
            set { this.m_Operation = value; }
        }

        public HardwareType Hardware
        {
            get { return this.m_Hardware; }
            set { this.m_Hardware = value; }
        }

        public byte HardwareAddressLength
        {
            get { return this.m_HardwareAddressLength; }
            set { this.m_HardwareAddressLength = value; }
        }

        public byte Hops
        {
            get { return this.m_Hops; }
            set { this.m_Hops = value; }
        }

        public int SessionId
        {
            get { return this.m_SessionId; }
            set { this.m_SessionId = value; }
        }

        public ushort SecondsElapsed
        {
            get { return this.m_SecondsElapsed; }
            set { this.m_SecondsElapsed = value; }
        }

        public ushort Flags
        {
            get { return this.m_Flags; }
            set { this.m_Flags = value; }
        }

        public byte[] ClientAddress
        {
            get { return this.m_ClientAddress; }
            set { CopyData(value, this.m_ClientAddress); }
        }

        public byte[] AssignedAddress
        {
            get { return this.m_AssignedAddress; }
            set { CopyData(value, this.m_AssignedAddress); }
        }

        public byte[] NextServerAddress
        {
            get { return this.m_NextServerAddress; }
            set { CopyData(value, this.m_NextServerAddress); }
        }

        public byte[] RelayAgentAddress
        {
            get { return this.m_RelayAgentAddress; }
            set { CopyData(value, this.m_RelayAgentAddress); }
        }

        public byte[] ClientHardwareAddress
        {
            get
            {
                byte[] hardwareAddress = new byte[this.m_HardwareAddressLength];
                Array.Copy(this.m_ClientHardwareAddress, hardwareAddress, this.m_HardwareAddressLength);
                return hardwareAddress;
            }

            set
            {
                CopyData(value, this.m_ClientHardwareAddress);
                this.m_HardwareAddressLength = (byte)value.Length;
                for (int i = value.Length; i < 16; i++)
                {
                    this.m_ClientHardwareAddress[i] = 0;
                }
            }
        }

        public byte[] OptionOrdering
        {
            get { return this.m_OptionOrdering; }
            set { this.m_OptionOrdering = value; }
        }

        public byte[] GetOptionData(DhcpOption option)
        {
            if (this.m_Options.ContainsKey(option))
            {
                return this.m_Options[option];
            }
            else
            {
                return null;
            }
        }

        public void AddOption(DhcpOption option, params byte[] data)
        {
            if (option == DhcpOption.Pad || option == DhcpOption.End)
            {
                throw new ArgumentException("The Pad and End DhcpOptions cannot be added explicitly.", "option");
            }

            this.m_Options.Add(option, data);
            this.m_OptionDataSize += data.Length;
        }

        public bool RemoveOption(DhcpOption option)
        {
            if (this.m_Options.ContainsKey(option))
            {
                this.m_OptionDataSize -= this.m_Options[option].Length;
            }

            return this.m_Options.Remove(option);
        }

        public void ClearOptions()
        {
            this.m_OptionDataSize = 0;
            this.m_Options.Clear();
        }

        public byte[] ToArray()
        {
            byte[] data = new byte[DhcpMinimumMessageSize + (this.m_Options.Count > 0 ? 4 + this.m_Options.Count * 2 + this.m_OptionDataSize + 1 : 0)];

            int offset = 0;

            data[offset++] = (byte)this.m_Operation;
            data[offset++] = (byte)this.m_Hardware;
            data[offset++] = this.m_HardwareAddressLength;
            data[offset++] = this.m_Hops;

            BitConverter.GetBytes(this.m_SessionId).CopyTo(data, offset);
            offset += 4;

            ReverseByteOrder(BitConverter.GetBytes(this.m_SecondsElapsed)).CopyTo(data, offset);
            offset += 2;

            BitConverter.GetBytes(this.m_Flags).CopyTo(data, offset);
            offset += 2;

            this.m_ClientAddress.CopyTo(data, offset);
            offset += 4;

            this.m_AssignedAddress.CopyTo(data, offset);
            offset += 4;

            this.m_NextServerAddress.CopyTo(data, offset);
            offset += 4;

            this.m_RelayAgentAddress.CopyTo(data, offset);
            offset += 4;

            this.m_ClientHardwareAddress.CopyTo(data, offset);
            offset += 16;

            offset += 192;

            if (this.m_Options.Count > 0)
            {
                BitConverter.GetBytes(WinDhcpOptionsMagicNumber).CopyTo(data, offset);
                offset += 4;

                foreach (byte optionId in this.m_OptionOrdering)
                {
                    DhcpOption option = (DhcpOption)optionId;
                    if (this.m_Options.ContainsKey(option))
                    {
                        data[offset++] = optionId;
                        if (this.m_Options[option] != null && this.m_Options[option].Length > 0)
                        {
                            data[offset++] = (byte)this.m_Options[option].Length;
                            Array.Copy(this.m_Options[option], 0, data, offset, this.m_Options[option].Length);
                            offset += this.m_Options[option].Length;
                        }
                    }
                }

                foreach (DhcpOption option in this.m_Options.Keys)
                {
                    if (Array.IndexOf(this.m_OptionOrdering, (byte)option) == -1)
                    {
                        data[offset++] = (byte)option;
                        if (this.m_Options[option] != null && this.m_Options[option].Length > 0)
                        {
                            data[offset++] = (byte)this.m_Options[option].Length;
                            Array.Copy(this.m_Options[option], 0, data, offset, this.m_Options[option].Length);
                            offset += this.m_Options[option].Length;
                        }
                    }
                }

                data[offset] = (byte)DhcpOption.End;
            }

            return data;
        }

        private void CopyData(byte[] source, byte[] dest)
        {
            if (source.Length > dest.Length)
            {
                throw new ArgumentException(string.Format("Values must be no more than {0} bytes.", dest.Length), "value");
            }

            source.CopyTo(dest, 0);
        }

        public static byte[] ReverseByteOrder(byte[] source)
        {
            byte[] reverse = new byte[source.Length];

            int j = 0;            
            for (int i = source.Length - 1; i >= 0; i--)
            {
                reverse[j++] = source[i];
            }

            return reverse;
        }
    }
}
