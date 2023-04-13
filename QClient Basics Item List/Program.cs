// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using QProtocol;
using QProtocol.GenericDefines;
using QProtocolExtended.RestfulClient;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;

Console.WriteLine("Example 1 - Item List");
Console.WriteLine("In this example we'll connect to QServer and print which Items are available to interact with.");
Console.WriteLine("First we'll need an IP Address:");

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

var httpConnection = new RestfulInterface($"http://{ipAddress}:8080");
var qServerPing = httpConnection.Get<InfoPing>(EndPoints.InfoPing);
if (qServerPing.Code != 0
    || qServerPing.Message.Equals("System is operational") == false)
{
    Console.WriteLine($"Unable to reach QServer on {ipAddress}");
    Environment.Exit(-1);
}

Console.WriteLine("Connection established, now we can request the Item list:");
var itemList = httpConnection.Get<List<ItemInfo>>(EndPoints.ItemList);
if (itemList.Count == 0)
{
    Console.WriteLine("Unable to fetch Item Info List.");
    Environment.Exit(-1);
}

foreach (var item in itemList)
{
    Console.WriteLine(JsonSerializer.Serialize(item));
}

Console.WriteLine(string.Empty);
Console.WriteLine("Thats it, press any key to exit.");
Console.ReadKey();
Environment.Exit(0);
