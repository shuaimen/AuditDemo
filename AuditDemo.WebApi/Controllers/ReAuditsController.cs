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
    [RoutePrefix("api/reaudits")]
    public class ReAuditsController : ApiController
    {
        [HttpGet]
        [Route("by-audit/{fromAuditId:guid}")]
        public IHttpActionResult ListByAudit(Guid fromAuditId)
        {
            var dt = Db.Query(@"
                SELECT ReAuditId, FromAuditId, [Year], Status, IsPassed, CreatedAt, SubmittedAt, ClosedAt
                FROM ReAudit WITH (NOLOCK)
                WHERE FromAuditId=@id
                ORDER BY CreatedAt DESC
            ", Db.P("@id", fromAuditId));

            return Ok(new
            {
                items = dt.AsEnumerable().Select(r => new
                {
                    reAuditId = (Guid)r["ReAuditId"],
                    fromAuditId = (Guid)r["FromAuditId"],
                    year = Convert.ToInt32(r["Year"]),
                    status = Convert.ToInt32(r["Status"]),
                    statusText = StatusText(Convert.ToInt32(r["Status"])),
                    isPassed = r["IsPassed"] == DBNull.Value ? (bool?)null : (bool)r["IsPassed"],
                    createdAt = Convert.ToDateTime(r["CreatedAt"]),
                    submittedAt = r["SubmittedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["SubmittedAt"]),
                    closedAt = r["ClosedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ClosedAt"])
                }).ToList()
            });
        }

        [HttpPost]
        [Route("")]
        public IHttpActionResult Create(CreateReAuditRequest req)
        {
            if (req == null || req.FromAuditId == Guid.Empty) return BadRequest("FromAuditId required");

            try
            {
                var dt = Db.Query("EXEC dbo.sp_CreateReAudit_FromAudit @FromAuditId,@CreatedBy",
                    Db.P("@FromAuditId", req.FromAuditId),
                    Db.P("@CreatedBy", Auth.CurrentUserId));
                return Ok(new { reAuditId = (Guid)dt.Rows[0]["ReAuditId"] });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("{reAuditId:guid}")]
        public IHttpActionResult Detail(Guid reAuditId)
        {
            var ra = Db.QuerySingle(@"
                SELECT r.ReAuditId, r.FromAuditId, r.FactoryId, r.[Year], r.Status, r.IsPassed, r.CreatedAt, r.SubmittedAt, r.ClosedAt, r.CloseConclusion,
                       f.FactoryCode, f.Name AS FactoryName
                FROM ReAudit r WITH (NOLOCK)
                JOIN Factory f WITH (NOLOCK) ON r.FactoryId=f.FactoryId
                WHERE r.ReAuditId=@id
            ", Db.P("@id", reAuditId));
            if (ra == null) return NotFound();

            var items = Db.Query(@"
                SELECT ReResultId, ClauseCode, ClauseLevel, PrevStatus, PrevComment, PrevResultId, Status, Comment
                FROM ReAuditClauseResult WITH (NOLOCK)
                WHERE ReAuditId=@id
                ORDER BY ClauseLevel DESC, ClauseCode
            ", Db.P("@id", reAuditId));

            // current photos
            var ph = Db.Query(@"
                SELECT p.PhotoId, p.ReResultId, p.SortNo
                FROM ReAuditClausePhoto p WITH (NOLOCK)
                JOIN ReAuditClauseResult r WITH (NOLOCK) ON p.ReResultId=r.ReResultId
                WHERE r.ReAuditId=@id
                ORDER BY p.SortNo
            ", Db.P("@id", reAuditId));

            var curPhotoMap = ph.AsEnumerable()
                .GroupBy(r => (Guid)r["ReResultId"])
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (object)new
                    {
                        photoId = (Guid)x["PhotoId"],
                        sortNo = Convert.ToInt32(x["SortNo"]),
                        url = "/api/files/" + ((Guid)x["PhotoId"]).ToString("N")
                    }).ToList()
                );


            // prev photos (reference): by PrevResultId
            var prevIds = items.AsEnumerable().Where(r => r["PrevResultId"] != DBNull.Value).Select(r => (Guid)r["PrevResultId"]).Distinct().ToList();
            var prevPhotoMap = new Dictionary<Guid, List<object>>();
            if (prevIds.Count > 0)
            {
                // SQL Server 2014: IN list via temp table pattern - for demo we do OR chain
                var where = string.Join(" OR ", prevIds.Select((x, i) => "p.ResultId=@p" + i));
                var ps = new List<System.Data.SqlClient.SqlParameter>();
                for (int i = 0; i < prevIds.Count; i++) ps.Add(Db.P("@p" + i, prevIds[i]));

                var prevPhotos = Db.Query("SELECT p.PhotoId, p.ResultId, p.SortNo FROM AuditClausePhoto p WITH (NOLOCK) WHERE " + where + " ORDER BY p.SortNo", ps.ToArray());
                prevPhotoMap = prevPhotos.AsEnumerable().GroupBy(r => (Guid)r["ResultId"]).ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (object)new {
                        photoId = (Guid)x["PhotoId"],
                        sortNo = Convert.ToInt32(x["SortNo"]),
                        url = "/api/files/" + ((Guid)x["PhotoId"]).ToString("N")
                    }).ToList()
                );
            }

            return Ok(new
            {
                reAudit = new
                {
                    reAuditId = (Guid)ra["ReAuditId"],
                    fromAuditId = (Guid)ra["FromAuditId"],
                    year = Convert.ToInt32(ra["Year"]),
                    status = Convert.ToInt32(ra["Status"]),
                    statusText = StatusText(Convert.ToInt32(ra["Status"])),
                    isPassed = ra["IsPassed"] == DBNull.Value ? (bool?)null : (bool)ra["IsPassed"],
                    closeConclusion = Convert.ToString(ra["CloseConclusion"]),
                    createdAt = Convert.ToDateTime(ra["CreatedAt"]),
                    submittedAt = ra["SubmittedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(ra["SubmittedAt"]),
                    closedAt = ra["ClosedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(ra["ClosedAt"]),
                    factory = new {
                        factoryId = (Guid)ra["FactoryId"],
                        factoryCode = Convert.ToString(ra["FactoryCode"]),
                        factoryName = Convert.ToString(ra["FactoryName"])
                    }
                },
                items = items.AsEnumerable().Select(r =>
                {
                    var reResultId = (Guid)r["ReResultId"];
                    var prevResultId = r["PrevResultId"] == DBNull.Value ? (Guid?)null : (Guid)r["PrevResultId"];
                    return new
                    {
                        reResultId = reResultId,
                        clauseCode = Convert.ToString(r["ClauseCode"]),
                        clauseLevel = Convert.ToString(r["ClauseLevel"]),
                        prevStatus = r["PrevStatus"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["PrevStatus"]),
                        prevComment = Convert.ToString(r["PrevComment"]),
                        status = Convert.ToInt32(r["Status"]),
                        comment = Convert.ToString(r["Comment"]),
                        photos = curPhotoMap.ContainsKey(reResultId) ? curPhotoMap[reResultId] : new List<object>(),
                        prevPhotos = (prevResultId.HasValue && prevPhotoMap.ContainsKey(prevResultId.Value)) ? prevPhotoMap[prevResultId.Value] : new List<object>()
                    };
                }).ToList()
            });
        }

        [HttpPut]
        [Route("{reAuditId:guid}/clauses/{clauseCode}")]
        public IHttpActionResult SaveClause(Guid reAuditId, string clauseCode, SaveReAuditClauseRequest req)
        {
            if (req == null) return BadRequest("Missing body");
            if (string.IsNullOrWhiteSpace(clauseCode)) return BadRequest("clauseCode required");
            if (req.Status < 0 || req.Status > 4) return BadRequest("Invalid status");

            Db.Execute(@"
                UPDATE ReAuditClauseResult
                SET Status=@s, Comment=@c, UpdatedAt=GETDATE()
                WHERE ReAuditId=@rid AND ClauseCode=@cc
            ", Db.P("@s", req.Status), Db.P("@c", (object)req.Comment ?? DBNull.Value), Db.P("@rid", reAuditId), Db.P("@cc", clauseCode));

            return Ok(new { ok = true });
        }

        [HttpPost]
        [Route("{reAuditId:guid}/clauses/{clauseCode}/photos")]
        public IHttpActionResult UploadPhotos(Guid reAuditId, string clauseCode)
        {
            var ctx = HttpContext.Current;
            if (ctx == null) return BadRequest("No HttpContext");
            if (ctx.Request.Files.Count <= 0) return BadRequest("No files");

            var row = Db.QuerySingle(@"
                SELECT r.ReResultId, ra.[Year], ra.FactoryId
                FROM ReAuditClauseResult r WITH (NOLOCK)
                JOIN ReAudit ra WITH (NOLOCK) ON r.ReAuditId=ra.ReAuditId
                WHERE r.ReAuditId=@id AND r.ClauseCode=@cc
            ", Db.P("@id", reAuditId), Db.P("@cc", clauseCode));
            if (row == null) return NotFound();

            var reResultId = (Guid)row["ReResultId"];
            var existing = Db.QuerySingle("SELECT COUNT(1) AS Cnt FROM ReAuditClausePhoto WITH (NOLOCK) WHERE ReResultId=@id", Db.P("@id", reResultId));
            var cnt = Convert.ToInt32(existing["Cnt"]);
            if (cnt >= 3) return BadRequest("Max 3 photos");

            var saved = new List<object>();
            for (int i = 0; i < ctx.Request.Files.Count; i++)
            {
                if (cnt >= 3) break;
                var f = ctx.Request.Files[i];
                var sf = FileStorage.SavePhoto(f, $"ReAudit/{reAuditId:N}/{clauseCode}");
                cnt++;

                var pid = Guid.NewGuid();
                Db.Execute(@"
                    INSERT ReAuditClausePhoto(PhotoId, ReResultId, RelativePath, FileName, SizeBytes, SortNo, UploadedBy, UploadedAt)
                    VALUES(@pid,@rid,@p,@fn,@sz,@sn,@u,GETDATE())
                ",
                Db.P("@pid", pid), Db.P("@rid", reResultId), Db.P("@p", sf.RelativePath), Db.P("@fn", sf.FileName), Db.P("@sz", sf.SizeBytes), Db.P("@sn", cnt), Db.P("@u", Auth.CurrentUserId));

                saved.Add(new { photoId = pid, url = "/api/files/" + pid.ToString("N"), sortNo = cnt });
            }

            return Ok(new { items = saved });
        }

        [HttpPost]
        [Route("{reAuditId:guid}/submit")]
        public IHttpActionResult Submit(Guid reAuditId)
        {
            try
            {
                var dt = Db.Query("EXEC dbo.sp_SubmitReAudit @ReAuditId", Db.P("@ReAuditId", reAuditId));
                var row = dt.Rows[0];
                return Ok(new { isPassed = Convert.ToInt32(row["IsPassed"]) == 1 });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("{reAuditId:guid}/close")]
        public IHttpActionResult Close(Guid reAuditId, CloseReAuditRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.CloseConclusion)) return BadRequest("CloseConclusion required");
            try
            {
                Db.Execute("EXEC dbo.sp_CloseReAudit @ReAuditId,@CloseConclusion",
                    Db.P("@ReAuditId", reAuditId),
                    Db.P("@CloseConclusion", req.CloseConclusion));
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private static string StatusText(int status)
        {
            switch (status)
            {
                case 1: return "进行中";
                case 2: return "已提交";
                case 3: return "已关闭";
                default: return "未知";
            }
        }
    }
}
