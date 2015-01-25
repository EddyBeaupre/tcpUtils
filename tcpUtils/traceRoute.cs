using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TCPUtils
{
    namespace TraceRoute
    {
        public class traceRouteEntry
        {
            /// <summary>
            /// The hop id. Represents the number of the hop.
            /// </summary>
            public int HopID { get; set; }

            /// <summary>
            /// The IP address.
            /// </summary>
            public string Address { get; set; }

            /// <summary>
            /// The hostname
            /// </summary>
            public string Hostname { get; set; }

            /// <summary>
            /// The reply time it took for the host to receive and reply to the request in milliseconds.
            /// </summary>
            public long ReplyTime { get; set; }

            /// <summary>
            /// The reply status of the request.
            /// </summary>
            public IPStatus ReplyStatus { get; set; }

            public override string ToString()
            {
                return string.Format("{0} | {1} | {2}",
                    HopID,
                    string.IsNullOrEmpty(Hostname) ? Address : Hostname + "[" + Address + "]",
                    ReplyStatus == IPStatus.TimedOut ? "Request Timed Out." : ReplyTime.ToString() + " ms"
                    );
            }
        }

        public class traceRoute
        {
            public List<traceRouteEntry> traceRouteData = new List<traceRouteEntry>();

            /// <summary>
            /// Traces the route which data have to travel through in order to reach an IP address.
            /// </summary>
            /// <param name="ipAddress">The IP address of the destination.</param>
            /// <param name="maxHops">Max hops to be returned.</param>
            /// <param name="timeout">Timeout for each step.</param>
            /// <param name="resolve">Resolve ip address to dns name.</param>
            public traceRoute(string ipAddress, int maxHops, int timeout, bool resolve)
            {
                IPAddress address;

                // Ensure that the argument address is valid.
                if (!IPAddress.TryParse(ipAddress.ToIPAddress().ToString(), out address))
                    throw new ArgumentException(string.Format("{0} is not a valid IP address.", ipAddress));

                // Max hops should be at least one or else there won't be any data to return.
                if (maxHops < 1)
                    throw new ArgumentException("Max hops can't be lower than 1.");

                // Ensure that the timeout is not set to 0 or a negative number.
                if (timeout < 1)
                    throw new ArgumentException("Timeout value must be higher than 0.");


                Ping ping = new Ping();
                PingOptions pingOptions = new PingOptions(1, true);
                Stopwatch pingReplyTime = new Stopwatch();
                PingReply reply;

                do
                {
                    pingReplyTime.Start();
                    reply = ping.Send(address, timeout, new byte[] { 0 }, pingOptions);
                    pingReplyTime.Stop();

                    string hostname = string.Empty;
                    if ((reply.Address != null) && (resolve == true))
                    {
                        try
                        {
                            //hostname = Dns.GetHostByAddress(reply.Address).HostName;    
                            hostname = Dns.GetHostEntry(reply.Address).HostName; // Retrieve the hostname for the replied address.
                        }
                        catch (SocketException) { /* No host available for that address. */ }
                    }

                    // Return out TracertEntry object with all the information about the hop.
                    traceRouteData.Add(new traceRouteEntry()
                    {
                        HopID = pingOptions.Ttl,
                        Address = reply.Address == null ? "N/A" : reply.Address.ToString(),
                        Hostname = hostname,
                        ReplyTime = pingReplyTime.ElapsedMilliseconds,
                        ReplyStatus = reply.Status
                    });

                    pingOptions.Ttl++;
                    pingReplyTime.Reset();
                }
                while (reply.Status != IPStatus.Success && pingOptions.Ttl <= maxHops);
            }
        }
    }
}
