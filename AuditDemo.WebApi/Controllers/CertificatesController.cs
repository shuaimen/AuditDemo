using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using AuditDemo.WebApi.Infrastructure;
using AuditDemo.WebApi.Models;

namespace AuditDemo.WebApi.Controllers
{
    [RoutePrefix("api/certificates")]
    public class CertificatesController : ApiController
    {
        // List certificates
        [HttpGet]
        [Route("")]
        public IHttpActionResult List(Guid? factoryId = null, string q = null, int? days = null, bool includeFiles = true, bool activeOnly = true)
        {
            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            int d = days.HasValue ? days.Value : -1;
            if (d == 0) d = -1;
            if (d > 3650) d = 3650;

            var like = q == null ? null : ("%" + q + "%");

            var dt = Db.Query(@"
                SELECT TOP 1000
                    c.CertId,
                    c.FactoryId,
                    f.FactoryCode,
                    f.Name AS FactoryName,
                    c.CertName,
                    c.CertNo,
                    c.CertType,
                    c.IssueDate,
                    c.ExpireDate,
                    c.Remark,
                    c.IsActive,
                    DATEDIFF(day, CONVERT(date,GETDATE()), c.ExpireDate) AS DaysLeft
                FROM FactoryCertificate c WITH (NOLOCK)
                JOIN Factory f WITH (NOLOCK) ON c.FactoryId=f.FactoryId
                WHERE f.IsActive=1
                  AND (@fid IS NULL OR c.FactoryId=@fid)
                  AND (@activeOnly=0 OR c.IsActive=1)
                  AND (@q IS NULL OR c.CertName LIKE @like OR c.CertNo LIKE @like OR c.CertType LIKE @like OR c.Remark LIKE @like)
                  AND (@days < 0 OR c.ExpireDate <= DATEADD(day, @days, CONVERT(date,GETDATE())))
                ORDER BY c.ExpireDate ASC, f.FactoryCode, c.CertName
            ",
                Db.P("@fid", (object)factoryId ?? DBNull.Value),
                Db.P("@activeOnly", activeOnly ? 1 : 0),
                Db.P("@q", (object)q ?? DBNull.Value),
                Db.P("@like", (object)like ?? DBNull.Value),
                Db.P("@days", d)
            );

            var certIds = dt.AsEnumerable().Select(r => (Guid)r["CertId"]).Distinct().ToList();
            var filesMap = includeFiles ? LoadFiles(certIds) : new Dictionary<Guid, List<object>>();

            var items = dt.AsEnumerable().Select(r => new
            {
                certId = (Guid)r["CertId"],
                factoryId = (Guid)r["FactoryId"],
                factoryCode = Convert.ToString(r["FactoryCode"]),
                factoryName = Convert.ToString(r["FactoryName"]),
                certName = Convert.ToString(r["CertName"]),
                certNo = Convert.ToString(r["CertNo"]),
                certType = Convert.ToString(r["CertType"]),
                issueDate = r["IssueDate"] == DBNull.Value ? null : Convert.ToDateTime(r["IssueDate"]).ToString("yyyy-MM-dd"),
                expireDate = Convert.ToDateTime(r["ExpireDate"]).ToString("yyyy-MM-dd"),
                daysLeft = Convert.ToInt32(r["DaysLeft"]),
                remark = Convert.ToString(r["Remark"]),
                isActive = Convert.ToBoolean(r["IsActive"]),
                files = includeFiles && filesMap.ContainsKey((Guid)r["CertId"]) ? filesMap[(Guid)r["CertId"]] : null
            }).ToList();

            return Ok(new { items });
        }

        // Get certificate detail
        [HttpGet]
        [Route("{certId:guid}")]
        public IHttpActionResult Get(Guid certId)
        {
            var r = Db.QuerySingle(@"
                SELECT TOP 1
                    c.CertId,
                    c.FactoryId,
                    f.FactoryCode,
                    f.Name AS FactoryName,
                    c.CertName,
                    c.CertNo,
                    c.CertType,
                    c.IssueDate,
                    c.ExpireDate,
                    c.Remark,
                    c.IsActive,
                    DATEDIFF(day, CONVERT(date,GETDATE()), c.ExpireDate) AS DaysLeft
                FROM FactoryCertificate c WITH (NOLOCK)
                JOIN Factory f WITH (NOLOCK) ON c.FactoryId=f.FactoryId
                WHERE c.CertId=@id
            ", Db.P("@id", certId));

            if (r == null) return NotFound();

            var filesMap = LoadFiles(new List<Guid> { certId });

            return Ok(new
            {
                cert = new
                {
                    certId = (Guid)r["CertId"],
                    factoryId = (Guid)r["FactoryId"],
                    factoryCode = Convert.ToString(r["FactoryCode"]),
                    factoryName = Convert.ToString(r["FactoryName"]),
                    certName = Convert.ToString(r["CertName"]),
                    certNo = Convert.ToString(r["CertNo"]),
                    certType = Convert.ToString(r["CertType"]),
                    issueDate = r["IssueDate"] == DBNull.Value ? null : Convert.ToDateTime(r["IssueDate"]).ToString("yyyy-MM-dd"),
                    expireDate = Convert.ToDateTime(r["ExpireDate"]).ToString("yyyy-MM-dd"),
                    daysLeft = Convert.ToInt32(r["DaysLeft"]),
                    remark = Convert.ToString(r["Remark"]),
                    isActive = Convert.ToBoolean(r["IsActive"]),
                    files = filesMap.ContainsKey(certId) ? filesMap[certId] : new List<object>()
                }
            });
        }

        // Create certificate
        [HttpPost]
        [Route("")]
        public IHttpActionResult Create([FromBody] CreateCertificateRequest req)
        {
            if (req == null) return BadRequest("Body is required");
            if (req.FactoryId == Guid.Empty) return BadRequest("FactoryId is required");
            if (string.IsNullOrWhiteSpace(req.CertName)) return BadRequest("CertName is required");
            if (!req.ExpireDate.HasValue) return BadRequest("ExpireDate is required");

            var certId = Db.NewId();
            Db.Execute(@"
                INSERT FactoryCertificate(CertId, FactoryId, CertName, CertNo, CertType, IssueDate, ExpireDate, Remark, IsActive)
                VALUES(@id, @fid, @name, @no, @type, @issue, @exp, @remark, 1)
            ",
                Db.P("@id", certId),
                Db.P("@fid", req.FactoryId),
                Db.P("@name", req.CertName.Trim()),
                Db.P("@no", (object)req.CertNo ?? DBNull.Value),
                Db.P("@type", (object)req.CertType ?? DBNull.Value),
                Db.P("@issue", (object)req.IssueDate ?? DBNull.Value),
                Db.P("@exp", req.ExpireDate.Value),
                Db.P("@remark", (object)req.Remark ?? DBNull.Value)
            );

            return Ok(new { certId });
        }

        // Update certificate
        [HttpPut]
        [Route("{certId:guid}")]
        public IHttpActionResult Update(Guid certId, [FromBody] UpdateCertificateRequest req)
        {
            if (req == null) return BadRequest("Body is required");
            if (string.IsNullOrWhiteSpace(req.CertName)) return BadRequest("CertName is required");
            if (!req.ExpireDate.HasValue) return BadRequest("ExpireDate is required");

            var aff = Db.Execute(@"
                UPDATE FactoryCertificate
                SET CertName=@name,
                    CertNo=@no,
                    CertType=@type,
                    IssueDate=@issue,
                    ExpireDate=@exp,
                    Remark=@remark,
                    IsActive=ISNULL(@active, IsActive),
                    UpdatedAt=GETDATE()
                WHERE CertId=@id
            ",
                Db.P("@id", certId),
                Db.P("@name", req.CertName.Trim()),
                Db.P("@no", (object)req.CertNo ?? DBNull.Value),
                Db.P("@type", (object)req.CertType ?? DBNull.Value),
                Db.P("@issue", (object)req.IssueDate ?? DBNull.Value),
                Db.P("@exp", req.ExpireDate.Value),
                Db.P("@remark", (object)req.Remark ?? DBNull.Value),
                Db.P("@active", req.IsActive.HasValue ? (object)(req.IsActive.Value ? 1 : 0) : DBNull.Value)
            );

            if (aff <= 0) return NotFound();
            return Ok(new { ok = true });
        }

        // Deactivate
        [HttpPost]
        [Route("{certId:guid}/deactivate")]
        public IHttpActionResult Deactivate(Guid certId)
        {
            var aff = Db.Execute(@"
                UPDATE FactoryCertificate
                SET IsActive=0, UpdatedAt=GETDATE()
                WHERE CertId=@id
            ", Db.P("@id", certId));

            if (aff <= 0) return NotFound();
            return Ok(new { ok = true });
        }

        // Upload certificate attachments (images or PDF). Max 5 files per certificate.
        [HttpPost]
        [Route("{certId:guid}/files")]
        public IHttpActionResult UploadFiles(Guid certId)
        {
            var http = HttpContext.Current;
            if (http == null) return BadRequest("No HttpContext");

            var files = http.Request.Files;
            if (files == null || files.Count <= 0) return BadRequest("No files");

            // Ensure cert exists
            var exists = Db.Scalar("SELECT COUNT(1) FROM FactoryCertificate WITH (NOLOCK) WHERE CertId=@id", Db.P("@id", certId));
            if (exists == null || Convert.ToInt32(exists) <= 0) return NotFound();

            var existingCountObj = Db.Scalar("SELECT COUNT(1) FROM FactoryCertificateFile WITH (NOLOCK) WHERE CertId=@id", Db.P("@id", certId));
            var existingCount = existingCountObj == null ? 0 : Convert.ToInt32(existingCountObj);

            if (existingCount >= 5) return BadRequest("附件已达上限（5）");

            var toAdd = files.Count;
            if (existingCount + toAdd > 5) return BadRequest("附件超出上限（5）");

            var userId = Auth.CurrentUserId;
            var subDir = "certificates/" + certId.ToString("N");

            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                var saved = FileStorage.SaveAttachment(f, subDir);

                var fileId = Db.NewId();
                var sortNo = existingCount + i + 1;

                // store original name for UI
                var origName = Path.GetFileName(f.FileName);

                Db.Execute(@"
                    INSERT FactoryCertificateFile(FileId, CertId, RelativePath, FileName, SizeBytes, SortNo, UploadedBy)
                    VALUES(@fid, @cid, @path, @name, @size, @sort, @uid)
                ",
                    Db.P("@fid", fileId),
                    Db.P("@cid", certId),
                    Db.P("@path", saved.RelativePath),
                    Db.P("@name", (object)origName ?? saved.FileName),
                    Db.P("@size", saved.SizeBytes),
                    Db.P("@sort", sortNo),
                    Db.P("@uid", userId)
                );
            }

            var map = LoadFiles(new List<Guid> { certId });
            return Ok(new { files = map.ContainsKey(certId) ? map[certId] : new List<object>() });
        }

        // Delete attachment
        [HttpDelete]
        [Route("files/{fileId:guid}")]
        public IHttpActionResult DeleteFile(Guid fileId)
        {
            var row = Db.QuerySingle(@"
                SELECT TOP 1 FileId, RelativePath
                FROM FactoryCertificateFile WITH (NOLOCK)
                WHERE FileId=@id
            ", Db.P("@id", fileId));

            if (row == null) return NotFound();

            var rel = Convert.ToString(row["RelativePath"]);
            Db.Execute("DELETE FROM FactoryCertificateFile WHERE FileId=@id", Db.P("@id", fileId));

            try
            {
                var abs = HttpContext.Current.Server.MapPath("~\\" + rel.Replace('/', '\\'));
                if (File.Exists(abs)) File.Delete(abs);
            }
            catch
            {
                // ignore
            }

            return Ok(new { ok = true });
        }

        // Expiring list (<= days). Includes expired.
        [HttpGet]
        [Route("expiring")]
        public IHttpActionResult Expiring(int days = 60, Guid? factoryId = null)
        {
            return List(factoryId, null, days, true, true);
        }

        // Export expiring certificates
        [HttpGet]
        [Route("export-expiring")]
        public HttpResponseMessage ExportExpiring(int days = 60, Guid? factoryId = null)
        {
            var bytes = ExcelExporter.ExportCertificateExpiring(days, factoryId);
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new ByteArrayContent(bytes);
            resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            resp.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = string.Format("cert_expiring_{0}d.xlsx", days)
            };
            return resp;
        }

        private static Dictionary<Guid, List<object>> LoadFiles(List<Guid> certIds)
        {
            var map = new Dictionary<Guid, List<object>>();
            if (certIds == null || certIds.Count == 0) return map;

            var inSql = string.Join(",", certIds.Select((_, i) => "@p" + i));
            var ps = certIds.Select((id, i) => Db.P("@p" + i, id)).ToArray();

            var dt = Db.Query(@"
                SELECT FileId, CertId, FileName, RelativePath, SizeBytes, SortNo
                FROM FactoryCertificateFile WITH (NOLOCK)
                WHERE CertId IN (" + inSql + @")
                ORDER BY CertId, SortNo
            ", ps);

            foreach (DataRow r in dt.Rows)
            {
                var cid = (Guid)r["CertId"];
                if (!map.ContainsKey(cid)) map[cid] = new List<object>();

                var fid = (Guid)r["FileId"];
                var rel = Convert.ToString(r["RelativePath"]);
                var ext = (Path.GetExtension(rel) ?? "").ToLowerInvariant();

                map[cid].Add(new
                {
                    fileId = fid,
                    fileName = Convert.ToString(r["FileName"]),
                    sortNo = Convert.ToInt32(r["SortNo"]),
                    sizeBytes = Convert.ToInt64(r["SizeBytes"]),
                    isPdf = ext == ".pdf",
                    url = "/api/files/" + fid.ToString("N")
                });
            }

            return map;
        }
    }
}
