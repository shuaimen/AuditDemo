using System;
using System.IO;
using System.Web;
using System.Web.Http;
using AuditDemo.WebApi.Infrastructure;

namespace AuditDemo.WebApi.Controllers
{
    [RoutePrefix("api/files")]
    public class FilesController : ApiController
    {
        [HttpGet]
        [Route("{id}")]
        public IHttpActionResult Get(string id)
        {
            Guid gid;
            if (!Guid.TryParseExact(id, "N", out gid)) return BadRequest("Invalid id");

            var row = Db.QuerySingle(@"
                SELECT TOP 1 RelativePath FROM AuditClausePhoto WITH (NOLOCK) WHERE PhotoId=@id
                UNION ALL
                SELECT TOP 1 RelativePath FROM CapaEvidencePhoto WITH (NOLOCK) WHERE EvidenceId=@id
                UNION ALL
                SELECT TOP 1 RelativePath FROM ReAuditClausePhoto WITH (NOLOCK) WHERE PhotoId=@id
                UNION ALL
                SELECT TOP 1 RelativePath FROM FactoryCertificateFile WITH (NOLOCK) WHERE FileId=@id
            ", Db.P("@id", gid));

            if (row == null) return NotFound();

            var rel = Convert.ToString(row["RelativePath"]);
            var abs = HttpContext.Current.Server.MapPath("~\\" + rel.Replace('/', '\\'));
            if (!File.Exists(abs)) return NotFound();

            var bytes = File.ReadAllBytes(abs);
            var ext = Path.GetExtension(abs).ToLowerInvariant();
            var ct = ext == ".pdf" ? "application/pdf" : "image/jpeg";
            return new FileResult(bytes, ct);
        }

        private class FileResult : IHttpActionResult
        {
            private readonly byte[] _bytes;
            private readonly string _contentType;
            public FileResult(byte[] bytes, string contentType) { _bytes = bytes; _contentType = contentType; }
            public System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> ExecuteAsync(System.Threading.CancellationToken cancellationToken)
            {
                var resp = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK);
                resp.Content = new System.Net.Http.ByteArrayContent(_bytes);
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);
                return System.Threading.Tasks.Task.FromResult(resp);
            }
        }
    }
}
