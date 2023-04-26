// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using DotNetBasics;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.WriteLine("DotNet Basics - Item List");
Console.WriteLine("In this example a connection to QServer will be established and the Item List read.");
Console.WriteLine("The fields of each Item will be discussed and printed to the Console Window.");
Console.WriteLine("Only standard DotNet classes will be used to accomplish this task.");
Console.WriteLine(string.Empty);

// You can either specify the system IP here or in the console when the application runs.
string ipAddress = "";
if (string.IsNullOrEmpty(ipAddress))
{
    Console.WriteLine("Please specify an IP Address:");
    ipAddress = GetIpAndValidate.AskUser()
                                .ToString();
}

var url = $"http://{ipAddress}:8080";

// Create an HTTP Client for the communication between QServer and your application.
var client = new HttpClient() { Timeout = new TimeSpan(0, 0, 10) };

// Now build a query. The first endpoint demonstrated here will be /info/ping/.
// This will ensure QServer is available on your network and is responding to HTTP queries.
var query = new StringBuilder();
query.Append(url);
query.Append("/info/ping/");

// Submit the query and ensure it returns without any errors.
var response = await client.GetAsync(query.ToString());
response.EnsureSuccessStatusCode();
var content = await response.Content.ReadAsStringAsync();

// Body text from Get actions should always return text.
if (string.IsNullOrEmpty(content))
{
    Console.WriteLine($"The response received from endpoint /info/ping/ had no errors but returned no text either.");
    Environment.Exit(-1);
}

// To display the response in a easy human readable way just parse it to a JsonNode.
var jsonNode = JsonNode.Parse(content);
if (jsonNode == null)
{
    Console.WriteLine("Invalid JSON response received and could not be parsed.");
    Environment.Exit(-1);
}

var options = new JsonSerializerOptions { WriteIndented = true };
Console.WriteLine($"Endpoint /info/ping/ successfully returned: {jsonNode.ToJsonString(options)}");

// Now lets query for a List of Items and display it.
query.Clear();
query.Append(url);
query.Append("/item/list/");

response = await client.GetAsync(query.ToString());
response.EnsureSuccessStatusCode();
content = await response.Content.ReadAsStringAsync();
if (string.IsNullOrEmpty(content))
{
    Console.WriteLine($"The response received from endpoint /item/list/ hat no errors but returned no text either.");
    Environment.Exit(-1);
}

// Convert it to a JSON object for easy manipulation.
jsonNode = JsonNode.Parse(content);
if (jsonNode == null)
{
    Console.WriteLine("Invalid JSON response received and could not be parsed.");
    Environment.Exit(-1);
}

// Print the items to the Console.
// The returned JSON from the /item/list/ endpoint is a list, with each entry containing the following information:
// "ItemId": A unique instance ID for the specified Item. Use this ID when interacting with the Item directly.
// "ItemName": A human readable name for the specific Item.
// "ItemNameIdentifier": A unique ID assigned for the specific Name.
// "ItemType": A human readable name for the Type. Item Types include Controller, Signal Conditioner, Module and Channel.
// "ItemTypeIdentifier": A unique ID assigned for the Type.
foreach (var item in jsonNode.AsArray())
{
    Console.WriteLine($"Found Item {item["ItemId"]}: {item["ItemName"]} (ID: {item["ItemNameIdentifier"]})" +
        $" of type {item["ItemType"]} (ID: {item["ItemTypeIdentifier"]})");
}

Console.WriteLine(string.Empty);
Console.WriteLine("Thats it, press any key to exit.");
Console.ReadKey();
Environment.Exit(0);
