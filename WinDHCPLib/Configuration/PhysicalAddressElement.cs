using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;

namespace WinDHCP.Library.Configuration
{
    public class PhysicalAddressElement : ConfigurationElement
    {
        [ConfigurationProperty("physicalAddress", IsRequired = true, IsKey = true)]
        public string PhysicalAddress
        {
            get { return (string)this["physicalAddress"]; }
        }
    }
}
