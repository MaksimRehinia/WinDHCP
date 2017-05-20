using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace WinDHCP.Library
{
    public class InternetAddress : IComparable, IEquatable<InternetAddress>
    {
        public static readonly InternetAddress Empty = new InternetAddress(0, 0, 0, 0);
        public static readonly InternetAddress Broadcast = new InternetAddress(255, 255, 255, 255);

        private byte[] m_Address = new byte[] { 0, 0, 0, 0 };

        public InternetAddress(params byte[] address)
        {
            if (address == null || address.Length != 4)
            {
                throw new ArgumentException("Address must have a length of 4.", "address");
            }

            address.CopyTo(this.m_Address, 0);
        }

        public byte this[int index]
        {
            get { return this.m_Address[index]; }
        }

        public bool IsEmpty
        {
            get { return this.Equals(Empty); }
        }

        public bool IsBroadcast
        {
            get { return this.Equals(Broadcast); }
        }

        internal InternetAddress NextAddress()
        {
            InternetAddress next = this.Copy();

            if (this.m_Address[3] == 255)
            {
                next.m_Address[3] = 0;

                if (this.m_Address[2] == 255)
                {
                    next.m_Address[2] = 0;

                    if (this.m_Address[1] == 255)
                    {
                        next.m_Address[1] = 0;

                        if (this.m_Address[0] == 255)
                        {
                            throw new InvalidOperationException();
                        }
                        else
                        {
                            next.m_Address[0] = (byte)(this.m_Address[0] + 1);
                        }
                    }
                    else
                    {
                        next.m_Address[1] = (byte)(this.m_Address[1] + 1);
                    }
                }
                else
                {
                    next.m_Address[2] = (byte)(this.m_Address[2] + 1);
                }
            }
            else
            {
                next.m_Address[3] = (byte)(this.m_Address[3] + 1);
            }

            return next;
        }

        /// <summary>
        /// Compares two ip addresses
        /// </summary>
        /// <param name="obj">address to compare with current</param>
        /// <returns> 1 if first bigger, -1 if smaller, 0 if equals</returns>
        public int CompareTo(object obj)
        {
            InternetAddress other = obj as InternetAddress;

            if (other == null)
            {
                return 1;
            }

            for (int i = 0; i < 4; i++)
            {
                if (this.m_Address[i] > other.m_Address[i])
                {
                    return 1;
                }
                else if (this.m_Address[i] < other.m_Address[i])
                {
                    return -1;
                }
            }

            return 0;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as InternetAddress);
        }

        public bool Equals(InternetAddress other)
        {
            return other != null &&
                this.m_Address[0] == other.m_Address[0] &&
                this.m_Address[1] == other.m_Address[1] &&
                this.m_Address[2] == other.m_Address[2] &&
                this.m_Address[3] == other.m_Address[3];
        }

        public override int GetHashCode()
        {
            return BitConverter.ToInt32(this.m_Address, 0);
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}.{2}.{3}", this[0], this[1], this[2], this[3]);
        }

        public InternetAddress Copy()
        {
            return new InternetAddress(this.m_Address[0], this.m_Address[1], this.m_Address[2], this.m_Address[3]);
        }

        public byte[] ToArray()
        {
            byte[] array = new byte[4];
            this.m_Address.CopyTo(array, 0);
            return array;
        }

        public static InternetAddress Parse(string address)
        {
            return new InternetAddress(IPAddress.Parse(address).GetAddressBytes());
        }
    }
}
