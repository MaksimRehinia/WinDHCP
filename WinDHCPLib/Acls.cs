using System;
using System.Collections.Generic;
using System.Threading;

namespace WinDHCP.Library
{
    public static class Acls
    {
        private static SortedList<PhysicalAddress, bool> m_Acl = new SortedList<PhysicalAddress, bool>();
        private static ReaderWriterLock m_AclLock = new ReaderWriterLock();

        public static SortedList<PhysicalAddress, bool> Acl
        {
            get { return m_Acl; }
            set { m_Acl = value; }
        }

        public static void Add(PhysicalAddress address, bool deny)
        {
            m_AclLock.AcquireWriterLock(Timeout.Infinite);

            try
            {
                if (m_Acl.ContainsKey(address))
                {
                    m_Acl[address] = !deny;
                }
                else
                {
                    m_Acl.Add(address, !deny);
                }
            }
            finally
            {
                m_AclLock.ReleaseLock();
            }
        }

        public static void Remove(PhysicalAddress address)
        {
            m_AclLock.AcquireWriterLock(Timeout.Infinite);

            try
            {
                if (m_Acl.ContainsKey(address))
                {
                    m_Acl.Remove(address);
                }
            }
            finally
            {
                m_AclLock.ReleaseLock();
            }
        }

        public static void Clear()
        {
            m_AclLock.AcquireWriterLock(Timeout.Infinite);

            try
            {
                m_Acl.Clear();
            }
            finally
            {
                m_AclLock.ReleaseLock();
            }

        }        
    }
}
