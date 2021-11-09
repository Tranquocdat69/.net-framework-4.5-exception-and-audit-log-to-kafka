using Serilog;
using Serilog.Sinks.Kafka;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            catch (Exception e)
            {
                Log.Logger = new LoggerConfiguration()
                .WriteTo.Kafka(new KafkaSinkOptions(topic: "log-tracking-exception", brokers: new[] { new Uri("https://10.26.7.60:9092") }))
                .CreateLogger();

                Log.Error("Error Message: {0}", e.InnerException);

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
