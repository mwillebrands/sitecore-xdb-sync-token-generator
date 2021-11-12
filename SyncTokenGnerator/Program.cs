using Microsoft.Azure.SqlDatabase.ElasticScale.ShardManagement;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
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
                    Console.WriteLine("The new sync token is:");
                    Console.WriteLine(Convert.ToBase64String(memoryStream.ToArray()));
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Unable to access the Shard Map Manager database");
                Console.WriteLine(ex.Message);
            }
                
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
    }
}
