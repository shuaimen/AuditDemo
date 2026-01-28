using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using AuditDemo.WebApi.Infrastructure;
using AuditDemo.WebApi.Models;

namespace AuditDemo.WebApi.Controllers
{
    [RoutePrefix("api/audits")]
    public class AuditsController : ApiController
    {
        [HttpGet]
        [Route("")]
        public IHttpActionResult List(int? year = null, Guid? factoryId = null)
        {
            var sql = @"
                SELECT TOP 200 a.AuditId, a.AuditType, a.[Year], a.Status, a.FinalGrade, a.CreatedAt,
                       f.FactoryCode, f.Name AS FactoryName, f.FactoryType
                FROM Audit a WITH (NOLOCK)
                JOIN Factory f WITH (NOLOCK) ON a.FactoryId=f.FactoryId
                WHERE 1=1";

            var ps = new List<System.Data.SqlClient.SqlParameter>();
            if (year.HasValue)
            {
                sql += " AND a.[Year]=@y";
                ps.Add(Db.P("@y", year.Value));
            }
            if (factoryId.HasValue)
            {
                sql += " AND a.FactoryId=@fid";
                ps.Add(Db.P("@fid", factoryId.Value));
            }
            sql += " ORDER BY a.CreatedAt DESC";

            var dt = Db.Query(sql, ps.ToArray());
            return Ok(new
            {
                items = dt.AsEnumerable().Select(r => new
                {
                    auditId = (Guid)r["AuditId"],
                    auditType = Convert.ToInt32(r["AuditType"]),
                    year = Convert.ToInt32(r["Year"]),
                    status = Convert.ToInt32(r["Status"]),
                    statusText = StatusText(Convert.ToInt32(r["Status"])),
                    finalGrade = Convert.ToString(r["FinalGrade"]),
                    createdAt = Convert.ToDateTime(r["CreatedAt"]),
                    factoryCode = Convert.ToString(r["FactoryCode"]),
                    factoryName = Convert.ToString(r["FactoryName"]),
                    factoryType = Convert.ToInt32(r["FactoryType"])
                }).ToList()
            });
        }

        [HttpPost]
        [Route("")]
        public IHttpActionResult Create(CreateAuditRequest req)
        {
            if (req == null) return BadRequest("Missing body");
            if (req.Year <= 0) return BadRequest("Year required");
            if (req.FactoryId == Guid.Empty) return BadRequest("FactoryId required");
            if (req.AuditType != 1 && req.AuditType != 2) return BadRequest("AuditType must be 1 or 2");

            var createdBy = Auth.CurrentUserId;
            try
            {
                var dt = Db.Query(
                    "EXEC dbo.sp_CreateAudit @AuditType,@Year,@FactoryId,@CreatedBy,@CopyLastYear,@AuditStatus",
                    Db.P("@AuditType", req.AuditType),
                    Db.P("@Year", req.Year),
                    Db.P("@FactoryId", req.FactoryId),
                    Db.P("@CreatedBy", createdBy),
                    Db.P("@CopyLastYear", req.CopyLastYear ? 1 : 0),
                    Db.P("@AuditStatus", 2)
                );

                var row = dt.Rows.Count > 0 ? dt.Rows[0] : null;
                if (row == null) return InternalServerError(new Exception("Create audit failed"));

                return Ok(new
                {
                    auditId = (Guid)row["AuditId"],
                    templateVersionId = (Guid)row["TemplateVersionId"],
                    copiedFromAuditId = row["CopiedFromAuditId"] == DBNull.Value ? (Guid?)null : (Guid)row["CopiedFromAuditId"]
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("{auditId:guid}")]
        public IHttpActionResult Detail(Guid auditId)
        {
            var a = Db.QuerySingle(@"
                SELECT a.AuditId, a.AuditType, a.[Year], a.Status, a.FinalGrade, a.CreatedAt, a.RatedAt, a.CopiedFromAuditId,
                       f.FactoryId, f.FactoryCode, f.Name AS FactoryName, f.FactoryType
                FROM Audit a WITH (NOLOCK)
                JOIN Factory f WITH (NOLOCK) ON a.FactoryId=f.FactoryId
                WHERE a.AuditId=@id
            ", Db.P("@id", auditId));

            if (a == null) return NotFound();

            var modules = Db.Query(@"
                SELECT am.ModuleId, tm.ModuleName, tm.SortNo, am.OwnerUserId, am.ModuleStatus, am.SubmittedAt
                FROM AuditModuleAssignment am WITH (NOLOCK)
                JOIN TemplateModule tm WITH (NOLOCK) ON am.ModuleId=tm.ModuleId
                WHERE am.AuditId=@id
                ORDER BY tm.SortNo
            ", Db.P("@id", auditId));

            // progress: filled/total per module
            var prog = Db.Query(@"
                SELECT tm.ModuleId,
                       SUM(CASE WHEN r.Status<>0 THEN 1 ELSE 0 END) AS Filled,
                       COUNT(1) AS Total
                FROM AuditClauseResult r
                JOIN TemplateClause c ON r.ClauseId=c.ClauseId
                JOIN TemplateModule tm ON c.ModuleId=tm.ModuleId
                WHERE r.AuditId=@id
                GROUP BY tm.ModuleId
            ", Db.P("@id", auditId));

            var progMap = prog.AsEnumerable().ToDictionary(
                r => (Guid)r["ModuleId"],
                r => new { filled = Convert.ToInt32(r["Filled"]), total = Convert.ToInt32(r["Total"]) }
            );

            return Ok(new
            {
                audit = new
                {
                    auditId = (Guid)a["AuditId"],
                    auditType = Convert.ToInt32(a["AuditType"]),
                    year = Convert.ToInt32(a["Year"]),
                    status = Convert.ToInt32(a["Status"]),
                    statusText = StatusText(Convert.ToInt32(a["Status"])),
                    finalGrade = Convert.ToString(a["FinalGrade"]),
                    createdAt = Convert.ToDateTime(a["CreatedAt"]),
                    ratedAt = a["RatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(a["RatedAt"]),
                    copiedFromAuditId = a["CopiedFromAuditId"] == DBNull.Value ? (Guid?)null : (Guid)a["CopiedFromAuditId"],
                    factory = new
                    {
                        factoryId = (Guid)a["FactoryId"],
                        factoryCode = Convert.ToString(a["FactoryCode"]),
                        factoryName = Convert.ToString(a["FactoryName"]),
                        factoryType = Convert.ToInt32(a["FactoryType"])
                    }
                },
                modules = modules.AsEnumerable().Select(r =>
                {
                    var mid = (Guid)r["ModuleId"];
                    var p = progMap.ContainsKey(mid) ? progMap[mid] : new { filled = 0, total = 0 };
                    return new
                    {
                        moduleId = mid,
                        moduleName = Convert.ToString(r["ModuleName"]),
                        sortNo = Convert.ToInt32(r["SortNo"]),
                        ownerUserId = r["OwnerUserId"] == DBNull.Value ? (Guid?)null : (Guid)r["OwnerUserId"],
                        moduleStatus = Convert.ToInt32(r["ModuleStatus"]),
                        submittedAt = r["SubmittedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["SubmittedAt"]),
                        filled = p.filled,
                        total = p.total
                    };
                }).ToList()
            });
        }

        [HttpPut]
        [Route("{auditId:guid}/assign-modules")]
        public IHttpActionResult AssignModules(Guid auditId, AssignModulesRequest req)
        {
            if (req == null || req.Assignments == null) return BadRequest("Missing body");
            // Demo：不做更细粒度校验（实际可限制：创建者/管理员才允许分配）

            foreach (var a in req.Assignments)
            {
                Db.Execute(@"
                    UPDATE AuditModuleAssignment
                    SET OwnerUserId=@u
                    WHERE AuditId=@aid AND ModuleId=@mid
                ", Db.P("@u", (object)a.OwnerUserId ?? DBNull.Value), Db.P("@aid", auditId), Db.P("@mid", a.ModuleId));
            }
            return Ok(new { ok = true });
        }

        [HttpPost]
        [Route("{auditId:guid}/reopen")]
        public IHttpActionResult Reopen(Guid auditId)
        {
            if (!Auth.IsAdmin) return Content(HttpStatusCode.Forbidden, new { message = "Admin only" });
            try
            {
                Db.Execute("EXEC dbo.sp_ReopenAudit @AuditId", Db.P("@AuditId", auditId));
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("{auditId:guid}/modules/{moduleId:guid}/lock")]
        public IHttpActionResult Lock(Guid auditId, Guid moduleId)
        {
            var dt = Db.Query("EXEC dbo.sp_TryLockAuditModule @AuditId,@ModuleId,@UserId,@Minutes",
                Db.P("@AuditId", auditId), Db.P("@ModuleId", moduleId), Db.P("@UserId", Auth.CurrentUserId), Db.P("@Minutes", 15));
            var row = dt.Rows[0];
            return Ok(new
            {
                locked = Convert.ToInt32(row["Locked"]) == 1,
                lockedByUserId = (Guid)row["LockedByUserId"],
                expiresAt = Convert.ToDateTime(row["ExpiresAt"]),
                lockToken = Convert.ToString(row["LockToken"])
            });
        }

        [HttpPost]
        [Route("{auditId:guid}/modules/{moduleId:guid}/heartbeat")]
        public IHttpActionResult Heartbeat(Guid auditId, Guid moduleId, [FromBody] dynamic body)
        {
            var token = body?.lockToken != null ? (string)body.lockToken : null;
            Guid t;
            if (!Guid.TryParse(token, out t) ) return BadRequest("lockToken required");
            var dt = Db.Query("EXEC dbo.sp_HeartbeatAuditModuleLock @AuditId,@ModuleId,@LockToken,@Minutes",
                Db.P("@AuditId", auditId), Db.P("@ModuleId", moduleId), Db.P("@LockToken", t), Db.P("@Minutes", 15));
            return Ok(new { affected = Convert.ToInt32(dt.Rows[0]["Affected"]) });
        }

        [HttpPost]
        [Route("{auditId:guid}/modules/{moduleId:guid}/unlock")]
        public IHttpActionResult Unlock(Guid auditId, Guid moduleId, [FromBody] dynamic body)
        {
            var token = body?.lockToken != null ? (string)body.lockToken : null;
            Guid t;
            if (!Guid.TryParse(token, out t)) return BadRequest("lockToken required");
            var dt = Db.Query("EXEC dbo.sp_UnlockAuditModule @AuditId,@ModuleId,@LockToken",
                Db.P("@AuditId", auditId), Db.P("@ModuleId", moduleId), Db.P("@LockToken", t));
            return Ok(new { affected = Convert.ToInt32(dt.Rows[0]["Affected"]) });
        }

        [HttpGet]
        [Route("{auditId:guid}/modules/{moduleId:guid}")]
        public IHttpActionResult GetModuleClauses(Guid auditId, Guid moduleId)
        {
            // current clauses + last year status/comment
            var dt = Db.Query(@"
                SELECT
                  c.ClauseId, c.ClauseCode, c.ClauseLevel, c.Content, c.SortNo,
                  r.Status, r.Comment, r.LastYearStatus, r.LastYearComment,
                  r.ResultId
                FROM TemplateClause c
                JOIN AuditClauseResult r ON r.ClauseId=c.ClauseId AND r.AuditId=@aid
                WHERE c.ModuleId=@mid AND c.IsActive=1
                ORDER BY c.SortNo
            ", Db.P("@aid", auditId), Db.P("@mid", moduleId));

            // current photos
            var photos = Db.Query(@"
                SELECT p.PhotoId, p.ResultId, p.SortNo
                FROM AuditClausePhoto p
                JOIN AuditClauseResult r ON p.ResultId=r.ResultId
                WHERE r.AuditId=@aid
                ORDER BY p.SortNo
            ", Db.P("@aid", auditId));

            var photoMap = photos.AsEnumerable()
                .GroupBy(r => (Guid)r["ResultId"])
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (object)new
                    {
                        photoId = (Guid)x["PhotoId"],
                        sortNo = Convert.ToInt32(x["SortNo"]),
                        url = "/api/files/" + ((Guid)x["PhotoId"]).ToString("N")
                    }).ToList()
                ); // Dictionary<Guid, List<object>>


            // last year photos (reference only)
            Guid? copiedFrom = null;
            var cf = Db.QuerySingle("SELECT CopiedFromAuditId FROM Audit WITH (NOLOCK) WHERE AuditId=@id", Db.P("@id", auditId));
            if (cf != null && cf["CopiedFromAuditId"] != DBNull.Value) copiedFrom = (Guid)cf["CopiedFromAuditId"];

            var lastYearPhotoMap = new Dictionary<Guid, List<object>>(); // key: ClauseId
            if (copiedFrom.HasValue)
            {
                var prev = Db.Query(@"
                    SELECT cur.ClauseId, pp.PhotoId, pp.SortNo
                    FROM AuditClauseResult cur
                    JOIN TemplateClause c ON cur.ClauseId=c.ClauseId
                    JOIN AuditClauseResult prev ON prev.AuditId=@prevAid AND prev.ClauseCode=cur.ClauseCode
                    JOIN AuditClausePhoto pp ON pp.ResultId=prev.ResultId
                    WHERE cur.AuditId=@aid AND c.ModuleId=@mid
                    ORDER BY c.SortNo, pp.SortNo
                ", Db.P("@prevAid", copiedFrom.Value), Db.P("@aid", auditId), Db.P("@mid", moduleId));

                lastYearPhotoMap = prev.AsEnumerable().GroupBy(r => (Guid)r["ClauseId"]).ToDictionary(
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
                items = dt.AsEnumerable().Select(r =>
                {
                    var resultId = (Guid)r["ResultId"];
                    var clauseId = (Guid)r["ClauseId"];
                    return new
                    {
                        clauseId = clauseId,
                        clauseCode = Convert.ToString(r["ClauseCode"]),
                        clauseLevel = Convert.ToString(r["ClauseLevel"]),
                        content = Convert.ToString(r["Content"]),
                        sortNo = Convert.ToInt32(r["SortNo"]),
                        status = Convert.ToInt32(r["Status"]),
                        comment = Convert.ToString(r["Comment"]),
                        lastYearStatus = r["LastYearStatus"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["LastYearStatus"]),
                        lastYearComment = Convert.ToString(r["LastYearComment"]),
                        photos = photoMap.ContainsKey(resultId) ? photoMap[resultId] : new List<object>(),
                        lastYearPhotos = lastYearPhotoMap.ContainsKey(clauseId) ? lastYearPhotoMap[clauseId] : new List<object>()
                    };
                }).ToList()
            });
        }

        [HttpPut]
        [Route("{auditId:guid}/clauses/{clauseId:guid}")]
        public IHttpActionResult SaveClause(Guid auditId, Guid clauseId, SaveClauseRequest req)
        {
            if (req == null) return BadRequest("Missing body");
            if (req.Status < 0 || req.Status > 4) return BadRequest("Invalid status");

            Db.Execute(@"
                UPDATE AuditClauseResult
                SET Status=@s, Comment=@c, UpdatedAt=GETDATE()
                WHERE AuditId=@aid AND ClauseId=@cid
            ", Db.P("@s", req.Status), Db.P("@c", (object)req.Comment ?? DBNull.Value), Db.P("@aid", auditId), Db.P("@cid", clauseId));

            return Ok(new { ok = true });
        }

        [HttpPost]
        [Route("{auditId:guid}/clauses/{clauseId:guid}/photos")]
        public IHttpActionResult UploadClausePhotos(Guid auditId, Guid clauseId)
        {
            var ctx = HttpContext.Current;
            if (ctx == null) return BadRequest("No HttpContext");
            if (ctx.Request.Files.Count <= 0) return BadRequest("No files");

            var r = Db.QuerySingle(@"SELECT ResultId, ClauseCode FROM AuditClauseResult WITH (NOLOCK) WHERE AuditId=@aid AND ClauseId=@cid",
                Db.P("@aid", auditId), Db.P("@cid", clauseId));
            if (r == null) return NotFound();

            var resultId = (Guid)r["ResultId"];
            var clauseCode = Convert.ToString(r["ClauseCode"]);

            var existing = Db.QuerySingle(@"SELECT COUNT(1) AS Cnt FROM AuditClausePhoto WITH (NOLOCK) WHERE ResultId=@rid",
                Db.P("@rid", resultId));
            var cnt = Convert.ToInt32(existing["Cnt"]);
            if (cnt >= 3) return BadRequest("Max 3 photos per clause");

            var saved = new List<object>();
            for (int i = 0; i < ctx.Request.Files.Count; i++)
            {
                if (cnt >= 3) break;
                var f = ctx.Request.Files[i];
                var sf = FileStorage.SavePhoto(f, $"Audit/{auditId:N}/{clauseCode}");
                cnt++;

                var photoId = Guid.NewGuid();
                Db.Execute(@"
                    INSERT AuditClausePhoto(PhotoId, ResultId, RelativePath, FileName, SizeBytes, SortNo, UploadedBy, UploadedAt)
                    VALUES(@pid,@rid,@p,@fn,@sz,@sn,@u,GETDATE())
                ",
                Db.P("@pid", photoId), Db.P("@rid", resultId), Db.P("@p", sf.RelativePath), Db.P("@fn", sf.FileName), Db.P("@sz", sf.SizeBytes), Db.P("@sn", cnt), Db.P("@u", Auth.CurrentUserId));

                saved.Add(new { photoId = photoId, url = "/api/files/" + photoId.ToString("N"), sortNo = cnt });
            }

            return Ok(new { items = saved });
        }

        [HttpPost]
        [Route("{auditId:guid}/modules/{moduleId:guid}/submit")]
        public IHttpActionResult SubmitModule(Guid auditId, Guid moduleId)
        {
            try
            {
                Db.Query("EXEC dbo.sp_SubmitAuditModule @AuditId,@ModuleId", Db.P("@AuditId", auditId), Db.P("@ModuleId", moduleId));
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("{auditId:guid}/modules/{moduleId:guid}/withdraw")]
        public IHttpActionResult WithdrawModule(Guid auditId, Guid moduleId)
        {
            try
            {
                Db.Query("EXEC dbo.sp_WithdrawAuditModule @AuditId,@ModuleId", Db.P("@AuditId", auditId), Db.P("@ModuleId", moduleId));
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [Route("{auditId:guid}/rate")]
        public IHttpActionResult Rate(Guid auditId)
        {
            try
            {
                var dt = Db.Query("EXEC dbo.sp_RateAudit_And_GenCapa @AuditId", Db.P("@AuditId", auditId));
                var row = dt.Rows[0];
                return Ok(new { finalGrade = Convert.ToString(row["FinalGrade"]), hasCapa = Convert.ToInt32(row["HasCapa"]) == 1 });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }



        [HttpGet]
        [Route("{auditId:guid}/export/detail")] 
        public HttpResponseMessage ExportDetail(Guid auditId)
        {
            var bytes = ExcelExporter.ExportAuditDetail(auditId);
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Content = new ByteArrayContent(bytes);
            resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            resp.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = "audit_detail.xlsx"
            };
            return resp;
        }

        private static string StatusText(int status)
        {
            switch (status)
            {
                case 1: return "草稿";
                case 2: return "进行中";
                case 3: return "已判级";
                case 4: return "整改中";
                case 5: return "已关闭";
                default: return "未知";
            }
        }
    }
}
