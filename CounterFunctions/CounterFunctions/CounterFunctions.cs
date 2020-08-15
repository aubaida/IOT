using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.Devices;
using System.Collections.Generic;

namespace CounterFunctions
{
    
    public static class CounterFunctions
    {
        //add
        static RegistryManager registryManager;
        private static string connectionString = "HostName=FirstTry1.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=pM+0OHYzhamTy1GSRFfasj7q7Hi2TARGlVr9yb7dNuk=";
        //end_add
        private static readonly AzureSignalR SignalR = new AzureSignalR(Environment.GetEnvironmentVariable("AzureSignalRConnectionString"));
      //  static string accountName = "firsttry1";
      //  static string accountKey = "pfXP7PSpVukhCQmIKLv44hRo93hnZWuyt3D/TVL5+ImwIeXX0BAOlMvhsBV96eD5rbS465e8I/6JgQmsV4tlzg==";
     /*   [FunctionName("negotiate")]
        public static async Task<SignalRConnectionInfo> NegotiateConnection(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage request,
            ILogger log)
        {
            try
            {
                ConnectionRequest connectionRequest = await ExtractContent<ConnectionRequest>(request);
                log.LogInformation($"Negotiating connection for user: <{connectionRequest.UserId}>.");

                string clientHubUrl = SignalR.GetClientHubUrl("CounterHub");
                string accessToken = SignalR.GenerateAccessToken(clientHubUrl, connectionRequest.UserId);
                
                return new SignalRConnectionInfo { AccessToken = accessToken, Url = clientHubUrl };
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to negotiate connection.");
                throw;
            }
        }
        //addition
        //public static CloudTable table;
        //end_addition
        [FunctionName("update-counter")]
        public static async Task UpdateCounter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
            [Table("CountersTable")] CloudTable cloudTable,
            [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            //addition
            CloudTable table=null;
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable( "accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                CloudTableClient client = account.CreateCloudTableClient();

                table = client.GetTableReference("counters");
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
           // table = new CloudTable(siteURL);
            //end_addition
            log.LogInformation("Updating counter.");

            Counter counterRequest = await ExtractContent<Counter>(request);

            Counter cloudCounter = await GetOrCreateCounter(table, counterRequest.Id);
            cloudCounter.Count++;
            log.LogInformation("************the cloud counter ID="+ cloudCounter.Id+" ,count="+ cloudCounter.Count);
            ConnectionRequest connectionRequest = await ExtractContent<ConnectionRequest>(request);
            TableOperation updateOperation = TableOperation.InsertOrReplace(cloudCounter);
            await table.ExecuteAsync(updateOperation);

            await signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "CounterUpdate",
                    Arguments = new object[] { cloudCounter }
                });
        }

        [FunctionName("get-counter")]
        public static async Task<Counter> GetCounter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-counter/{id}")] HttpRequestMessage request,
            [Table("CountersTable")] CloudTable cloudTable,
            string id,
            ILogger log)
        {
            log.LogInformation("Getting counter.");
            //addition
            CloudTable table = null;
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                CloudTableClient client = account.CreateCloudTableClient();

                table = client.GetTableReference("counters");
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            // table = new CloudTable(siteURL);
            //end_addition
            return await GetOrCreateCounter(table, int.Parse(id));
        }

        private static async Task<T> ExtractContent<T>(HttpRequestMessage request)
        {
            string connectionRequestJson = await request.Content.ReadAsStringAsync();
            Console.Out.Write("the connectionRequestJson is :   "+connectionRequestJson +"\n");
            return JsonConvert.DeserializeObject<T>(connectionRequestJson);
        }

        private static async Task<Counter> GetOrCreateCounter(CloudTable cloudTable, int counterId)
        {
            TableQuery<Counter> idQuery = new TableQuery<Counter>()
                .Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, counterId.ToString()));
         //   Console.Out.Write("111111111111111111111111111111\n");
            TableQuerySegment<Counter> queryResult = await cloudTable.ExecuteQuerySegmentedAsync(idQuery,null);
        //    Console.Out.Write("12222222222222222222222222222\n");
            Counter cloudCounter = queryResult.FirstOrDefault();
        //    Console.Out.Write("3333333333333333333333333333333333333333\n");
            if (cloudCounter == null)
            { 
                cloudCounter = new Counter { Id = counterId };

                TableOperation insertOperation = TableOperation.InsertOrReplace(cloudCounter);
                cloudCounter.PartitionKey = "counter";
                cloudCounter.RowKey = cloudCounter.Id.ToString();
                TableResult tableResult = await cloudTable.ExecuteAsync(insertOperation);
                return await GetOrCreateCounter(cloudTable, counterId);
            }

            return cloudCounter;
        }
        [FunctionName("get-devices")]
        public static async Task<Device[]> getDevices(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
            [Table("CountersTable")] CloudTable cloudTable,
            [SignalR(HubName = "CounterHub")] IAsyncCollector<SignalRMessage> signalRMessages,
            ILogger log)
        {
            log.LogInformation("getting the devices.");
            List<string> devices = new List<string>();

            //add
            registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            IQuery query = registryManager.CreateQuery("SELECT * FROM devices", 100);
            while (query.HasMoreResults)
            {
                var page = await query.GetNextAsTwinAsync();
                foreach (var twin in page)
                {
                    Console.Out.Write("the twin DeviceId :" + twin.DeviceId + "\n");
                    devices.Add(twin.DeviceId);
                }
            }
            //end_add
            Device[] retList = new Device[devices.Count];
            int index = 0;
            foreach (string s in devices) {
                Device d = new Device();
                d.id = s;
                retList[index]=d;
                index++;
            }
            return retList;

        }
*/
        [FunctionName("get-isOpen")]
        public static async Task<status> GetIfIsOpen(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-isOpen/{id}")] HttpRequestMessage request,
            [Table("Garage")] CloudTable cloudTable,
            string id,
            ILogger log)
        {
            log.LogInformation("Getting if to open or not.");
            //addition
            CloudTable table = null;
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                CloudTableClient client = account.CreateCloudTableClient();

                table = client.GetTableReference("Garage");
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            // table = new CloudTable(siteURL);
            //end_addition
            Console.WriteLine("*********");
            Console.WriteLine(await GetTheIDstatus(table, id));
            return await GetTheIDstatus(table,id);
        }
        private static async Task<status> GetTheIDstatus(CloudTable cloudTable, string platId)
        {
            TableQuery<PlateNumber> idQuery = new TableQuery<PlateNumber>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, platId));
            TableQuerySegment<PlateNumber> queryResult = await cloudTable.ExecuteQuerySegmentedAsync(idQuery, null);
            PlateNumber plateNumber = queryResult.FirstOrDefault();
            status st = new status();
            st.isOpen = "open";
            if (plateNumber == null)
            {
                st.isOpen = "don't open";
            }

            return st;
        }
        [FunctionName("update-User")]
        public static async Task<String> updateUser(
          [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "update-User/{act}/{name}")] HttpRequestMessage request,
          [Table("Users")] CloudTable cloudTable,
          string name,
          string act,
          ILogger log)
        {
            Console.Out.WriteLine("in updateUser");
            //addition
            CloudTable table = null;
            CloudTableClient client = null;
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                client = account.CreateCloudTableClient();
                table = client.GetTableReference("Table00"+name);
                
                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            
            if (act.Equals("remove"))
            {
                Console.Out.WriteLine("in remove");
                await table.DeleteIfExistsAsync();
                //delete user from Users
                TableOperation retrieve = TableOperation.Retrieve<User>(name, "");
                CloudTable usersTable = client.GetTableReference("Users");
                await usersTable.CreateIfNotExistsAsync();
                TableResult result = await usersTable.ExecuteAsync(retrieve);

                var deleteEntity = (User)result.Result;

                TableOperation delete = TableOperation.Delete(deleteEntity);

                await usersTable.ExecuteAsync(delete);
                
                return act + " " + name;

            }
            else if (act == "add") {
                Console.Out.WriteLine("in add");
                await table.CreateIfNotExistsAsync();
                CloudTable usersTable = client.GetTableReference("Users");
                await usersTable.CreateIfNotExistsAsync();

                TableEntity newUser = new TableEntity();
                newUser.PartitionKey = name;
                newUser.RowKey = "";

                TableOperation add = TableOperation.InsertOrReplace(newUser);
                await usersTable.ExecuteAsync(add);

                return act + " " + name ;

            }
            return act +" "+ name +" error in action";

        }

        [FunctionName("get-All-Users")]
        public static async Task<List<string>> GetUsers(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-All-Users/")] HttpRequestMessage request,
           [Table("Users")] CloudTable cloudTable,
           
           ILogger log)
        {
            Console.WriteLine("in get-All-Users");
            //addition
            CloudTable table = null;
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                CloudTableClient client = account.CreateCloudTableClient();

                table = client.GetTableReference("Users");
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            Console.WriteLine("callin GetUsersFromTable");
            return await GetUsersFromTable(table);
        }
        private static async Task<List<string>> GetUsersFromTable(CloudTable cloudTable)
        {
            TableQuery<User> idQuery = new TableQuery<User>();
            List<string> users = new List<string>();
            foreach (User entity in  await cloudTable.ExecuteQuerySegmentedAsync(idQuery,null))
            {
                User user = new User();
                user.UserName = entity.PartitionKey;

                users.Add(user.UserName);
            }
            return users;
        }




        [FunctionName("add-PlateNumber")]
        public static async Task<String> GetaddPlateNymber(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "add-PlateNumber/{user}/{id}/{owner}")] HttpRequestMessage request,
            string id,
            string user,
            string owner,
            ILogger log)
        {
            log.LogInformation("add PlateNumber.");
            //addition
            CloudTable table = null;
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                CloudTableClient client = account.CreateCloudTableClient();

                table = client.GetTableReference("Table00"+user);
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
 
            return await updatePlateNumber(table, id, owner , "add");
        }

        [FunctionName("remove-PlateNumber")]
        public static async Task<String> GetRemovePlateNymber(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "remove-PlateNumber/{user}/{id}/{owner}")] HttpRequestMessage request,
            string id,
            string user,
            string owner,
            ILogger log)
        {
            log.LogInformation("remove PlateNumber.");
            //addition
            CloudTable table = null;
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                CloudTableClient client = account.CreateCloudTableClient();

                table = client.GetTableReference("Table00" + user);
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return await updatePlateNumber(table, id, owner, "remove");
        }


        private static async Task<String> updatePlateNumber(CloudTable cloudTable, string id, string owner, string action)
        {
            if (action.Equals("add")) { 
            try
            {
                PlateNumber newPlate = new PlateNumber();
                newPlate.PartitionKey = owner;
                newPlate.RowKey = id;

                TableOperation add = TableOperation.InsertOrReplace(newPlate);
                await cloudTable.ExecuteAsync(add);
            }
            catch (Exception e)
            {
                return e.Message;
            }
            return "Success";
            // action=='remove'
            }else{
                try
                {
                    List<string> list = new List<string>();
                    list.Add(id);
                    TableOperation retrieve = TableOperation.Retrieve<PlateNumber>(owner, id);

                    TableResult result = await cloudTable.ExecuteAsync(retrieve);

                    var deleteEntity = (PlateNumber)result.Result;

                    TableOperation delete = TableOperation.Delete(deleteEntity);

                    await cloudTable.ExecuteAsync(delete);
                }
                catch (Exception e)
                {
                    return e.Message;
                }
                return "Success";
            }
        }
        [FunctionName("get-regestered-cars")]
        public static async Task<List<TableEntity>> GetRegesteredCar(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-regestered-cars/{user}")] HttpRequestMessage request,
           string user,
           ILogger log)
        {
            log.LogInformation("GetRegesteredCar");
            //addition
            CloudTable table = null;
            CloudTableClient client = null;
            //first get all users
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                client = account.CreateCloudTableClient();

                table = client.GetTableReference("Users");
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            List<string> users;
            if (user.Equals("admin")) { 
                users = await GetUsersFromTable(table);
            }else{
                users = new List<string>();
                users.Add(user);
            }
        
            List<TableEntity> cars = new List<TableEntity>();
            foreach (string regesterdUser in users) {
                if (regesterdUser.Equals("admin")) {
                    continue;
                }
                CloudTable usersTable = client.GetTableReference("Table00"+ regesterdUser);
                await usersTable.CreateIfNotExistsAsync();
                TableQuery<User> idQuery = new TableQuery<User>();
                foreach (User entity in await usersTable.ExecuteQuerySegmentedAsync(idQuery, null))
                {
                    cars.Add((User)entity);
                }
            }

            return cars;
        }

    }





    }
    

