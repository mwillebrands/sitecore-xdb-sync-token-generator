using Microsoft.Azure.SqlDatabase.ElasticScale.ShardManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

namespace SyncTokenGenerator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Please enter the connectionstring to the shardmapmanager");
            var shardMapManagerConnectionString = Console.ReadLine();
            Dictionary<string, long?> shardChangeVersions = new Dictionary<string, long?>();
            string syncToken = "";

            try
            {
                if (!ShardMapManagerFactory.TryGetSqlShardMapManager(shardMapManagerConnectionString, ShardMapManagerLoadPolicy.Eager, out var shardMapManager))
                {
                    Console.WriteLine("Could not get the shard map manager for the given connectionstring");
                    return;
                }

                var shardMap = shardMapManager.GetShardMaps().FirstOrDefault();
                if (shardMap == null)
                {
                    Console.WriteLine("Could not get a shardmap from the current shard map manager");
                    return;
                }

                foreach (var shard in shardMapManager.GetShardMaps().First().GetShards())
                {
                    var shardConnectionString = new SqlConnectionStringBuilder(Credentials(shardMapManagerConnectionString))
                    {
                        DataSource = shard.Location.DataSource,
                        InitialCatalog = shard.Location.Database
                    }.ConnectionString;

                    using (var connection = new SqlConnection(shardConnectionString))
                    {
                        using (var cmd = new SqlCommand("SELECT CHANGE_TRACKING_CURRENT_VERSION()", connection))
                        {
                            connection.Open();
                            var changeVersion = (long)cmd.ExecuteScalar();
                            Console.WriteLine($"Retrieved change version {changeVersion} for shard {shard.Location}");
                            shardChangeVersions.Add(shard.Location.ToString(), changeVersion);
                        }
                    }
                }

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    BinaryFormatter binaryFormatter = new BinaryFormatter();
                    binaryFormatter.Serialize(memoryStream, shardChangeVersions);
                    syncToken = Convert.ToBase64String(memoryStream.ToArray());
                    Console.WriteLine("The new sync token is:");
                    Console.WriteLine(syncToken);
                }
            }
            catch (Exception ex)
            {
                WriteError("Unable to access the Shard Map Manager database");
                WriteError(ex.Message);
            }

            Console.WriteLine();
            Console.WriteLine("Update XDB SOLR Index? (Y/N)");
            bool updateSolr = Console.ReadLine().ToLowerInvariant() == "y";
            if (updateSolr)
            {
                try
                {
                    Console.WriteLine("Please enter the SOLR XDB connectionstring, for ex https://localhost:8983/solr/sitecore_xdb");
                    var solrConnectionString = Console.ReadLine();
                    if (!solrConnectionString.EndsWith("/"))
                    {
                        solrConnectionString += "/";
                    }
                    solrConnectionString += "update?commit=true";
                    using (var webclient = new WebClient())
                    {
                        var postBody = $"[{{ \"id\":\"xdb-index-token\",\"xdbtokenbytes_s\":\"{syncToken}\"}}]";
                        webclient.Headers.Add("content-type", "application/json");
                        var response = webclient.UploadString(solrConnectionString, "post", postBody);
                        var jsonResponse = !string.IsNullOrEmpty(response) ? JObject.Parse(response) : null; ;
                        if (!int.TryParse((jsonResponse["responseHeader"]?["status"] as JValue)?.Value?.ToString(), out var status) || status != 0)
                        {
                            WriteError("Invalid response recieved from SOLR");
                            WriteError(jsonResponse?.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteError("Unable to update Solr");
                    WriteError(ex.Message);
                }
            }

            Console.WriteLine("Done, press any key to exit");
            Console.ReadLine();
        }

        static string Credentials(string connectionString)
        {
            var source = new SqlConnectionStringBuilder(connectionString);
            var result = new SqlConnectionStringBuilder();
            if (source.IntegratedSecurity)
            {
                result.IntegratedSecurity = source.IntegratedSecurity;
            }
            else
            {
                result.UserID = source.UserID;
                result.Password = source.Password;
            }
            return result.ToString();
        }

        static void WriteError(string message)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }
    }
}
