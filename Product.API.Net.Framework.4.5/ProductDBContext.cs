using Logging.Serilog.SendAuditLogSerilogSinkKafka.Enums;
using Product.API.Net.Framework._4._5.Models;
using Serilog;
using Serilog.Sinks.Kafka;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

namespace Product.API.Net.Framework._4._5
{
    public partial class ProductDBContext : DbContext
    {
        private List<AuditEntry> auditEntries = new List<AuditEntry>();

        public ProductDBContext(): base("name=ProductDBContext")
        {
        }

        public virtual DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<Audit>();
            modelBuilder.Ignore<AuditEntry>();
        }

        public virtual async Task<int> SaveChangesAsync()
        {
            OnBeforeSaveChanges(ConfigurationManager.AppSettings["loggedUser"]);
            var result = await base.SaveChangesAsync();

            if (result > 0)
            {
                SendLogToKafka();
            }

            return result;
        }

        public void OnBeforeSaveChanges(string username)
        {
            ChangeTracker.DetectChanges();

            foreach (var entry in ChangeTracker.Entries().Where(p => p.State == EntityState.Added || p.State == EntityState.Deleted || p.State == EntityState.Modified))
            {
                if (entry.Entity is Audit || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;
                var keyNames = entry.Entity.GetType().GetProperties().Where(p => p.GetCustomAttributes(typeof(KeyAttribute), false).Count() > 0).ToList();

                string keyName = keyNames[0].Name;
                

                var auditEntry = new AuditEntry();
                auditEntry.TableName = entry.Entity.GetType().Name;
                auditEntry.Username = username;

                auditEntries.Add(auditEntry);

                if (entry.State == EntityState.Added)
                {
                    string keyValue = (string)entry.CurrentValues.GetValue<object>(keyName);
                    auditEntry.KeyValues[keyName] = keyValue;

                    foreach (var propertyName in entry.CurrentValues.PropertyNames)
                    {
                        auditEntry.AuditType = AuditType.Create;
                        auditEntry.NewValues[propertyName] = entry.CurrentValues.GetValue<object>(propertyName);
                    }
                }
                else if (entry.State == EntityState.Deleted)
                {
                    string keyValue = (string)entry.OriginalValues.GetValue<object>(keyName);
                    auditEntry.KeyValues[keyName] = keyValue;

                    foreach (var propertyName in entry.OriginalValues.PropertyNames)
                    {
                        auditEntry.AuditType = AuditType.Delete;
                        auditEntry.OldValues[propertyName] = entry.OriginalValues.GetValue<object>(propertyName);
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    string keyValue = (string)entry.CurrentValues.GetValue<object>(keyName);
                    auditEntry.KeyValues[keyName] = keyValue;

                    foreach (var propertyName in entry.CurrentValues.PropertyNames)
                    {
                        if (!object.Equals(entry.OriginalValues.GetValue<object>(propertyName), entry.CurrentValues.GetValue<object>(propertyName)))
                        {
                            auditEntry.ChangedColumns.Add(propertyName);
                            auditEntry.AuditType = AuditType.Update;
                            auditEntry.OldValues[propertyName] = entry.OriginalValues.GetValue<object>(propertyName);
                            auditEntry.NewValues[propertyName] = entry.CurrentValues.GetValue<object>(propertyName);
                        }
                    }
                }
            }
        }

        private void SendLogToKafka()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Kafka(new KafkaSinkOptions(topic: ConfigurationManager.AppSettings["topicAudit"], brokers: new[] { new Uri(ConfigurationManager.AppSettings["broker"]) }))
                .CreateLogger();

            foreach (var auditEntry in auditEntries)
            {
                Log.Information(
                      "IP Adress: {IP_Adress}" + "\n"
                    + "Service Name: {Service_Name}" + "\n"
                    + "Username: {Username}" + "\n"
                    + "Type: {Type}" + "\n"
                    + "Table Name: {Table_Name}" + "\n"
                    + "DateTime: {DateTime}"  + "\n"
                    + "Old Values: {Old_Values}" + "\n"
                    + "New Values: {New_Values}" + "\n"
                    + "Affected Column: {Affected_Columns}" + "\n"
                    + "Primary Key: {Primary_Key}" + "\n"
                    + "Version .NET: {Version}" , 
                      GetLocalIPAdress().ToString(), 
                      Assembly.GetExecutingAssembly().FullName.Split(',')[0], 
                      auditEntry.ToAudit().Username, 
                      auditEntry.ToAudit().Type,
                      auditEntry.ToAudit().TableName,
                      auditEntry.ToAudit().DateTime,
                      auditEntry.ToAudit().OldValues,
                      auditEntry.ToAudit().NewValues,
                      auditEntry.ToAudit().AffectedColumns,
                      auditEntry.ToAudit().PrimaryKey,
                      ".NET Framework 4.5"
                    );
            }

            Log.Logger = new LoggerConfiguration()
                .CreateLogger();
        }
        private IPAddress GetLocalIPAdress()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            return host
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }
    }
}
