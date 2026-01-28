using System;
using System.Linq;
using System.Web.Http;
using AuditDemo.WebApi.Infrastructure;
using System.Data;
using System.Linq;

namespace AuditDemo.WebApi.Controllers
{
    [RoutePrefix("api/factories")]
    public class FactoriesController : ApiController
    {
        [HttpGet]
        [Route("")]
        public IHttpActionResult List()
        {
            var dt = Db.Query(@"
                SELECT FactoryId, FactoryCode, Name, ShortName, FactoryType, Address, ContactName, ContactPhone
                FROM Factory WITH (NOLOCK)
                WHERE IsActive=1
                ORDER BY FactoryCode
            ");
            return Ok(new { items = dt.AsEnumerable().Select(r => new {
                factoryId = (Guid)r["FactoryId"],
                factoryCode = Convert.ToString(r["FactoryCode"]),
                name = Convert.ToString(r["Name"]),
                shortName = Convert.ToString(r["ShortName"]),
                factoryType = Convert.ToInt32(r["FactoryType"]),
                address = Convert.ToString(r["Address"]),
                contactName = Convert.ToString(r["ContactName"]),
                contactPhone = Convert.ToString(r["ContactPhone"])
            }).ToList() });
        }
    }
}
