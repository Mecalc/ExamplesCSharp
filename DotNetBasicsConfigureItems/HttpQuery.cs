// -------------------------------------------------------------------------
// Copyright (c) Mecalc (Pty) Limited. All rights reserved.
// -------------------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DotNetBasicsConfigureItems
{
    /// <summary>
    /// This helper class consolidate the HTTP Get and Put methods into this file to keep the example code short.
    /// </summary>
    public static class HttpQuery
    {
        static readonly HttpClient client = new() { Timeout = new TimeSpan(0, 0, 10) };

        /// <summary>
        /// Gets a response from the QServer for the provided endpoint and parameter.
        /// </summary>
        /// <param name="url">Specify the URL of the QServer device.</param>
        /// <param name="endpoint">Specify the endpoint.</param>
        /// <param name="parameters">Specify the required parameters when interfacing with Items directly.</param>
        /// <returns>The QServer response.</returns>
        public static async Task<string> Get(string url, string endpoint, params string[] parameters)
        {
            var stringbuilder = new StringBuilder();
            stringbuilder.Append(url);
            stringbuilder.Append(endpoint);
            if (parameters != null && parameters.Length != 0)
            {
                stringbuilder.Append(string.Join(string.Empty, parameters));
            }

            var query = stringbuilder.ToString();
            Console.WriteLine($"{Environment.NewLine}Get query: {query}");

            var response = await client.GetAsync(query);
            response.EnsureSuccessStatusCode();
            Console.WriteLine($"Response Status Code: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content))
            {
                Console.WriteLine($"The response received from query {query} had no errors but returned no text either.");
                Environment.Exit(-1);
            }

            return content;
        }

        /// <summary>
        /// Puts a query to QServer with an optional JSON body.
        /// </summary>
        /// <param name="url">Specify the URL of the QServer device.</param>
        /// <param name="endpoint">Specify the endpoint.</param>
        /// <param name="body">Optional: A JSON body.</param>
        /// <param name="parameters">Specify the required parameters when interfacing with Items directly.</param>
        /// <returns>An await-able task.</returns>
        public static async Task Put(string url, string endpoint, JsonNode body, params string[] parameters)
        {
            var stringbuilder = new StringBuilder();
            stringbuilder.Append(url);
            stringbuilder.Append(endpoint);
            stringbuilder.Append(string.Join(string.Empty, parameters));

            var query = stringbuilder.ToString();
            var jsonBody = JsonSerializer.Serialize(body);
            Console.WriteLine($"{Environment.NewLine}Put query: {query}; Body: {jsonBody}");

            var response = await client.PutAsync(query, new StringContent(jsonBody, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Console.WriteLine($"Response Status Code: {response.StatusCode}");
        }

        /// <summary>
        /// Puts a query to QServer.
        /// </summary>
        /// <param name="url">Specify the URL of the QServer device.</param>
        /// <param name="endpoint">Specify the endpoint.</param>
        /// <param name="parameters">Specify the required parameters when interfacing with Items directly.</param>
        /// <returns>An await-able task.</returns>
        public static async Task Put(string url, string endpoint, params string[] parameters)
        {
            await Put(url, endpoint, null, parameters);
        }
    }
}
