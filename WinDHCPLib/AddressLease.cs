using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace WinDHCP.Library
{
    [DataContract]
    internal class AddressLease
    {
        [DataMember]
        private PhysicalAddress m_Owner;
        [DataMember]
        private InternetAddress m_Address;
        [DataMember]
        private DateTime m_Expiration;
        [DataMember]
        private int m_SessionId;
        [DataMember]
        private bool m_Acknowledged;

        [DataMember]
        public PhysicalAddress Owner
        {
            get { return this.m_Owner; }
            set { this.m_Owner = value; }
        }

        [DataMember]
        public InternetAddress Address
        {
            get { return this.m_Address; }
            set { this.m_Address = value; }
        }

        [DataMember]
        public DateTime Expiration
        {
            get { return this.m_Expiration; }
            set { this.m_Expiration = value; }
        }

        [DataMember]
        public int SessionId
        {
            get { return m_SessionId; }
            set { m_SessionId = value; }
        }

        [DataMember]
        public bool Acknowledged
        {
            get { return m_Acknowledged; }
            set { m_Acknowledged = value; }
        }

        public AddressLease()
        {
        }

        public AddressLease(PhysicalAddress owner, InternetAddress address)
            : this(owner, address, DateTime.Now.AddDays(1))
        {
        }

        public AddressLease(PhysicalAddress owner, InternetAddress address, DateTime expiration)
        {
            this.m_Owner = owner;
            this.m_Address = address;
            this.m_Expiration = expiration;
        }
    }
}
