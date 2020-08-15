using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace CounterFunctions
{
    public class User : TableEntity
    {
        public string UserName { get; set; }

        
    }
}