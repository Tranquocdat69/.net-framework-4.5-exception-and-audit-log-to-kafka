using Serilog;
using Serilog.Sinks.Kafka;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace Product.API.Net.Framework._4._5.Controllers
{
    [RoutePrefix("api")]
    public class ProductController : ApiController
    {
        [HttpGet]
        [Route("allProducts")]
        public IHttpActionResult GetAllProducts()
        {
            IList<Product> products = null;

            using (var ctx = new ProductDBContext())
            {
                products = ctx.Products.ToList();
            }

            if (products.Count == 0)
            {
                return NotFound();
            }

            return Ok(products);
        }

        [HttpPost]
        [Route("addProduct")]
        public async Task<IHttpActionResult> AddNewProduct(Product product)
        {
            try
            {
                using (var ctx = new ProductDBContext())
                {
                    ctx.Products.Add(product);
                    await ctx.SaveChangesAsync();
                }
                return Ok(product);
            }
            catch (Exception ex)
            {
                Log.Logger = new LoggerConfiguration()
                .WriteTo.Kafka(new KafkaSinkOptions(topic: ConfigurationManager.AppSettings["topicException"], brokers: new[] { new Uri(ConfigurationManager.AppSettings["broker"]) }))
                .CreateLogger();

                Log.Error("Log Written Date: {DateTime}"+"\n"
                         +"Service Name: {Service_Name}" + "\n"
                         +"Error Line No: {Line}" + "\n"
                         +"Error Message: {Message}" + "\n" 
                         +"Exeption Type: {Exception_Type}" + "\n" 
                         +"Error Url: {Error_Url}" + "\n"
                         +"IP Adress: {IP_Adress}" + "\n"
                         +"Logged in user: {User}" + "\n",
                         DateTime.Now.ToString(),
                         Assembly.GetExecutingAssembly().FullName.Split(',')[0],
                         ex.StackTrace.Substring(ex.StackTrace.Length - 7, 7),
                         ex.InnerException.ToString(),
                         ex.GetType().ToString(),
                         HttpContext.Current.Request.Url.ToString(),
                         Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork),
                         ConfigurationManager.AppSettings["loggedUser"]);

                return Conflict();
            }
            finally
            {
                Log.Logger = new LoggerConfiguration()
                .CreateLogger();
            }

        }

        [HttpPut]
        [Route("editProduct/{id}")]
        public async Task<IHttpActionResult> EditProduct(string id,Product product)
        {
            if(id != product.Id)
            {
                return BadRequest("Product Id is not match with data in body: ");
            }

            using (var ctx = new ProductDBContext())
            {
                var existingProduct = ctx.Products.Find(id);

                if (existingProduct != null)
                {
                    ctx.Entry(existingProduct).CurrentValues.SetValues(product);
                    await ctx.SaveChangesAsync();
                }
                else
                {
                    return NotFound();
                }
            }

            return Ok(product);
        }

        [HttpDelete]
        [Route("deleteProduct/{id}")]
        public async Task<IHttpActionResult> DeleteProduct(string id)
        {
            using (var ctx = new ProductDBContext())
            {
                var existingProduct = ctx.Products.Find(id);

                if (existingProduct != null)
                {
                    ctx.Products.Remove(existingProduct);
                    await ctx.SaveChangesAsync();
                }
                else
                {
                    return NotFound();
                }
            }

            return Ok();
        }
    }
}
