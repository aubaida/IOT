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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-isOpen/{id}/{state}")] HttpRequestMessage request,
            string id,
            string state,
            ILogger log)
        {
            log.LogInformation("Getting if to open or not.");
            //addition
            CloudTable table = null;
            CloudTableClient client=null;
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
            status st = new status();
            
            CloudTable garageTable = client.GetTableReference("Garage");
            Boolean isInTheGarage = await isIdInTable(garageTable, "PartitionKey", id);
            if (isInTheGarage && state.Equals("in"))
            {
                st.isOpen = "arleady in the Garage!";
                return st;
            }
            else if (!isInTheGarage && state.Equals("out"))
            {
                st.isOpen = "arleady out of the Garage!";
                return st;
            }
            else if (isInTheGarage && state.Equals("out")) {
                //remove car from garage
                TableOperation retrieve = TableOperation.Retrieve<TableEntity>(id,"car");
                TableResult result = await garageTable.ExecuteAsync(retrieve);
               // Console.WriteLine("here");
                var deleteEntity = (TableEntity)result.Result;
               // Console.WriteLine("here2");
                TableOperation delete = TableOperation.Delete(deleteEntity);
                await garageTable.ExecuteAsync(delete);
                
                st.isOpen = "open";
                return st;
            }
            List<String> users = await GetUsersFromTable(table);
            foreach (string regesterdUser in users)
            {
               
                CloudTable usersTable = client.GetTableReference("Table00" + regesterdUser);
                await usersTable.CreateIfNotExistsAsync();
                Boolean isIdRegestered = await isIdInTable(usersTable, "RowKey", id);
                if (isIdRegestered ) {
                    //add to garage
                    TableEntity newCar = new TableEntity();
                    newCar.PartitionKey = id;
                    newCar.RowKey = "car";

                    TableOperation add = TableOperation.InsertOrReplace(newCar);
                    await garageTable.ExecuteAsync(add);
                    st.isOpen = "open";
                    return st;
                }
            }
            st.isOpen = "don't open";
            return st;
        }
        private static async Task<Boolean> isIdInTable(CloudTable table , String colName , string platId) {
            TableQuery<PlateNumber> idQuery = new TableQuery<PlateNumber>()
               .Where(TableQuery.GenerateFilterCondition(colName, QueryComparisons.Equal, platId));
            TableQuerySegment<PlateNumber> queryResult = await table.ExecuteQuerySegmentedAsync(idQuery, null);
            PlateNumber plateNumber = queryResult.FirstOrDefault();
            if (plateNumber == null)
            {
                return false;
            }

            return true;
            }
     /*   private static async Task<status> GetTheIDstatus(CloudTable garageTable, string platId,string state)
        {
            TableQuery<PlateNumber> idQuery = new TableQuery<PlateNumber>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, platId));
            TableQuerySegment<PlateNumber> queryResult = await garageTable.ExecuteQuerySegmentedAsync(idQuery, null);
            PlateNumber plateNumber = queryResult.FirstOrDefault();
            status st = new status();
            st.isOpen = "open";
            if (plateNumber == null)
            {
                st.isOpen = "don't open";
            }

            return st;
        }*/
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
                TableOperation retrieve = TableOperation.Retrieve<TableEntity>(name, "");
                CloudTable usersTable = client.GetTableReference("Users");
                await usersTable.CreateIfNotExistsAsync();
                TableResult result = await usersTable.ExecuteAsync(retrieve);

                var deleteEntity = (TableEntity)result.Result;

                TableOperation delete = TableOperation.Delete(deleteEntity);

                await usersTable.ExecuteAsync(delete);
                
                return act + " " + name;

            }
            else if (act == "add") {
                Console.Out.WriteLine("in add");
                await table.CreateIfNotExistsAsync();
                CloudTable usersTable = client.GetTableReference("Users");
                await usersTable.CreateIfNotExistsAsync();

                User newUser = new User();
                newUser.PartitionKey = name;
                newUser.RowKey = "";
                newUser.Password = name;
                newUser.UserType = "user";

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
            TableQuery<TableEntity> idQuery = new TableQuery<TableEntity>();
            List<string> users = new List<string>();
            foreach (TableEntity entity in  await cloudTable.ExecuteQuerySegmentedAsync(idQuery,null))
            {
                TableEntity user = new TableEntity();
                user.PartitionKey = entity.PartitionKey;

                users.Add(user.PartitionKey);
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
        public static async Task<List<User>> GetRegesteredCar(
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
            if (user.Equals("admin")) { //must be chaaaaaaange
                users = await GetUsersFromTable(table);
            }else{
                users = new List<string>();
                users.Add(user);
            }
        
            List<User> cars = new List<User>();
            foreach (string regesterdUser in users) {
               /* if (regesterdUser.Equals("admin")) {
                    continue;
                }*/
                CloudTable usersTable = client.GetTableReference("Table00"+ regesterdUser);
                await usersTable.CreateIfNotExistsAsync();
                TableQuery<User> idQuery = new TableQuery<User>();
                foreach (User entity in await usersTable.ExecuteQuerySegmentedAsync(idQuery, null))
                {
                    entity.UserName = regesterdUser;
                    cars.Add((User)entity);
                }
            }

            return cars;
        }

        [FunctionName("post-login")]
        public static async Task<String> isLogin(
           [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
           ILogger log)
        {
            log.LogInformation("is login.");
            String result= "false";
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
            User userLoginRequest = await ExtractContent<User>(request);

            TableQuery<User> idQuery = new TableQuery<User>()
               .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, userLoginRequest.PartitionKey));
            TableQuerySegment<User> queryResult = await table.ExecuteQuerySegmentedAsync(idQuery, null);
            User user = queryResult.FirstOrDefault();

            if (user == null)
            {
                result = "false";
            }
            else {
                if (user.Password.Equals(userLoginRequest.Password)) {
                    result = user.UserType;
                }
                else { result = "false"; }
            }
            return result;

        }
        [FunctionName("post-changePass")]
        public static async Task<String> changePass(
           [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage request,
           ILogger log)
        {
            log.LogInformation("is login.");
            String result = "false";
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
            User userLoginRequest = await ExtractContent<User>(request);

            TableQuery<User> idQuery = new TableQuery<User>()
               .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, userLoginRequest.PartitionKey));
            TableQuerySegment<User> queryResult = await table.ExecuteQuerySegmentedAsync(idQuery, null);
            User user = queryResult.FirstOrDefault();

            if (user == null)
            {
                result = "notChanged";
            }
            else
            {
                user.Password = userLoginRequest.Password;
                
                TableOperation add = TableOperation.InsertOrReplace(user);
                await table.ExecuteAsync(add);
                result = "changed";
            }
            return result;

        }
        private static async Task<T> ExtractContent<T>(HttpRequestMessage request)
        {
            string connectionRequestJson = await request.Content.ReadAsStringAsync();
            Console.Out.Write("the connectionRequestJson is :   " + connectionRequestJson + "\n");
            return JsonConvert.DeserializeObject<T>(connectionRequestJson);
        }

        [FunctionName("get-requests")]
        public static async Task<List<Request>> GetRequests(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-requests/{user}/{isApproved}")] HttpRequestMessage request,
           string user,
           Boolean isApproved,
           ILogger log)
        {
            log.LogInformation("GetRequests");
            //addition
            CloudTable table = null;
            CloudTableClient client = null;
            Boolean isAdmin = false;

        
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                client = account.CreateCloudTableClient();

                table = client.GetTableReference("Requests");
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            if (user.Equals("admin"))
            { //must be chaaaaaaange
                isAdmin = true;
            }  

            return await GetRequests(table , user , isAdmin ,isApproved );
        }
        private static async Task<List<Request>> GetRequests(CloudTable cloudTable, string userName ,Boolean isAdmin, Boolean isApproved )
        {
            TableQuery<Request> idQuery = null;

            List<Request> requests = new List<Request>();
            if (isAdmin)
            {
                idQuery = new TableQuery<Request>()
            .Where(TableQuery.GenerateFilterCondition("approved", QueryComparisons.Equal, "waiting"));

            }
            else {
                if (isApproved.Equals(true))
                {
                    idQuery = new TableQuery<Request>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, userName))
                .Where(TableQuery.GenerateFilterCondition("approved", QueryComparisons.NotEqual, "waiting"));
                }
                else {
                    idQuery = new TableQuery<Request>()
                   .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, userName))
                   .Where(TableQuery.GenerateFilterCondition("approved", QueryComparisons.Equal, "waiting"));
                }
            }
            
            TableQuerySegment<Request> queryResult = await cloudTable.ExecuteQuerySegmentedAsync(idQuery, null);
       
            List<Request> cloudRequest = queryResult.Results;

            return cloudRequest;
        }
        [FunctionName("get-action-request")]
        public static async Task<string> GetActionRequest(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-action-request/{act}/{user}/{owner}/{plateNum}")] HttpRequestMessage request,
           string user,
           string act,
           string owner,
           string plateNum,
           ILogger log)
        {
            log.LogInformation("GetActionRequest");
            //addition
            CloudTable table = null;
            CloudTableClient client = null;

            //first get all users
            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                client = account.CreateCloudTableClient();

                table = client.GetTableReference("Requests");
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            if (act.Equals("add"))
            {
                Request newRequest = new Request();
                newRequest.PartitionKey = user;
                newRequest.RowKey = plateNum;
                newRequest.ownerName = owner;
                newRequest.approved = "waiting";

                TableOperation add = TableOperation.InsertOrReplace(newRequest);
                await table.ExecuteAsync(add);
            }
            else {//remove 
                TableOperation retrieve = TableOperation.Retrieve<Request>(user, plateNum);

                TableResult result = await table.ExecuteAsync(retrieve);

                var deleteEntity = (Request)result.Result;

                TableOperation delete = TableOperation.Delete(deleteEntity);

                await table.ExecuteAsync(delete);
            }

            return act;
        }
        [FunctionName("get-approve-request")] //if true must update the user table 
        public static async Task<string> GetupdateRequest(//if we have to requests that contains the same id number???
          [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "get-approve-request/{act}/{user}/{owner}/{plateNum}")] HttpRequestMessage request,
          string user,
          string act,
          string owner,
          string plateNum,
          ILogger log)
        {
            log.LogInformation("GetActionRequest");
            //addition
            CloudTable table = null;
            CloudTableClient client = null;

            try
            {
                StorageCredentials creds = new StorageCredentials(Environment.GetEnvironmentVariable("accountName"), Environment.GetEnvironmentVariable("accountKey"));
                CloudStorageAccount account = new CloudStorageAccount(creds, useHttps: true);

                client = account.CreateCloudTableClient();

                table = client.GetTableReference("Requests");
                await table.CreateIfNotExistsAsync();

                Console.WriteLine(table.Uri.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
           
              Request newRequest = new Request();
              newRequest.PartitionKey = user;
              newRequest.RowKey = plateNum;
              newRequest.ownerName = owner;
              newRequest.approved = act;

              TableOperation add = TableOperation.InsertOrReplace(newRequest);
              await table.ExecuteAsync(add);
            


            return "done";
        }


    }


    }
    

