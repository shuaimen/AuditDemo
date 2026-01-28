/*
  OEMAuditDemo - SQL Server 2014 compatible schema + procs (v2)

  状态枚举：
    Audit.Status: 1 草稿, 2 进行中, 3 已判级, 4 整改中, 5 已关闭
    AuditModuleAssignment.ModuleStatus: 1 编辑中, 2 已提交
    AuditClauseResult.Status: 0 未填写, 1 符合, 2 部分不符合, 3 不符合, 4 不适用
    CapaItem.Status: 1 待整改, 2 已提交证据, 3 已复核关闭
    ReAudit.Status: 1 进行中, 2 已提交, 3 已关闭
    User.Role: 1 Admin, 2 Auditor

  判级规则（Demo）：
    - 任何 E 条款出现(部分不符合/不符合) => FinalGrade='E'
    - 否则 any D => 'D'
    - 否则 any C => 'C'
    - 否则 any B => 'B'
    - 否则 => 'A'

  业务规则要点（已落地到 Demo）：
    - “不适用”视为无影响，不生成整改项
    - A 条款不参与判级，但出现(部分不符合/不符合)仍生成整改项
    - 任意出现(部分不符合/不符合)都生成整改项(CAPA)
    - 模块提交前必须本模块所有条款都已填写(不允许 Status=0)
    - 允许撤回（但已判级/整改中/已关闭不可撤回，除非管理员重开）
    - 管理员可重开（清理 CAPA/证据、清空判级、模块回到编辑中）
    - 年度评鉴复制去年：去年结果复制为今年默认值可编辑；去年照片仅作为链接/缩略图参考，不复制
    - 复评：仅复查不合格条款(部分不符合/不符合)，单独记录与提交/关闭
*/

SET NOCOUNT ON;

IF DB_ID('OEMAuditDemo') IS NULL
BEGIN
    PRINT 'NOTE: Please create database OEMAuditDemo first, then run this script inside it.';
END

-- =====================
-- Drop procs (SQL 2014: no CREATE OR ALTER)
-- =====================
IF OBJECT_ID('dbo.sp_CloseReAudit','P') IS NOT NULL DROP PROCEDURE dbo.sp_CloseReAudit;
IF OBJECT_ID('dbo.sp_SubmitReAudit','P') IS NOT NULL DROP PROCEDURE dbo.sp_SubmitReAudit;
IF OBJECT_ID('dbo.sp_CreateReAudit_FromAudit','P') IS NOT NULL DROP PROCEDURE dbo.sp_CreateReAudit_FromAudit;
IF OBJECT_ID('dbo.sp_ReopenAudit','P') IS NOT NULL DROP PROCEDURE dbo.sp_ReopenAudit;
IF OBJECT_ID('dbo.sp_RateAudit_And_GenCapa','P') IS NOT NULL DROP PROCEDURE dbo.sp_RateAudit_And_GenCapa;
IF OBJECT_ID('dbo.sp_WithdrawAuditModule','P') IS NOT NULL DROP PROCEDURE dbo.sp_WithdrawAuditModule;
IF OBJECT_ID('dbo.sp_SubmitAuditModule','P') IS NOT NULL DROP PROCEDURE dbo.sp_SubmitAuditModule;
IF OBJECT_ID('dbo.sp_UnlockAuditModule','P') IS NOT NULL DROP PROCEDURE dbo.sp_UnlockAuditModule;
IF OBJECT_ID('dbo.sp_HeartbeatAuditModuleLock','P') IS NOT NULL DROP PROCEDURE dbo.sp_HeartbeatAuditModuleLock;
IF OBJECT_ID('dbo.sp_TryLockAuditModule','P') IS NOT NULL DROP PROCEDURE dbo.sp_TryLockAuditModule;
IF OBJECT_ID('dbo.sp_CreateAudit','P') IS NOT NULL DROP PROCEDURE dbo.sp_CreateAudit;
GO

-- =====================
-- Drop tables (dependency order)
-- =====================
IF OBJECT_ID('dbo.ReAuditClausePhoto','U') IS NOT NULL DROP TABLE dbo.ReAuditClausePhoto;
IF OBJECT_ID('dbo.ReAuditClauseResult','U') IS NOT NULL DROP TABLE dbo.ReAuditClauseResult;
IF OBJECT_ID('dbo.ReAudit','U') IS NOT NULL DROP TABLE dbo.ReAudit;

IF OBJECT_ID('dbo.CapaEvidencePhoto','U') IS NOT NULL DROP TABLE dbo.CapaEvidencePhoto;
IF OBJECT_ID('dbo.CapaItem','U') IS NOT NULL DROP TABLE dbo.CapaItem;
IF OBJECT_ID('dbo.AuditClausePhoto','U') IS NOT NULL DROP TABLE dbo.AuditClausePhoto;
IF OBJECT_ID('dbo.AuditClauseResult','U') IS NOT NULL DROP TABLE dbo.AuditClauseResult;
IF OBJECT_ID('dbo.AuditModuleLock','U') IS NOT NULL DROP TABLE dbo.AuditModuleLock;
IF OBJECT_ID('dbo.AuditModuleAssignment','U') IS NOT NULL DROP TABLE dbo.AuditModuleAssignment;
IF OBJECT_ID('dbo.Audit','U') IS NOT NULL DROP TABLE dbo.Audit;
IF OBJECT_ID('dbo.TemplateClause','U') IS NOT NULL DROP TABLE dbo.TemplateClause;
IF OBJECT_ID('dbo.TemplateModule','U') IS NOT NULL DROP TABLE dbo.TemplateModule;
IF OBJECT_ID('dbo.TemplateVersion','U') IS NOT NULL DROP TABLE dbo.TemplateVersion;
IF OBJECT_ID('dbo.FactoryCertificateFile','U') IS NOT NULL DROP TABLE dbo.FactoryCertificateFile;
IF OBJECT_ID('dbo.FactoryCertificate','U') IS NOT NULL DROP TABLE dbo.FactoryCertificate;
IF OBJECT_ID('dbo.Factory','U') IS NOT NULL DROP TABLE dbo.Factory;
IF OBJECT_ID('dbo.AuthToken','U') IS NOT NULL DROP TABLE dbo.AuthToken;
IF OBJECT_ID('dbo.[User]','U') IS NOT NULL DROP TABLE dbo.[User];
GO

-- =====================
-- Core tables
-- =====================
CREATE TABLE dbo.[User](
    UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_User PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL CONSTRAINT UQ_User_Username UNIQUE,
    PasswordHash VARBINARY(64) NOT NULL,
    PasswordSalt VARBINARY(32) NOT NULL,
    Role INT NOT NULL, -- 1 admin, 2 auditor
    IsActive BIT NOT NULL CONSTRAINT DF_User_IsActive DEFAULT(1),
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_User_CreatedAt DEFAULT(GETDATE())
);

CREATE TABLE dbo.AuthToken(
    Token NVARCHAR(64) NOT NULL CONSTRAINT PK_AuthToken PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME NOT NULL,
    ExpiredAt DATETIME NOT NULL,
    CONSTRAINT FK_AuthToken_User FOREIGN KEY(UserId) REFERENCES dbo.[User](UserId)
);
CREATE INDEX IX_AuthToken_UserId ON dbo.AuthToken(UserId);
CREATE INDEX IX_AuthToken_ExpiredAt ON dbo.AuthToken(ExpiredAt);

CREATE TABLE dbo.Factory(
    FactoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Factory PRIMARY KEY,
    FactoryCode NVARCHAR(50) NOT NULL CONSTRAINT UQ_Factory_Code UNIQUE,
    Name NVARCHAR(200) NOT NULL,
    ShortName NVARCHAR(50) NULL,
    FactoryType INT NOT NULL, -- 1织造 2印染 3后加工 4其他/混合
    Address NVARCHAR(500) NULL,
    ContactName NVARCHAR(100) NULL,
    ContactPhone NVARCHAR(50) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Factory_IsActive DEFAULT(1),
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_Factory_CreatedAt DEFAULT(GETDATE())
);

CREATE TABLE dbo.FactoryCertificate(
    CertId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_FactoryCertificate PRIMARY KEY,
    FactoryId UNIQUEIDENTIFIER NOT NULL,
    CertName NVARCHAR(200) NOT NULL,
    CertNo NVARCHAR(100) NULL,
    CertType NVARCHAR(100) NULL,
    IssueDate DATE NULL,
    ExpireDate DATE NOT NULL,
    Remark NVARCHAR(500) NULL,
    RelativePath NVARCHAR(500) NULL, -- legacy main file
    IsActive BIT NOT NULL CONSTRAINT DF_FactoryCertificate_IsActive DEFAULT(1),
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_FactoryCertificate_CreatedAt DEFAULT(GETDATE()),
    UpdatedAt DATETIME NOT NULL CONSTRAINT DF_FactoryCertificate_UpdatedAt DEFAULT(GETDATE()),
    CONSTRAINT FK_FactoryCertificate_Factory FOREIGN KEY(FactoryId) REFERENCES dbo.Factory(FactoryId)
);
CREATE INDEX IX_FactoryCertificate_ExpireDate ON dbo.FactoryCertificate(ExpireDate);
CREATE INDEX IX_FactoryCertificate_Factory ON dbo.FactoryCertificate(FactoryId, IsActive, ExpireDate);

CREATE TABLE dbo.FactoryCertificateFile(
    FileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_FactoryCertificateFile PRIMARY KEY,
    CertId UNIQUEIDENTIFIER NOT NULL,
    RelativePath NVARCHAR(500) NOT NULL,
    FileName NVARCHAR(200) NOT NULL,
    SizeBytes BIGINT NOT NULL,
    SortNo INT NOT NULL, -- 1..5
    UploadedBy UNIQUEIDENTIFIER NOT NULL,
    UploadedAt DATETIME NOT NULL CONSTRAINT DF_FactoryCertificateFile_UploadedAt DEFAULT(GETDATE()),
    CONSTRAINT FK_FactoryCertificateFile_Cert FOREIGN KEY(CertId) REFERENCES dbo.FactoryCertificate(CertId),
    CONSTRAINT FK_FactoryCertificateFile_User FOREIGN KEY(UploadedBy) REFERENCES dbo.[User](UserId)
);
CREATE INDEX IX_FactoryCertificateFile_Cert ON dbo.FactoryCertificateFile(CertId, SortNo);

CREATE TABLE dbo.TemplateVersion(
    TemplateVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TemplateVersion PRIMARY KEY,
    VersionNo NVARCHAR(50) NOT NULL,
    FactoryType INT NOT NULL,
    IsPublished BIT NOT NULL CONSTRAINT DF_TemplateVersion_IsPublished DEFAULT(0),
    PublishedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_TemplateVersion_CreatedAt DEFAULT(GETDATE())
);
CREATE INDEX IX_TemplateVersion_Type_PublishedAt ON dbo.TemplateVersion(FactoryType, IsPublished, PublishedAt);

CREATE TABLE dbo.TemplateModule(
    ModuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TemplateModule PRIMARY KEY,
    TemplateVersionId UNIQUEIDENTIFIER NOT NULL,
    ModuleName NVARCHAR(200) NOT NULL,
    SortNo INT NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_TemplateModule_IsActive DEFAULT(1),
    CONSTRAINT FK_TemplateModule_Version FOREIGN KEY(TemplateVersionId) REFERENCES dbo.TemplateVersion(TemplateVersionId)
);
CREATE INDEX IX_TemplateModule_Version ON dbo.TemplateModule(TemplateVersionId, SortNo);

CREATE TABLE dbo.TemplateClause(
    ClauseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TemplateClause PRIMARY KEY,
    ModuleId UNIQUEIDENTIFIER NOT NULL,
    ClauseCode NVARCHAR(50) NOT NULL,
    ClauseLevel CHAR(1) NOT NULL, -- A/B/C/D/E
    Content NVARCHAR(2000) NOT NULL,
    SortNo INT NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_TemplateClause_IsActive DEFAULT(1),
    CONSTRAINT FK_TemplateClause_Module FOREIGN KEY(ModuleId) REFERENCES dbo.TemplateModule(ModuleId)
);
CREATE INDEX IX_TemplateClause_Module ON dbo.TemplateClause(ModuleId, SortNo);

CREATE TABLE dbo.Audit(
    AuditId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Audit PRIMARY KEY,
    AuditType INT NOT NULL, -- 1年度评鉴 2新引入(同流程)
    [Year] INT NOT NULL,
    FactoryId UNIQUEIDENTIFIER NOT NULL,
    TemplateVersionId UNIQUEIDENTIFIER NOT NULL,
    Status INT NOT NULL, -- 1..5
    FinalGrade CHAR(1) NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_Audit_CreatedAt DEFAULT(GETDATE()),
    RatedAt DATETIME NULL,
    CopiedFromAuditId UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Audit_Factory FOREIGN KEY(FactoryId) REFERENCES dbo.Factory(FactoryId),
    CONSTRAINT FK_Audit_TemplateVersion FOREIGN KEY(TemplateVersionId) REFERENCES dbo.TemplateVersion(TemplateVersionId)
);
CREATE INDEX IX_Audit_FactoryYear ON dbo.Audit(FactoryId, [Year]);

CREATE TABLE dbo.AuditModuleAssignment(
    AuditId UNIQUEIDENTIFIER NOT NULL,
    ModuleId UNIQUEIDENTIFIER NOT NULL,
    OwnerUserId UNIQUEIDENTIFIER NULL,
    ModuleStatus INT NOT NULL CONSTRAINT DF_AuditModule_Status DEFAULT(1),
    SubmittedAt DATETIME NULL,
    CONSTRAINT PK_AuditModuleAssignment PRIMARY KEY(AuditId, ModuleId),
    CONSTRAINT FK_AuditModule_Audit FOREIGN KEY(AuditId) REFERENCES dbo.Audit(AuditId),
    CONSTRAINT FK_AuditModule_Module FOREIGN KEY(ModuleId) REFERENCES dbo.TemplateModule(ModuleId)
);

CREATE TABLE dbo.AuditModuleLock(
    AuditId UNIQUEIDENTIFIER NOT NULL,
    ModuleId UNIQUEIDENTIFIER NOT NULL,
    LockToken UNIQUEIDENTIFIER NOT NULL,
    LockedByUserId UNIQUEIDENTIFIER NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    CONSTRAINT PK_AuditModuleLock PRIMARY KEY(AuditId, ModuleId),
    CONSTRAINT FK_AuditModuleLock_User FOREIGN KEY(LockedByUserId) REFERENCES dbo.[User](UserId)
);

CREATE TABLE dbo.AuditClauseResult(
    ResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditClauseResult PRIMARY KEY,
    AuditId UNIQUEIDENTIFIER NOT NULL,
    ClauseId UNIQUEIDENTIFIER NOT NULL,
    ClauseCode NVARCHAR(50) NOT NULL,
    ClauseLevel CHAR(1) NOT NULL,
    Status INT NOT NULL CONSTRAINT DF_AuditClauseResult_Status DEFAULT(0),
    Comment NVARCHAR(2000) NULL,
    LastYearStatus INT NULL,
    LastYearComment NVARCHAR(2000) NULL,
    UpdatedAt DATETIME NULL,
    CONSTRAINT UQ_AuditClauseResult UNIQUE(AuditId, ClauseId),
    CONSTRAINT FK_AuditClauseResult_Audit FOREIGN KEY(AuditId) REFERENCES dbo.Audit(AuditId),
    CONSTRAINT FK_AuditClauseResult_Clause FOREIGN KEY(ClauseId) REFERENCES dbo.TemplateClause(ClauseId)
);
CREATE INDEX IX_AuditClauseResult_Audit ON dbo.AuditClauseResult(AuditId);

CREATE TABLE dbo.AuditClausePhoto(
    PhotoId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditClausePhoto PRIMARY KEY,
    ResultId UNIQUEIDENTIFIER NOT NULL,
    RelativePath NVARCHAR(500) NOT NULL,
    FileName NVARCHAR(200) NOT NULL,
    SizeBytes BIGINT NOT NULL,
    SortNo INT NOT NULL,
    UploadedBy UNIQUEIDENTIFIER NOT NULL,
    UploadedAt DATETIME NOT NULL,
    CONSTRAINT FK_AuditClausePhoto_Result FOREIGN KEY(ResultId) REFERENCES dbo.AuditClauseResult(ResultId)
);
CREATE INDEX IX_AuditClausePhoto_Result ON dbo.AuditClausePhoto(ResultId, SortNo);

CREATE TABLE dbo.CapaItem(
    CapaId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CapaItem PRIMARY KEY,
    AuditId UNIQUEIDENTIFIER NOT NULL,
    ResultId UNIQUEIDENTIFIER NOT NULL,
    ClauseCode NVARCHAR(50) NOT NULL,
    ClauseLevel CHAR(1) NOT NULL,
    FindingStatus INT NOT NULL, -- 2/3
    CorrectiveAction NVARCHAR(2000) NULL,
    ExternalOwnerName NVARCHAR(100) NULL,
    ExternalOwnerPhone NVARCHAR(50) NULL,
    DueDate DATE NULL,
    Status INT NOT NULL CONSTRAINT DF_CapaItem_Status DEFAULT(1),
    ReviewConclusion NVARCHAR(2000) NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_CapaItem_CreatedAt DEFAULT(GETDATE()),
    ClosedByUserId UNIQUEIDENTIFIER NULL,
    ClosedAt DATETIME NULL,
    CONSTRAINT UQ_CapaItem_Result UNIQUE(ResultId),
    CONSTRAINT FK_CapaItem_Audit FOREIGN KEY(AuditId) REFERENCES dbo.Audit(AuditId),
    CONSTRAINT FK_CapaItem_Result FOREIGN KEY(ResultId) REFERENCES dbo.AuditClauseResult(ResultId)
);

-- Status>=2 requires action/name/phone/duedate
ALTER TABLE dbo.CapaItem ADD CONSTRAINT CK_CapaItem_RequiredFields
CHECK (
    Status < 2 OR
    (CorrectiveAction IS NOT NULL AND LTRIM(RTRIM(CorrectiveAction))<>''
     AND ExternalOwnerName IS NOT NULL AND LTRIM(RTRIM(ExternalOwnerName))<>''
     AND ExternalOwnerPhone IS NOT NULL AND LTRIM(RTRIM(ExternalOwnerPhone))<>''
     AND DueDate IS NOT NULL)
);

-- Status=3 requires review conclusion
ALTER TABLE dbo.CapaItem ADD CONSTRAINT CK_CapaItem_ReviewConclusion
CHECK (
    Status < 3 OR
    (ReviewConclusion IS NOT NULL AND LTRIM(RTRIM(ReviewConclusion))<>'')
);

CREATE TABLE dbo.CapaEvidencePhoto(
    EvidenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CapaEvidencePhoto PRIMARY KEY,
    CapaId UNIQUEIDENTIFIER NOT NULL,
    RelativePath NVARCHAR(500) NOT NULL,
    FileName NVARCHAR(200) NOT NULL,
    SizeBytes BIGINT NOT NULL,
    SortNo INT NOT NULL,
    UploadedBy UNIQUEIDENTIFIER NOT NULL,
    UploadedAt DATETIME NOT NULL,
    CONSTRAINT FK_CapaEvidence_Capa FOREIGN KEY(CapaId) REFERENCES dbo.CapaItem(CapaId)
);
CREATE INDEX IX_CapaEvidence_Capa ON dbo.CapaEvidencePhoto(CapaId, SortNo);

-- =====================
-- Re-audit (复评，仅不合格条款)
-- =====================
CREATE TABLE dbo.ReAudit(
    ReAuditId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ReAudit PRIMARY KEY,
    FromAuditId UNIQUEIDENTIFIER NOT NULL,
    FactoryId UNIQUEIDENTIFIER NOT NULL,
    [Year] INT NOT NULL,
    Status INT NOT NULL CONSTRAINT DF_ReAudit_Status DEFAULT(1),
    IsPassed BIT NULL,
    Conclusion NVARCHAR(2000) NULL,
    CreatedBy UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME NOT NULL CONSTRAINT DF_ReAudit_CreatedAt DEFAULT(GETDATE()),
    SubmittedAt DATETIME NULL,
    ClosedAt DATETIME NULL,
    CONSTRAINT FK_ReAudit_FromAudit FOREIGN KEY(FromAuditId) REFERENCES dbo.Audit(AuditId),
    CONSTRAINT FK_ReAudit_Factory FOREIGN KEY(FactoryId) REFERENCES dbo.Factory(FactoryId)
);
CREATE INDEX IX_ReAudit_FromAudit ON dbo.ReAudit(FromAuditId, CreatedAt);

CREATE TABLE dbo.ReAuditClauseResult(
    ReResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ReAuditClauseResult PRIMARY KEY,
    ReAuditId UNIQUEIDENTIFIER NOT NULL,
    ClauseCode NVARCHAR(50) NOT NULL,
    ClauseLevel CHAR(1) NOT NULL,
    PrevResultId UNIQUEIDENTIFIER NULL,
    PrevStatus INT NULL,
    PrevComment NVARCHAR(2000) NULL,
    Status INT NOT NULL CONSTRAINT DF_ReAuditClause_Status DEFAULT(0), -- 0..4
    Comment NVARCHAR(2000) NULL,
    UpdatedAt DATETIME NULL,
    CONSTRAINT UQ_ReAuditClause UNIQUE(ReAuditId, ClauseCode),
    CONSTRAINT FK_ReAuditClause_ReAudit FOREIGN KEY(ReAuditId) REFERENCES dbo.ReAudit(ReAuditId)
);
CREATE INDEX IX_ReAuditClause_ReAudit ON dbo.ReAuditClauseResult(ReAuditId);

CREATE TABLE dbo.ReAuditClausePhoto(
    PhotoId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ReAuditClausePhoto PRIMARY KEY,
    ReResultId UNIQUEIDENTIFIER NOT NULL,
    RelativePath NVARCHAR(500) NOT NULL,
    FileName NVARCHAR(200) NOT NULL,
    SizeBytes BIGINT NOT NULL,
    SortNo INT NOT NULL,
    UploadedBy UNIQUEIDENTIFIER NOT NULL,
    UploadedAt DATETIME NOT NULL,
    CONSTRAINT FK_ReAuditClausePhoto_Result FOREIGN KEY(ReResultId) REFERENCES dbo.ReAuditClauseResult(ReResultId)
);
CREATE INDEX IX_ReAuditClausePhoto_Result ON dbo.ReAuditClausePhoto(ReResultId, SortNo);
GO

-- =====================
-- Stored Procedures
-- =====================

CREATE PROCEDURE dbo.sp_CreateAudit
    @AuditType INT,
    @Year INT,
    @FactoryId UNIQUEIDENTIFIER,
    @CreatedBy UNIQUEIDENTIFIER,
    @CopyLastYear BIT,
    @AuditStatus INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @FactoryType INT;
    SELECT @FactoryType = FactoryType FROM dbo.Factory WITH (NOLOCK) WHERE FactoryId=@FactoryId;
    IF @FactoryType IS NULL
        RAISERROR('Factory not found', 16, 1);

    DECLARE @TemplateVersionId UNIQUEIDENTIFIER;
    SELECT TOP 1 @TemplateVersionId = TemplateVersionId
    FROM dbo.TemplateVersion WITH (NOLOCK)
    WHERE FactoryType=@FactoryType AND IsPublished=1
    ORDER BY PublishedAt DESC, CreatedAt DESC;

    IF @TemplateVersionId IS NULL
        RAISERROR('No published template for this factory type', 16, 1);

    DECLARE @AuditId UNIQUEIDENTIFIER = NEWID();
    DECLARE @CopiedFromAuditId UNIQUEIDENTIFIER = NULL;

    -- 年度评鉴才复制去年
    IF @CopyLastYear=1 AND @AuditType=1
    BEGIN
        SELECT TOP 1 @CopiedFromAuditId = a.AuditId
        FROM dbo.Audit a WITH (NOLOCK)
        WHERE a.FactoryId=@FactoryId AND a.[Year]=@Year-1 AND a.Status IN (3,4,5)
        ORDER BY ISNULL(a.RatedAt, a.CreatedAt) DESC;
    END

    BEGIN TRAN;

    INSERT dbo.Audit(AuditId, AuditType, [Year], FactoryId, TemplateVersionId, Status, CreatedBy, CreatedAt, CopiedFromAuditId)
    VALUES(@AuditId, @AuditType, @Year, @FactoryId, @TemplateVersionId, @AuditStatus, @CreatedBy, GETDATE(), @CopiedFromAuditId);

    INSERT dbo.AuditModuleAssignment(AuditId, ModuleId, OwnerUserId, ModuleStatus)
    SELECT @AuditId, m.ModuleId, NULL, 1
    FROM dbo.TemplateModule m WITH (NOLOCK)
    WHERE m.TemplateVersionId=@TemplateVersionId AND m.IsActive=1;

    INSERT dbo.AuditClauseResult(ResultId, AuditId, ClauseId, ClauseCode, ClauseLevel, Status, Comment, LastYearStatus, LastYearComment)
    SELECT NEWID(), @AuditId, c.ClauseId, c.ClauseCode, c.ClauseLevel,
           CASE WHEN @CopiedFromAuditId IS NOT NULL THEN ISNULL(prev.Status, 0) ELSE 0 END AS Status,
           CASE WHEN @CopiedFromAuditId IS NOT NULL THEN prev.Comment ELSE NULL END AS Comment,
           CASE WHEN @CopiedFromAuditId IS NOT NULL THEN prev.Status ELSE NULL END AS LastYearStatus,
           CASE WHEN @CopiedFromAuditId IS NOT NULL THEN prev.Comment ELSE NULL END AS LastYearComment
    FROM dbo.TemplateClause c WITH (NOLOCK)
    JOIN dbo.TemplateModule m WITH (NOLOCK) ON c.ModuleId=m.ModuleId
    LEFT JOIN dbo.AuditClauseResult prev WITH (NOLOCK)
           ON prev.AuditId=@CopiedFromAuditId AND prev.ClauseCode=c.ClauseCode
    WHERE m.TemplateVersionId=@TemplateVersionId AND m.IsActive=1 AND c.IsActive=1;

    COMMIT;

    SELECT @AuditId AS AuditId, @TemplateVersionId AS TemplateVersionId, @CopiedFromAuditId AS CopiedFromAuditId;
END
GO

CREATE PROCEDURE dbo.sp_TryLockAuditModule
    @AuditId UNIQUEIDENTIFIER,
    @ModuleId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Minutes INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @now DATETIME = GETDATE();

    IF EXISTS(
        SELECT 1 FROM dbo.AuditModuleLock WITH (UPDLOCK, HOLDLOCK)
        WHERE AuditId=@AuditId AND ModuleId=@ModuleId AND ExpiresAt>@now AND LockedByUserId<>@UserId
    )
    BEGIN
        SELECT CAST(0 AS INT) AS Locked, LockedByUserId, ExpiresAt, LockToken
        FROM dbo.AuditModuleLock WITH (NOLOCK)
        WHERE AuditId=@AuditId AND ModuleId=@ModuleId;
        RETURN;
    END

    DECLARE @token UNIQUEIDENTIFIER = NEWID();

    MERGE dbo.AuditModuleLock AS t
    USING (SELECT @AuditId AS AuditId, @ModuleId AS ModuleId) AS s
    ON t.AuditId=s.AuditId AND t.ModuleId=s.ModuleId
    WHEN MATCHED THEN
        UPDATE SET LockToken=@token, LockedByUserId=@UserId, ExpiresAt=DATEADD(MINUTE, @Minutes, @now)
    WHEN NOT MATCHED THEN
        INSERT (AuditId, ModuleId, LockToken, LockedByUserId, ExpiresAt)
        VALUES (@AuditId, @ModuleId, @token, @UserId, DATEADD(MINUTE, @Minutes, @now));

    SELECT CAST(1 AS INT) AS Locked,
           @UserId AS LockedByUserId,
           DATEADD(MINUTE, @Minutes, @now) AS ExpiresAt,
           @token AS LockToken;
END
GO

CREATE PROCEDURE dbo.sp_HeartbeatAuditModuleLock
    @AuditId UNIQUEIDENTIFIER,
    @ModuleId UNIQUEIDENTIFIER,
    @LockToken UNIQUEIDENTIFIER,
    @Minutes INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AuditModuleLock
    SET ExpiresAt=DATEADD(MINUTE, @Minutes, GETDATE())
    WHERE AuditId=@AuditId AND ModuleId=@ModuleId AND LockToken=@LockToken;
    SELECT @@ROWCOUNT AS Affected;
END
GO

CREATE PROCEDURE dbo.sp_UnlockAuditModule
    @AuditId UNIQUEIDENTIFIER,
    @ModuleId UNIQUEIDENTIFIER,
    @LockToken UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE dbo.AuditModuleLock
    WHERE AuditId=@AuditId AND ModuleId=@ModuleId AND LockToken=@LockToken;
    SELECT @@ROWCOUNT AS Affected;
END
GO

CREATE PROCEDURE dbo.sp_SubmitAuditModule
    @AuditId UNIQUEIDENTIFIER,
    @ModuleId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- 已判级/整改中/已关闭不允许再提交（需先管理员重开）
    IF EXISTS(SELECT 1 FROM dbo.Audit WITH (NOLOCK) WHERE AuditId=@AuditId AND Status IN (3,4,5))
        RAISERROR('Audit is not editable', 16, 1);

    -- 本模块必须全部已填写
    IF EXISTS(
        SELECT 1
        FROM dbo.TemplateClause c WITH (NOLOCK)
        JOIN dbo.AuditClauseResult r WITH (NOLOCK) ON r.ClauseId=c.ClauseId AND r.AuditId=@AuditId
        WHERE c.ModuleId=@ModuleId AND c.IsActive=1 AND r.Status=0
    )
        RAISERROR('Module has unfilled clauses', 16, 1);

    UPDATE dbo.AuditModuleAssignment
    SET ModuleStatus=2, SubmittedAt=GETDATE()
    WHERE AuditId=@AuditId AND ModuleId=@ModuleId;
END
GO

CREATE PROCEDURE dbo.sp_WithdrawAuditModule
    @AuditId UNIQUEIDENTIFIER,
    @ModuleId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS(SELECT 1 FROM dbo.Audit WITH (NOLOCK) WHERE AuditId=@AuditId AND Status IN (3,4,5))
        RAISERROR('Audit is not editable', 16, 1);

    UPDATE dbo.AuditModuleAssignment
    SET ModuleStatus=1, SubmittedAt=NULL
    WHERE AuditId=@AuditId AND ModuleId=@ModuleId;
END
GO

CREATE PROCEDURE dbo.sp_RateAudit_And_GenCapa
    @AuditId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS(SELECT 1 FROM dbo.Audit WITH (NOLOCK) WHERE AuditId=@AuditId AND Status IN (3,4,5))
        RAISERROR('Audit already rated/closed', 16, 1);

    -- must all modules submitted
    IF EXISTS(
        SELECT 1 FROM dbo.AuditModuleAssignment WITH (NOLOCK)
        WHERE AuditId=@AuditId AND ModuleStatus<>2
    )
        RAISERROR('Not all modules submitted', 16, 1);

    DECLARE @grade CHAR(1) = 'A';

    IF EXISTS(SELECT 1 FROM dbo.AuditClauseResult WITH (NOLOCK) WHERE AuditId=@AuditId AND ClauseLevel='E' AND Status IN (2,3))
        SET @grade='E';
    ELSE IF EXISTS(SELECT 1 FROM dbo.AuditClauseResult WITH (NOLOCK) WHERE AuditId=@AuditId AND ClauseLevel='D' AND Status IN (2,3))
        SET @grade='D';
    ELSE IF EXISTS(SELECT 1 FROM dbo.AuditClauseResult WITH (NOLOCK) WHERE AuditId=@AuditId AND ClauseLevel='C' AND Status IN (2,3))
        SET @grade='C';
    ELSE IF EXISTS(SELECT 1 FROM dbo.AuditClauseResult WITH (NOLOCK) WHERE AuditId=@AuditId AND ClauseLevel='B' AND Status IN (2,3))
        SET @grade='B';

    DECLARE @hasCapa INT = 0;
    IF EXISTS(SELECT 1 FROM dbo.AuditClauseResult WITH (NOLOCK) WHERE AuditId=@AuditId AND Status IN (2,3))
        SET @hasCapa=1;

    BEGIN TRAN;

    INSERT dbo.CapaItem(CapaId, AuditId, ResultId, ClauseCode, ClauseLevel, FindingStatus, Status, CreatedAt)
    SELECT NEWID(), r.AuditId, r.ResultId, r.ClauseCode, r.ClauseLevel, r.Status, 1, GETDATE()
    FROM dbo.AuditClauseResult r WITH (NOLOCK)
    LEFT JOIN dbo.CapaItem c WITH (NOLOCK) ON c.ResultId=r.ResultId
    WHERE r.AuditId=@AuditId AND r.Status IN (2,3) AND c.CapaId IS NULL;

    UPDATE dbo.Audit
    SET FinalGrade=@grade,
        RatedAt=GETDATE(),
        Status=CASE WHEN @hasCapa=1 THEN 4 ELSE 3 END
    WHERE AuditId=@AuditId;

    COMMIT;

    SELECT @grade AS FinalGrade, @hasCapa AS HasCapa;
END
GO

CREATE PROCEDURE dbo.sp_ReopenAudit
    @AuditId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    -- clear capa evidence and items
    DELETE e
    FROM dbo.CapaEvidencePhoto e
    JOIN dbo.CapaItem c ON e.CapaId=c.CapaId
    WHERE c.AuditId=@AuditId;

    DELETE FROM dbo.CapaItem WHERE AuditId=@AuditId;

    -- unlock
    DELETE FROM dbo.AuditModuleLock WHERE AuditId=@AuditId;

    -- modules back to editable
    UPDATE dbo.AuditModuleAssignment
    SET ModuleStatus=1, SubmittedAt=NULL
    WHERE AuditId=@AuditId;

    UPDATE dbo.Audit
    SET Status=2, FinalGrade=NULL, RatedAt=NULL
    WHERE AuditId=@AuditId;

    COMMIT;

    SELECT 1 AS Ok;
END
GO

CREATE PROCEDURE dbo.sp_CreateReAudit_FromAudit
    @FromAuditId UNIQUEIDENTIFIER,
    @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @fid UNIQUEIDENTIFIER;
    DECLARE @year INT;
    SELECT @fid=FactoryId, @year=[Year] FROM dbo.Audit WITH (NOLOCK) WHERE AuditId=@FromAuditId;
    IF @fid IS NULL
        RAISERROR('Audit not found', 16, 1);

    DECLARE @rid UNIQUEIDENTIFIER = NEWID();

    BEGIN TRAN;

    INSERT dbo.ReAudit(ReAuditId, FromAuditId, FactoryId, [Year], Status, CreatedBy, CreatedAt)
    VALUES(@rid, @FromAuditId, @fid, @year, 1, @CreatedBy, GETDATE());

    -- only nonconformity clauses
    INSERT dbo.ReAuditClauseResult(ReResultId, ReAuditId, ClauseCode, ClauseLevel, PrevResultId, PrevStatus, PrevComment, Status, Comment)
    SELECT NEWID(), @rid, r.ClauseCode, r.ClauseLevel, r.ResultId, r.Status, r.Comment, 0, NULL
    FROM dbo.AuditClauseResult r WITH (NOLOCK)
    WHERE r.AuditId=@FromAuditId AND r.Status IN (2,3);

    COMMIT;

    SELECT @rid AS ReAuditId;
END
GO

CREATE PROCEDURE dbo.sp_SubmitReAudit
    @ReAuditId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS(SELECT 1 FROM dbo.ReAudit WITH (NOLOCK) WHERE ReAuditId=@ReAuditId AND Status IN (2,3))
        RAISERROR('ReAudit already submitted/closed', 16, 1);

    IF EXISTS(SELECT 1 FROM dbo.ReAuditClauseResult WITH (NOLOCK) WHERE ReAuditId=@ReAuditId AND Status=0)
        RAISERROR('ReAudit has unfilled clauses', 16, 1);

    DECLARE @passed BIT = 1;
    IF EXISTS(SELECT 1 FROM dbo.ReAuditClauseResult WITH (NOLOCK) WHERE ReAuditId=@ReAuditId AND Status IN (2,3))
        SET @passed = 0;

    UPDATE dbo.ReAudit
    SET Status=2, SubmittedAt=GETDATE(), IsPassed=@passed
    WHERE ReAuditId=@ReAuditId;

    SELECT @passed AS IsPassed;
END
GO

CREATE PROCEDURE dbo.sp_CloseReAudit
    @ReAuditId UNIQUEIDENTIFIER,
    @Conclusion NVARCHAR(2000)
AS
BEGIN
    SET NOCOUNT ON;
    IF @Conclusion IS NULL OR LTRIM(RTRIM(@Conclusion))=''
        RAISERROR('Conclusion required', 16, 1);

    UPDATE dbo.ReAudit
    SET Status=3, ClosedAt=GETDATE(), Conclusion=@Conclusion
    WHERE ReAuditId=@ReAuditId;

    SELECT 1 AS Ok;
END
GO

-- =====================
-- Seed
-- =====================
-- =====================
-- Seed (Dashboard demo data)
-- =====================
DECLARE @fid1 UNIQUEIDENTIFIER = NEWID();
DECLARE @fid2 UNIQUEIDENTIFIER = NEWID();

INSERT dbo.Factory(FactoryId, FactoryCode, Name, ShortName, FactoryType, Address, ContactName, ContactPhone, IsActive)
VALUES
(@fid1, 'F0001', N'示例代工厂（织造）', N'示例织造', 1, N'示例地址1', N'张三', N'13800000000', 1),
(@fid2, 'F0002', N'示例代工厂（织造2）', N'示例织造2', 1, N'示例地址2', N'李四', N'13900000000', 1);

-- Certificates: upcoming + expired
INSERT dbo.FactoryCertificate(CertId, FactoryId, CertName, CertNo, ExpireDate, RelativePath, IsActive)
VALUES
(NEWID(), @fid1, N'营业执照', N'BL-001', DATEADD(DAY, 10, CONVERT(date,GETDATE())), NULL, 1),
(NEWID(), @fid1, N'消防验收', N'FIRE-001', DATEADD(DAY, -5, CONVERT(date,GETDATE())), NULL, 1),
(NEWID(), @fid2, N'排污许可证', N'EP-002', DATEADD(DAY, 45, CONVERT(date,GETDATE())), NULL, 1);

-- Template
DECLARE @tv UNIQUEIDENTIFIER = NEWID();
INSERT dbo.TemplateVersion(TemplateVersionId, VersionNo, FactoryType, IsPublished, PublishedAt)
VALUES(@tv, 'V1.0', 1, 1, GETDATE());

DECLARE @m1 UNIQUEIDENTIFIER = NEWID();
DECLARE @m2 UNIQUEIDENTIFIER = NEWID();
INSERT dbo.TemplateModule(ModuleId, TemplateVersionId, ModuleName, SortNo, IsActive)
VALUES
(@m1, @tv, N'工务/设备', 1, 1),
(@m2, @tv, N'安环/消防', 2, 1);

DECLARE @cA UNIQUEIDENTIFIER = NEWID();
DECLARE @cB UNIQUEIDENTIFIER = NEWID();
DECLARE @cC UNIQUEIDENTIFIER = NEWID();
DECLARE @cD UNIQUEIDENTIFIER = NEWID();
DECLARE @cE UNIQUEIDENTIFIER = NEWID();

INSERT dbo.TemplateClause(ClauseId, ModuleId, ClauseCode, ClauseLevel, Content, SortNo, IsActive)
VALUES
(@cA, @m1, 'A1-01', 'A', N'A级条款示例：设备台账完整', 1, 1),
(@cB, @m1, 'B1-01', 'B', N'B级条款示例：关键设备保养记录齐全', 2, 1),
(@cC, @m1, 'C1-01', 'C', N'C级条款示例：特种设备定检有效', 3, 1),
(@cD, @m2, 'D1-01', 'D', N'D级条款示例：消防通道无阻塞', 1, 1),
(@cE, @m2, 'E1-01', 'E', N'E级条款示例：重大安全隐患（触发即不合格）', 2, 1);

-- Seed audits (for grade trend / module compare / closure rate / risk)
DECLARE @u UNIQUEIDENTIFIER = NEWID();

-- F0001 - 2024 annual: Grade B, closed, CAPA closed
DECLARE @a1 UNIQUEIDENTIFIER = NEWID();
INSERT dbo.Audit(AuditId, AuditType, [Year], FactoryId, TemplateVersionId, Status, FinalGrade, CreatedBy, CreatedAt, RatedAt, CopiedFromAuditId)
VALUES(@a1, 1, YEAR(DATEADD(YEAR,-1,GETDATE())), @fid1, @tv, 5, 'B', @u, DATEADD(DAY,-380,GETDATE()), DATEADD(DAY,-370,GETDATE()), NULL);

INSERT dbo.AuditModuleAssignment(AuditId, ModuleId, OwnerUserId, ModuleStatus, SubmittedAt)
VALUES
(@a1, @m1, NULL, 2, DATEADD(DAY,-372,GETDATE())),
(@a1, @m2, NULL, 2, DATEADD(DAY,-371,GETDATE()));

DECLARE @r1A UNIQUEIDENTIFIER = NEWID();
DECLARE @r1B UNIQUEIDENTIFIER = NEWID();
DECLARE @r1C UNIQUEIDENTIFIER = NEWID();
DECLARE @r1D UNIQUEIDENTIFIER = NEWID();
DECLARE @r1E UNIQUEIDENTIFIER = NEWID();
INSERT dbo.AuditClauseResult(ResultId, AuditId, ClauseId, ClauseCode, ClauseLevel, Status, Comment, UpdatedAt)
VALUES
(@r1A, @a1, @cA, 'A1-01', 'A', 1, N'符合', DATEADD(DAY,-372,GETDATE())),
(@r1B, @a1, @cB, 'B1-01', 'B', 2, N'部分不符合：保养记录缺失', DATEADD(DAY,-372,GETDATE())),
(@r1C, @a1, @cC, 'C1-01', 'C', 1, N'符合', DATEADD(DAY,-372,GETDATE())),
(@r1D, @a1, @cD, 'D1-01', 'D', 1, N'符合', DATEADD(DAY,-371,GETDATE())),
(@r1E, @a1, @cE, 'E1-01', 'E', 1, N'符合', DATEADD(DAY,-371,GETDATE()));

INSERT dbo.CapaItem(CapaId, AuditId, ResultId, ClauseCode, ClauseLevel, FindingStatus, CorrectiveAction, ExternalOwnerName, ExternalOwnerPhone, DueDate, Status, ReviewConclusion, CreatedAt, ClosedByUserId, ClosedAt)
VALUES
(NEWID(), @a1, @r1B, 'B1-01', 'B', 2, N'补齐保养记录并建立月度点检机制', N'王五', N'13700000000', DATEADD(DAY,-330,CONVERT(date,GETDATE())), 3, N'复核通过', DATEADD(DAY,-369,GETDATE()), @u, DATEADD(DAY,-320,GETDATE()));

-- F0001 - 2025 annual: Grade E, rectifying, CAPA overdue
DECLARE @a2 UNIQUEIDENTIFIER = NEWID();
INSERT dbo.Audit(AuditId, AuditType, [Year], FactoryId, TemplateVersionId, Status, FinalGrade, CreatedBy, CreatedAt, RatedAt, CopiedFromAuditId)
VALUES(@a2, 1, YEAR(GETDATE()), @fid1, @tv, 4, 'E', @u, DATEADD(DAY,-40,GETDATE()), DATEADD(DAY,-35,GETDATE()), @a1);

INSERT dbo.AuditModuleAssignment(AuditId, ModuleId, OwnerUserId, ModuleStatus, SubmittedAt)
VALUES
(@a2, @m1, NULL, 2, DATEADD(DAY,-36,GETDATE())),
(@a2, @m2, NULL, 2, DATEADD(DAY,-36,GETDATE()));

DECLARE @r2A UNIQUEIDENTIFIER = NEWID();
DECLARE @r2B UNIQUEIDENTIFIER = NEWID();
DECLARE @r2C UNIQUEIDENTIFIER = NEWID();
DECLARE @r2D UNIQUEIDENTIFIER = NEWID();
DECLARE @r2E UNIQUEIDENTIFIER = NEWID();
INSERT dbo.AuditClauseResult(ResultId, AuditId, ClauseId, ClauseCode, ClauseLevel, Status, Comment, UpdatedAt)
VALUES
(@r2A, @a2, @cA, 'A1-01', 'A', 2, N'部分不符合：台账未更新', DATEADD(DAY,-36,GETDATE())),
(@r2B, @a2, @cB, 'B1-01', 'B', 1, N'符合', DATEADD(DAY,-36,GETDATE())),
(@r2C, @a2, @cC, 'C1-01', 'C', 1, N'符合', DATEADD(DAY,-36,GETDATE())),
(@r2D, @a2, @cD, 'D1-01', 'D', 1, N'符合', DATEADD(DAY,-36,GETDATE())),
(@r2E, @a2, @cE, 'E1-01', 'E', 2, N'部分不符合：重大隐患触发', DATEADD(DAY,-36,GETDATE()));

-- CAPA for A (status 1, no due yet)
INSERT dbo.CapaItem(CapaId, AuditId, ResultId, ClauseCode, ClauseLevel, FindingStatus, Status)
VALUES(NEWID(), @a2, @r2A, 'A1-01', 'A', 2, 1);

-- CAPA for E (status 2, overdue)
INSERT dbo.CapaItem(CapaId, AuditId, ResultId, ClauseCode, ClauseLevel, FindingStatus, CorrectiveAction, ExternalOwnerName, ExternalOwnerPhone, DueDate, Status, CreatedAt)
VALUES
(NEWID(), @a2, @r2E, 'E1-01', 'E', 2, N'立即消除隐患并完善防护措施', N'赵六', N'13600000000', DATEADD(DAY,-2,CONVERT(date,GETDATE())), 2, DATEADD(DAY,-34,GETDATE()));

-- F0002 - 2024 annual: Grade A, closed, no CAPA
DECLARE @a3 UNIQUEIDENTIFIER = NEWID();
INSERT dbo.Audit(AuditId, AuditType, [Year], FactoryId, TemplateVersionId, Status, FinalGrade, CreatedBy, CreatedAt, RatedAt, CopiedFromAuditId)
VALUES(@a3, 1, YEAR(DATEADD(YEAR,-1,GETDATE())), @fid2, @tv, 5, 'A', @u, DATEADD(DAY,-370,GETDATE()), DATEADD(DAY,-365,GETDATE()), NULL);

INSERT dbo.AuditModuleAssignment(AuditId, ModuleId, OwnerUserId, ModuleStatus, SubmittedAt)
VALUES
(@a3, @m1, NULL, 2, DATEADD(DAY,-366,GETDATE())),
(@a3, @m2, NULL, 2, DATEADD(DAY,-366,GETDATE()));

INSERT dbo.AuditClauseResult(ResultId, AuditId, ClauseId, ClauseCode, ClauseLevel, Status, Comment, UpdatedAt)
VALUES
(NEWID(), @a3, @cA, 'A1-01', 'A', 1, N'符合', DATEADD(DAY,-366,GETDATE())),
(NEWID(), @a3, @cB, 'B1-01', 'B', 1, N'符合', DATEADD(DAY,-366,GETDATE())),
(NEWID(), @a3, @cC, 'C1-01', 'C', 1, N'符合', DATEADD(DAY,-366,GETDATE())),
(NEWID(), @a3, @cD, 'D1-01', 'D', 1, N'符合', DATEADD(DAY,-366,GETDATE())),
(NEWID(), @a3, @cE, 'E1-01', 'E', 1, N'符合', DATEADD(DAY,-366,GETDATE()));

-- F0002 - 2025 annual: Grade C, rectifying, CAPA open
DECLARE @a4 UNIQUEIDENTIFIER = NEWID();
INSERT dbo.Audit(AuditId, AuditType, [Year], FactoryId, TemplateVersionId, Status, FinalGrade, CreatedBy, CreatedAt, RatedAt, CopiedFromAuditId)
VALUES(@a4, 1, YEAR(GETDATE()), @fid2, @tv, 4, 'C', @u, DATEADD(DAY,-20,GETDATE()), DATEADD(DAY,-18,GETDATE()), @a3);

INSERT dbo.AuditModuleAssignment(AuditId, ModuleId, OwnerUserId, ModuleStatus, SubmittedAt)
VALUES
(@a4, @m1, NULL, 2, DATEADD(DAY,-19,GETDATE())),
(@a4, @m2, NULL, 2, DATEADD(DAY,-19,GETDATE()));

DECLARE @r4C UNIQUEIDENTIFIER = NEWID();
INSERT dbo.AuditClauseResult(ResultId, AuditId, ClauseId, ClauseCode, ClauseLevel, Status, Comment, UpdatedAt)
VALUES
(NEWID(), @a4, @cA, 'A1-01', 'A', 1, N'符合', DATEADD(DAY,-19,GETDATE())),
(NEWID(), @a4, @cB, 'B1-01', 'B', 1, N'符合', DATEADD(DAY,-19,GETDATE())),
(@r4C, @a4, @cC, 'C1-01', 'C', 3, N'不符合：定检已过期', DATEADD(DAY,-19,GETDATE())),
(NEWID(), @a4, @cD, 'D1-01', 'D', 1, N'符合', DATEADD(DAY,-19,GETDATE())),
(NEWID(), @a4, @cE, 'E1-01', 'E', 1, N'符合', DATEADD(DAY,-19,GETDATE()));

INSERT dbo.CapaItem(CapaId, AuditId, ResultId, ClauseCode, ClauseLevel, FindingStatus, CorrectiveAction, ExternalOwnerName, ExternalOwnerPhone, DueDate, Status, CreatedAt)
VALUES
(NEWID(), @a4, @r4C, 'C1-01', 'C', 3, N'安排复检并更新证照', N'陈七', N'13500000000', DATEADD(DAY, 20, CONVERT(date,GETDATE())), 2, DATEADD(DAY,-17,GETDATE()));

PRINT 'DB init completed.';
