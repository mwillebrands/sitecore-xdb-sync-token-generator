# Sitecore XDB Sync Token Generator
This application can generate a Sitecore XDB Sync token that is required by the Sitecore XConnect Indexworker to determine which changes need to be indexed.

# How to use
- Download the latest version from the [Releases](https://github.com/mwillebrands/sitecore-xdb-sync-token-generator/releases)
- Run SyncTokenGenerator.exe
- Fill in the connectionstring to your Shard Map Manager database and press enter.  
  _This is the same as the "collection" connectionstring within XConnect, for example;   
  "user id=myuser;password=mypass;data source=localhost;Initial Catalog=Sitecore_Xdb.Collection.ShardMapManager"_
- The token is generated, if you want to update Solr with the new token, type Y and press enter.
- Fill in the connectionstring to your XDB Solr core.  
  _This is the same as the "solrCore" connectionstring within XConnect, for example;  
  "https://solr:8983/solr/sitecore_xdb"_
- After the application is done the sync token is displayed, and according to your choice Solr is updated as well.
