// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using DotNet_Basics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.WriteLine("DotNet Basics");
Console.WriteLine("In this example we'll connect to QServer and print which Items are available to interact with.");
Console.WriteLine("We will only use standard DotNet classes to accomplish this task.");
Console.WriteLine("First we'll need an IP Address:");

var ipAddress = GetIpAndValidate.AskUser();
var url = $"http://{ipAddress}:8080"; 

// Create an HTTP Client for the communication between QServer and your application.
var client = new HttpClient() { Timeout = new TimeSpan(0, 0, 10) };

// Now build a query.
var query = new StringBuilder();

const string infoPing = "/info/ping/";
query.Append(url);
query.Append(infoPing);

// Submit the query and ensure it returns without any errors.
var response = await client.GetAsync(query.ToString());
response.EnsureSuccessStatusCode();
var content = await response.Content.ReadAsStringAsync();

// Body text from Get actions should always return text.
if (string.IsNullOrEmpty(content))
{
    Console.WriteLine($"The response received from endpoint {infoPing} hat no errors but returned no text either.");
    Environment.Exit(-1);
}

// To display the response in a easy human readable way we can parse it to a JsonNode.
var jsonNode = JsonNode.Parse(content);
if (jsonNode == null)
{
    Console.WriteLine("Invalid JSON response received and could not be parsed.");
    Environment.Exit(-1);
}

var options = new JsonSerializerOptions { WriteIndented = true };
Console.WriteLine($"Endpoint {infoPing} successfully returned: {jsonNode.ToJsonString(options)}");

// Now lets query for a List of Items and display it.
const string itemList = "/item/list/";
query.Clear();
query.Append(url);
query.Append(itemList);

response = await client.GetAsync(query.ToString());
response.EnsureSuccessStatusCode();
content = await response.Content.ReadAsStringAsync();
if (string.IsNullOrEmpty(content))
{
    Console.WriteLine($"The response received from endpoint {itemList} hat no errors but returned no text either.");
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
foreach (var item in jsonNode.AsArray())
{
    Console.WriteLine($"Found Item {item["ItemId"]}: {item["ItemName"]} (ID: {item["ItemNameIdentifier"]})" +
        $" of type {item["ItemType"]} (ID: {item["ItemTypeIdentifier"]})");
}

Console.WriteLine(string.Empty);
Console.WriteLine("Thats it, press any key to exit.");
Console.ReadKey();
Environment.Exit(0);