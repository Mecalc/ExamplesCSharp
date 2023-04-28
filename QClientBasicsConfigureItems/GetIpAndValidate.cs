// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using System.Net;
using System.Net.NetworkInformation;

namespace QClientBasics
{
    public static class GetIpAndValidate
    {
        public static IPAddress AskUser()
        {
            Console.WriteLine("Please specify an IP Address:");
            var potentialIPAddress = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(potentialIPAddress))
            {
                Console.WriteLine("Invalid IP specified.");
                Environment.Exit(-1);
            }

            var ipAddress = IPAddress.Parse(potentialIPAddress);
            if (ipAddress == null)
            {
                Console.WriteLine("Invalid IP specified.");
                Environment.Exit(-1);
            }

            Console.WriteLine($"Pinging {ipAddress} to see if it is reachable on the network.");
            var pingRequest = new Ping().Send(ipAddress);
            if (pingRequest.Status != IPStatus.Success)
            {
                Console.WriteLine($"Unable to reach QServer on {ipAddress}");
                Environment.Exit(-1);
            }

            return ipAddress;
        }

        public static void PingIp(string ipAddress)
        {
            Console.WriteLine($"Pinging QServer on {ipAddress} to see if it is running.");
            var pingRequest = new Ping().Send(ipAddress);
            if (pingRequest.Status != IPStatus.Success)
            {
                Console.WriteLine($"Unable to reach QServer on {ipAddress}");
                Environment.Exit(-1);
            }
        }
    }
}
