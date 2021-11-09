using Logging.Serilog.SendAuditLogSerilogSinkKafka.Enums;
using Product.API.Net.Framework._4._5.Models;
using Serilog;
using Serilog.Sinks.Kafka;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

        public virtual async Task<int> SaveChangesAsync(string username = "admin")
        {
            OnBeforeSaveChanges(username);
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
                .WriteTo.Kafka(new KafkaSinkOptions(topic: "log-audit", brokers: new[] { new Uri("https://10.26.7.60:9092") }))
                .CreateLogger();

            foreach (var auditEntry in auditEntries)
            {
                Log.Information(
                      "IP Adress: " + GetLocalIPAdress().ToString() + "\n"
                    + "Service Name: " + Assembly.GetExecutingAssembly().FullName.Split(',')[0] + "\n"
                    + "Username: " + auditEntry.ToAudit().Username + "\n"
                    + "Type: " + auditEntry.ToAudit().Type + "\n"
                    + "Table Name: " + auditEntry.ToAudit().TableName + "\n"
                    + "DateTime: " + auditEntry.ToAudit().DateTime + "\n"
                    + "Old Values: " + auditEntry.ToAudit().OldValues + "\n"
                    + "New Values: " + auditEntry.ToAudit().NewValues + "\n"
                    + "Affected Column: " + auditEntry.ToAudit().AffectedColumns + "\n"
                    + "Primary Key: " + auditEntry.ToAudit().PrimaryKey + "\n"
                    + "Version .NET: " + "{version}", ".NET Framework 4.5"
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
