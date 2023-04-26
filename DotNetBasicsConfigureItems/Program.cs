// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using DotNetBasics;
using DotNetBasicsConfigureItems;
using System.Text.Json.Nodes;

Console.WriteLine("DotNet Basics - Configure Items");
Console.WriteLine("In this example a connection to QServer will be established and the Item List read.");
Console.WriteLine("All the ICS42 Modules and Channels will be discovered and configured.");
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

// For this example the HttpClient and JsonNode conversions has been moved to separate files.
// Inspect the HttpQuery and JsonConvert classes for their implementation.

// First Get the /item/list/, this will show which Items are available.
string content = await HttpQuery.Get(url, "/item/list/");
JsonNode jsonNode = JsonConvert.ParseAndCheck(content);

// Build a list of ICS42 Modules and Channels.
// The ItemTypeIdentifier field can be used to distinguish between Item Types. 
const int moduleTypeId = 2;
const int channelTypeId = 4;

// A specific Item can be identified by using the unique ItemNameIdentifier.
const int ics42ModuleNameId = 182;
const int ics42ChannelNameId = 46;

var icsModules = jsonNode.AsArray()
                         .Where(item => item["ItemTypeIdentifier"].GetValue<int>() == moduleTypeId)
                         .Where(item => item["ItemNameIdentifier"].GetValue<int>() == ics42ModuleNameId)
                         .ToList();

var icsChannels = jsonNode.AsArray()
                          .Where(item => item["ItemTypeIdentifier"].GetValue<int>() == channelTypeId)
                          .Where(item => item["ItemNameIdentifier"].GetValue<int>() == ics42ChannelNameId)
                          .ToList();

// Module Items control the sampling rate of their hosted Channels. Lets change it to the highest possible value.
foreach (var icsModule in icsModules)
{
    // First ensure all Modules are Enabled by requesting the Operation Mode settings with the GET /item/operationMode/ endpoint.
    // Use the ItemId parameter when directly interfacing with a specific item.
    // The ItemId can be obtained from the Item List JsonNode.
    content = await HttpQuery.Get(url, "/item/operationMode/", $"?itemId={icsModule["ItemId"]}");
    jsonNode = JsonConvert.ParseAndCheck(content);

    // To change the Operation Mode, search through the Settings field for an entry called Operation Mode.
    var settings = jsonNode["Settings"];
    var operationMode = settings.AsArray()
                                .First(field => field["Name"].ToString() == "Operation Mode");

    // Settings will always have these fields: Name, Type, SupportedValues and Value.
    // To update the Setting, select an Id from the SupportedValues list and assign it to the Value field.
    operationMode["Value"] = operationMode["SupportedValues"].AsArray()
                                                             .First(field => field["Description"].ToString() == "Enabled")["Id"]
                                                             .GetValue<int>();

    // The jsonNode variable should be updated since all these objects are reference types.
    // Last thing to do now is to send it back to QServer.
    await HttpQuery.Put(url, "/item/operationMode/", jsonNode, $"?itemId={icsModule["ItemId"]}");
}

// It is important to understand that, at this point the settings being cached in QServer, it is not applied to the hardware yet.
// You should change all the settings you want to, and apply once at the end.
// Lets continue to change the sample rate.
foreach (var icsModule in icsModules)
{
    // Start by fetching the settings.
    content = await HttpQuery.Get(url, "/item/settings/", $"?itemId={icsModule["ItemId"]}");
    jsonNode = JsonConvert.ParseAndCheck(content);

    // As with the /item/operationmode/ query, the structure of the /item/settings/ response will have a Settings field.
    // Settings are always a list, hence you'll need to search for the field of interest.
    var settings = jsonNode["Settings"];
    var sampleRate = settings.AsArray()
                             .First(field => field["Name"].ToString() == "Sample Rate");

    // Now that we have the Sample Rate field, we can assign a new value from the SupportedValues list.
    sampleRate["Value"] = sampleRate["SupportedValues"].AsArray()
                                                       .First(field => field["Description"].ToString() == "MSR Divide by 4")["Id"]
                                                       .GetValue<int>();

    // The jsonNode variable should be updated since all these objects are reference types.
    // Last thing to do now is to send it back to QServer.
    await HttpQuery.Put(url, "/item/settings/", jsonNode, $"?itemId={icsModule["ItemId"]}");
}

// Now that all of the Modules are enabled, and the sample rate has been changed, Apply to sync the cache with the hardware.
await HttpQuery.Put(url, "/system/settings/apply/");

Console.WriteLine(string.Empty);
Console.WriteLine("Thats it, press any key to exit.");
Console.ReadKey();
Environment.Exit(0);
