using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Product.API.Net.Framework._4._5.Models
{
    public class Audit
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Type { get; set; }
        public string TableName { get; set; }
        public DateTime DateTime { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public string AffectedColumns { get; set; }
        public string PrimaryKey { get; set; }
        public string IPAdress { get; set; }
        public string ServiceName { get; set; }
    }
}