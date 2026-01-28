using System.Net.Http.Headers;
using System.Web.Http;
using Newtonsoft.Json.Serialization;
using AuditDemo.WebApi.Infrastructure;

namespace AuditDemo.WebApi.App_Start
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Auth handler (X-Token)
            config.MessageHandlers.Add(new TokenAuthHandler());

            // JSON: camelCase
            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            json.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;

            // Allow JSON on browser
            json.SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/html"));

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
