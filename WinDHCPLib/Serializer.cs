using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace WinDHCP.Library
{
    internal static class Serializer
    {
        internal static void SaveActiveLeases(Dictionary<InternetAddress, AddressLease> activeLeases)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.TypeNameHandling = TypeNameHandling.All;
            serializer.Formatting = Formatting.Indented;
            try
            {
                using (StreamWriter fs = new StreamWriter("..//..//active_leases", false))
                {
                    if (activeLeases != null)
                    {
                        var tempActiveLeases = new Dictionary<string, AddressLease>();
                        foreach (var address in activeLeases.Keys)
                        {
                            string adr = address.ToString();
                            tempActiveLeases.Add(adr, activeLeases[address]);
                        }
                        JsonTextWriter wr = new JsonTextWriter(fs);
                        serializer.Serialize(wr, tempActiveLeases);
                        wr.Close();
                    }                    
                }
            }
            catch (Exception ex)
            {
                DhcpServer.TraceException("Error in serializing activeLeases", ex);
            }
        }

        internal static Dictionary<InternetAddress, AddressLease> RestoreActiveLeases()
        {
            JsonSerializer deserializer = new JsonSerializer();
            deserializer.TypeNameHandling = TypeNameHandling.All;

            var activeLeases = new Dictionary<InternetAddress, AddressLease>();
            try
            {
                using (StreamReader fs = new StreamReader("..//..//active_leases", Encoding.ASCII))
                {
                    JsonTextReader wr = new JsonTextReader(fs);
                    var tempActiveLeases = (Dictionary<string, AddressLease>)deserializer.Deserialize(wr);
                    if (tempActiveLeases != null && tempActiveLeases.Count != 0)
                    {
                        foreach (var address in tempActiveLeases.Keys)
                        {
                            InternetAddress adr = InternetAddress.Parse(address);
                            activeLeases.Add(adr, tempActiveLeases[address]);
                        }
                    }                    
                    wr.Close();
                }                
            }
            catch (Exception ex)
            {
                DhcpServer.TraceException("Error in deserializing activeLeases", ex);
            }
            return activeLeases;
        }
    }
}
