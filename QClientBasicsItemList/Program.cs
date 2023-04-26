// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using QClient.RestfulClient;
using QClientBasics;
using QProtocol;
using QProtocol.GenericDefines;
using System.Text.Json;

Console.WriteLine("QClient Basics - Item List");
Console.WriteLine("This example will demonstrate some of the helpful features of the QProtocol and QClient libraries.");
Console.WriteLine("A connection to QServer will be established and the Item List read and printed to the Console window in an effortless way.");
Console.WriteLine(string.Empty);

// You can either specify the system IP here or in the console when the application runs.
string ipAddress = "";
if (string.IsNullOrEmpty(ipAddress))
{
    ipAddress = GetIpAndValidate.AskUser().ToString();
    GetIpAndValidate.PingIp(ipAddress);
}

// First create a new instance of the RestfulInterface class. This class provide methods to send Get, Put and Delete requests
// to QServer over a Http protocol.
var httpConnection = new RestfulInterface($"http://{ipAddress}:8080");

// Send the /info/ping/ endpoint with the Get method. The response can be casted to the InfoPing class for easy manipulation.
var qServerPing = httpConnection.Get<InfoPing>(EndPoints.InfoPing);
if (qServerPing.Code != 0
    || qServerPing.Message.Equals("System is operational") == false)
{
    Console.WriteLine($"Unable to reach QServer on {ipAddress}");
    Environment.Exit(-1);
}

Console.WriteLine("Connection established, now we can request the Item list:");

// Next is the /item/list/ endpoint. It will return a List of ItemInfo's.
// Serialization and deserialization of the requests are made easy with the pre constructed classes in the QProtocol Library.
var itemList = httpConnection.Get<List<ItemInfo>>(EndPoints.ItemList);
if (itemList.Count == 0)
{
    Console.WriteLine("Unable to fetch Item Info List.");
    Environment.Exit(-1);
}

// Now to write the Item List to the Console indented we can serialize the class with serialization options.
var options = new JsonSerializerOptions { WriteIndented = true };
foreach (var item in itemList)
{
    Console.WriteLine(JsonSerializer.Serialize(item, options));
}

// Or configure it in your own way.
Console.WriteLine(string.Empty);
foreach (var item in itemList)
{
    Console.WriteLine($"Found Item {item.ItemId}: {item.ItemName} (ID: {item.ItemNameIdentifier})" +
        $" of type {item.ItemType} (ID: {item.ItemTypeIdentifier})");
}

Console.WriteLine(string.Empty);
Console.WriteLine("Thats it, press any key to exit.");
Console.ReadKey();
Environment.Exit(0);
