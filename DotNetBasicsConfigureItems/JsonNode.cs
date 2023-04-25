// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotNetBasicsConfigureItems
{
    /// <summary>
    /// A helper class to keep the example code short.
    /// </summary>
    public static class JsonConvert
    {
        /// <summary>
        /// This helper function is used to parse the HTTP response from QServer into a JsonNode object.
        /// It will also print the JSON object to the console. Feel free to place a breakpoint after the write 
        /// operation, inspect the Console window to see the JSON structure.
        /// </summary>
        /// <param name="content">Content returned from QServer.</param>
        /// <returns>JsonNode</returns>
        public static JsonNode ParseAndCheck(string content)
        {
            var jsonNode = JsonNode.Parse(content);
            if (jsonNode == null)
            {
                Console.WriteLine("Invalid JSON response received and could not be parsed.");
                Environment.Exit(-1);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine($"Response as JSON:");
            Console.WriteLine($"{jsonNode.ToJsonString(options)}");
            return jsonNode;
        }
    }
}
