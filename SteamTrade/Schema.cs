using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using System.Threading;

namespace SteamTrade
{
    /// <summary>
    /// This class represents the TF2 Item schema as deserialized from its
    /// JSON representation.
    /// </summary>
    public class Schema
    {
        private const string SchemaMutexName = "steam_bot_cache_file_mutex";
        private const string SchemaApiUrlBase = "http://api.steampowered.com/IEconItems_440/GetSchema/v0001/?key=";
        private const string cachefile = "tf_schema.cache";

        /// <summary>
        /// Fetches the Tf2 Item schema.
        /// </summary>
        /// <param name="apiKey">The API key.</param>
        /// <returns>A  deserialized instance of the Item Schema.</returns>
        /// <remarks>
        /// The schema will be cached for future use if it is updated.
        /// </remarks>
        public static Schema FetchSchema (string apiKey)
        {   
            var url = SchemaApiUrlBase + apiKey;

            // just let one thread/proc do the initial check/possible update.
            bool wasCreated;
            var mre = new EventWaitHandle(false, 
                EventResetMode.ManualReset, SchemaMutexName, out wasCreated);

            // the thread that create the wait handle will be the one to 
            // write the cache file. The others will wait patiently.
            if (!wasCreated)
            {
                bool signaled = mre.WaitOne(10000);

                if (!signaled)
                {
                    return null;
                }
            }

            DateTime schemaLastModified = File.Exists(cachefile) ? File.GetCreationTime(cachefile) : default(DateTime);

            string result = String.Empty;
            try
            {
                HttpWebResponse response = SteamWeb.Request(url, "GET", null, null, false, schemaLastModified);
                result = GetSchemaString(response);
            }
            catch (WebException ex)
            {
                result = GetSchemaString((HttpWebResponse)ex.Response);
            }

            // were done here. let others read.
            mre.Set();

            Schema schemaResult = result != null ? JsonConvert.DeserializeObject<SchemaResult>(result).result : null;

            return schemaResult;
        }

        // Gets the schema from the web or from the cached file.
        private static string GetSchemaString(HttpWebResponse response)
        {
            string result = String.Empty;
            if (response.StatusCode != HttpStatusCode.OK)
            {
                response.Close();
                result = File.Exists(cachefile) ? File.ReadAllText(cachefile) : null;
            }
            else
            {
                using (var responseStream = response.GetResponseStream())
                {
                    if (responseStream != null)
                    {
                        using (var reader = new StreamReader(responseStream))
                        {
                            result = reader.ReadToEnd();
                            File.WriteAllText(cachefile, result);
                            File.SetCreationTime(cachefile, response.LastModified);
                        }
                    }
                }
            }
            return result;
        }

        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("items_game_url")]
        public string ItemsGameUrl { get; set; }

        [JsonProperty("items")]
        public Item[] Items { get; set; }

        [JsonProperty("originNames")]
        public ItemOrigin[] OriginNames { get; set; }

        /// <summary>
        /// Find an SchemaItem by it's defindex.
        /// </summary>
        public Item GetItem (int defindex)
        {
            foreach (Item item in Items)
            {
                if (item.Defindex == defindex)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Returns all Items of the given crafting material.
        /// </summary>
        /// <param name="material">Item's craft_material_type JSON property.</param>
        /// <seealso cref="Item"/>
        public List<Item> GetItemsByCraftingMaterial(string material)
        {
            return Items.Where(item => item.CraftMaterialType == material).ToList();
        }

        public List<Item> GetItems()
        {
            return Items.ToList();
        }

        public class ItemOrigin
        {
            [JsonProperty("origin")]
            public int Origin { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }

        public class Item
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("defindex")]
            public ushort Defindex { get; set; }

            [JsonProperty("item_class")]
            public string ItemClass { get; set; }

            [JsonProperty("item_type_name")]
            public string ItemTypeName { get; set; }

            [JsonProperty("item_name")]
            public string ItemName { get; set; }

            [JsonProperty("craft_material_type")]
            public string CraftMaterialType { get; set; }

            [JsonProperty("used_by_classes")]
            public string[] UsableByClasses { get; set; }

            [JsonProperty("item_slot")]
            public string ItemSlot { get; set; }

            [JsonProperty("craft_class")]
            public string CraftClass { get; set; }

            [JsonProperty("item_quality")]
            public int ItemQuality { get; set; }
        }

        protected class SchemaResult
        {
            public Schema result { get; set; }
        }

    }
}

