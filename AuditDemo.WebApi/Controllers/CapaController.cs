using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Http;
using AuditDemo.WebApi.Infrastructure;
using AuditDemo.WebApi.Models;

namespace AuditDemo.WebApi.Controllers
{
    [RoutePrefix("api/capa")]
    public class CapaController : ApiController
    {
        [HttpGet]
        [Route("by-audit/{auditId:guid}")]
        public IHttpActionResult ListByAudit(Guid auditId)
        {
            var dt = Db.Query(@"
                SELECT CapaId, AuditId, ClauseCode, CorrectiveAction, ExternalOwnerName, ExternalOwnerPhone, DueDate, Status, ReviewConclusion, CreatedAt
                FROM CapaItem WITH (NOLOCK)
                WHERE AuditId=@id
                ORDER BY CreatedAt
            ", Db.P("@id", auditId));

            var ev = Db.Query(@"
                SELECT EvidenceId, CapaId, SortNo
                FROM CapaEvidencePhoto WITH (NOLOCK)
                WHERE CapaId IN (SELECT CapaId FROM CapaItem WHERE AuditId=@id)
                ORDER BY SortNo
            ", Db.P("@id", auditId));

            var evMap = ev.AsEnumerable()
                .GroupBy(r => (Guid)r["CapaId"])
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (object)new
                    {
                        evidenceId = (Guid)x["EvidenceId"],
                        sortNo = Convert.ToInt32(x["SortNo"]),
                        url = "/api/files/" + ((Guid)x["EvidenceId"]).ToString("N")
                    }).ToList()
                );

            return Ok(new {
                items = dt.AsEnumerable().Select(r => new {
                    capaId = (Guid)r["CapaId"],
                    auditId = (Guid)r["AuditId"],
                    clauseCode = Convert.ToString(r["ClauseCode"]),
                    correctiveAction = Convert.ToString(r["CorrectiveAction"]),
                    externalOwnerName = Convert.ToString(r["ExternalOwnerName"]),
                    externalOwnerPhone = Convert.ToString(r["ExternalOwnerPhone"]),
                    dueDate = r["DueDate"]==DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["DueDate"]),
                    status = Convert.ToInt32(r["Status"]),
                    statusText = StatusText(Convert.ToInt32(r["Status"])),
                    reviewConclusion = Convert.ToString(r["ReviewConclusion"]),
                    createdAt = Convert.ToDateTime(r["CreatedAt"]),
                    evidence = evMap.ContainsKey((Guid)r["CapaId"]) ? evMap[(Guid)r["CapaId"]] : new List<object>()
                }).ToList()
            });
        }

        [HttpPut]
        [Route("{capaId:guid}")]
        public IHttpActionResult Update(Guid capaId, UpdateCapaRequest req)
        {
            if (req == null) return BadRequest("Missing body");
            Db.Execute(@"
                UPDATE CapaItem
                SET CorrectiveAction=@a,
                    ExternalOwnerName=@n,
                    ExternalOwnerPhone=@p,
                    DueDate=@d,
                    ReviewConclusion=@rc
                WHERE CapaId=@id
            ",
            Db.P("@a", (object)req.CorrectiveAction ?? DBNull.Value),
            Db.P("@n", (object)req.ExternalOwnerName ?? DBNull.Value),
            Db.P("@p", (object)req.ExternalOwnerPhone ?? DBNull.Value),
            Db.P("@d", (object)req.DueDate ?? DBNull.Value),
            Db.P("@rc", (object)req.ReviewConclusion ?? DBNull.Value),
            Db.P("@id", capaId));
            return Ok(new { ok = true });
        }

        [HttpPost]
        [Route("{capaId:guid}/evidence")]
        public IHttpActionResult UploadEvidence(Guid capaId)
        {
            var ctx = HttpContext.Current;
            if (ctx == null) return BadRequest("No HttpContext");
            if (ctx.Request.Files.Count <= 0) return BadRequest("No files");

            var row = Db.QuerySingle("SELECT AuditId, ClauseCode FROM CapaItem WITH (NOLOCK) WHERE CapaId=@id", Db.P("@id", capaId));
            if (row == null) return NotFound();

            var auditId = (Guid)row["AuditId"];
            var clauseCode = Convert.ToString(row["ClauseCode"]);

            var existing = Db.QuerySingle("SELECT COUNT(1) AS Cnt FROM CapaEvidencePhoto WITH (NOLOCK) WHERE CapaId=@id", Db.P("@id", capaId));
            var cnt = Convert.ToInt32(existing["Cnt"]);
            if (cnt >= 5) return BadRequest("Max 5 evidence photos");

            var saved = new List<object>();
            for (int i = 0; i < ctx.Request.Files.Count; i++)
            {
                if (cnt >= 5) break;
                var f = ctx.Request.Files[i];
                var sf = FileStorage.SavePhoto(f, $"CAPA/{auditId:N}/{clauseCode}");
                cnt++;

                var eid = Guid.NewGuid();
                Db.Execute(@"
                    INSERT CapaEvidencePhoto(EvidenceId, CapaId, RelativePath, FileName, SizeBytes, SortNo, UploadedBy, UploadedAt)
                    VALUES(@eid,@cid,@p,@fn,@sz,@sn,@u,GETDATE())
                ",
                Db.P("@eid", eid), Db.P("@cid", capaId), Db.P("@p", sf.RelativePath), Db.P("@fn", sf.FileName), Db.P("@sz", sf.SizeBytes), Db.P("@sn", cnt), Db.P("@u", Auth.CurrentUserId));

                saved.Add(new { evidenceId = eid, url = "/api/files/" + eid.ToString("N"), sortNo = cnt });
            }

            return Ok(new { items = saved });
        }

        [HttpPost]
        [Route("{capaId:guid}/submit-evidence")]
        public IHttpActionResult SubmitEvidence(Guid capaId)
        {
            // will fail by CHECK constraint if required fields missing
            Db.Execute("UPDATE CapaItem SET Status=2 WHERE CapaId=@id", Db.P("@id", capaId));
            return Ok(new { ok = true });
        }

        [HttpPost]
        [Route("{capaId:guid}/close")]
        public IHttpActionResult Close(Guid capaId, CloseCapaRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ReviewConclusion))
                return BadRequest("ReviewConclusion required");

            Db.Execute(@"
                UPDATE CapaItem
                SET Status=3,
                    ReviewConclusion=@rc,
                    ClosedByUserId=@u,
                    ClosedAt=GETDATE()
                WHERE CapaId=@id
            ", Db.P("@rc", req.ReviewConclusion), Db.P("@u", Auth.CurrentUserId), Db.P("@id", capaId));

            return Ok(new { ok = true });
        }

        private static string StatusText(int status)
        {
            switch (status)
            {
                case 1: return "待整改";
                case 2: return "已提交证据";
                case 3: return "已复核关闭";
                default: return "未知";
            }
        }
    }
}
