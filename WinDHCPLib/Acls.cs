using System;
using System.Collections.Generic;

namespace WinDHCP.Library
{
    public static class Acls
    {
        private static SortedList<PhysicalAddress, bool> m_Acl = new SortedList<PhysicalAddress, bool>();        

        public static SortedList<PhysicalAddress, bool> Acl
        {
            get { return m_Acl; }
            set { m_Acl = value; }
        }

        public static void Add(PhysicalAddress address, bool deny)
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

        public static void Remove(PhysicalAddress address)
        {           
            if (m_Acl.ContainsKey(address))
            {
                m_Acl.Remove(address);
            }            
        }

        public static void Clear()
        {
            m_Acl.Clear();
        }        
    }
}
