using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 32L * 1024 * 1024;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:4173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();

// ── Oracle pool singleton ──────────────────────────────────────────────────
var poolLock = new object();

var cfg = builder.Configuration.GetSection("Oracle");
var currentUser = cfg["User"] ?? "protot";
var currentPassword = cfg["Password"] ?? "protot";
var currentDsn = cfg["Dsn"] ?? "10.223.35.82:1521/mes";

string BuildConnStr(string user, string pass, string dsn) =>
    $"User Id={user};Password={pass};Data Source={dsn};Connection Timeout=10;";

string GetConnStr() => BuildConnStr(currentUser, currentPassword, currentDsn);

// ── Safe identifier (allow only word chars and dots) ──────────────────────
static string SafeIdent(string s)
{
    if (!Regex.IsMatch(s, @"^[\w.]+$"))
        throw new ArgumentException($"Invalid identifier: {s}");
    return s.ToUpper();
}

// ── Skip BLOB/RAW column types ─────────────────────────────────────────────
static bool IsSkipType(OracleDbType t) =>
    t == OracleDbType.Blob || t == OracleDbType.Raw || t == OracleDbType.LongRaw;

// ═══════════════════════════════════════════════════════════════════════════
// POST /api/db/test  — test connection without changing current pool
// ═══════════════════════════════════════════════════════════════════════════
app.MapPost("/api/db/test", async (HttpContext ctx) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
    try
    {
        var host = body.GetProperty("host").GetString() ?? "";
        var port = body.GetProperty("port").GetString() ?? "1521";
        var name = body.GetProperty("name").GetString() ?? "";
        var user = body.GetProperty("user").GetString() ?? "";
        var pass = body.GetProperty("pass").GetString() ?? "";
        var dsn = $"{host}:{port}/{name}";
        var cs = BuildConnStr(user, pass, dsn);
        using var conn = new OracleConnection(cs);
        await conn.OpenAsync();
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// POST /api/db/connect  — update active connection string
// ═══════════════════════════════════════════════════════════════════════════
app.MapPost("/api/db/connect", async (HttpContext ctx) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
    try
    {
        string dsn;
        if (body.TryGetProperty("connectString", out var cs) && cs.GetString() is { Length: > 0 } csStr)
        {
            dsn = csStr;
            currentUser = body.TryGetProperty("user", out var u) ? u.GetString() ?? currentUser : currentUser;
            currentPassword = body.TryGetProperty("pass", out var p) ? p.GetString() ?? currentPassword : currentPassword;
        }
        else
        {
            var host = body.GetProperty("host").GetString() ?? "";
            var port = body.GetProperty("port").GetString() ?? "1521";
            var name = body.GetProperty("name").GetString() ?? "";
            currentUser = body.GetProperty("user").GetString() ?? currentUser;
            currentPassword = body.GetProperty("pass").GetString() ?? currentPassword;
            dsn = $"{host}:{port}/{name}";
        }
        currentDsn = dsn;
        // Verify it works
        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// GET /api/db/tables  — list user tables
// ═══════════════════════════════════════════════════════════════════════════
app.MapGet("/api/db/tables", async () =>
{
    try
    {
        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT table_name FROM user_tables ORDER BY table_name";
        using var reader = await cmd.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        return Results.Ok(new { ok = true, tables });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// GET /api/db/pk  — get primary key columns
// ═══════════════════════════════════════════════════════════════════════════
app.MapGet("/api/db/pk", async (string table) =>
{
    try
    {
        var tbl = SafeIdent(table);
        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT cols.column_name
            FROM all_constraints cons, all_cons_columns cols
            WHERE cons.constraint_type = 'P'
              AND cons.constraint_name = cols.constraint_name
              AND cons.owner = cols.owner
              AND cols.table_name = :tbl
            ORDER BY cols.position";
        cmd.Parameters.Add(new OracleParameter("tbl", tbl));
        using var reader = await cmd.ExecuteReaderAsync();
        var pks = new List<string>();
        while (await reader.ReadAsync()) pks.Add(reader.GetString(0));
        return Results.Ok(new { ok = true, pks });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// GET /api/db/distinct  — distinct values for autocomplete
// ═══════════════════════════════════════════════════════════════════════════
app.MapGet("/api/db/distinct", async (string table, string col) =>
{
    try
    {
        var tbl = SafeIdent(table);
        var colSafe = SafeIdent(col);
        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT DISTINCT {colSafe} FROM {tbl} WHERE {colSafe} IS NOT NULL ORDER BY {colSafe}";
        cmd.FetchSize = 200;
        using var reader = await cmd.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync() && values.Count < 200)
        {
            var v = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (v != null) values.Add(v);
        }
        return Results.Ok(new { ok = true, values });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// GET /api/db/query  — paginated query with sort/search/colFilters
// ═══════════════════════════════════════════════════════════════════════════
app.MapGet("/api/db/query", async (HttpContext ctx) =>
{
    var q = ctx.Request.Query;
    try
    {
        int page = int.TryParse(q["page"], out var pg) ? Math.Max(1, pg) : 1;
        int size = int.TryParse(q["size"], out var sz) ? Math.Clamp(sz, 1, 500) : 20;
        string table = q["table"].ToString();
        string sort = q["sort"].ToString();
        string dir = q["dir"].ToString().ToUpper() == "DESC" ? "DESC" : "ASC";
        string search = q["search"].ToString();
        string colFiltersJson = q["colFilters"].ToString();

        // Parse colFilters: {"ADDA": ["Open", ""]}
        Dictionary<string, List<string>> colFilters = new();
        if (!string.IsNullOrEmpty(colFiltersJson))
        {
            try { colFilters = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(colFiltersJson) ?? new(); }
            catch { }
        }

        // Determine if table is a raw SQL statement
        string innerSql;
        if (table.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            innerSql = $"({table})";
        else
            innerSql = SafeIdent(table);

        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();

        // First: get columns and their types
        using var schemCmd = conn.CreateCommand();
        schemCmd.CommandText = $"SELECT * FROM {innerSql} WHERE ROWNUM = 0";
        schemCmd.FetchSize = 1;
        using var schemaReader = await schemCmd.ExecuteReaderAsync(System.Data.CommandBehavior.SchemaOnly);
        var schemaTable = schemaReader.GetSchemaTable();
        var allCols = new List<(string Name, OracleDbType DbType)>();
        if (schemaTable != null)
        {
            foreach (System.Data.DataRow row in schemaTable.Rows)
            {
                var colName = row["ColumnName"]?.ToString() ?? "";
                var provType = row["ProviderType"];
                OracleDbType dbType = OracleDbType.Varchar2;
                if (provType != null && int.TryParse(provType.ToString(), out int pt))
                    dbType = (OracleDbType)pt;
                allCols.Add((colName, dbType));
            }
        }
        schemaReader.Close();

        // Filter out BLOB/RAW columns
        var displayCols = allCols.Where(c => !IsSkipType(c.DbType)).ToList();
        var stringCols = allCols.Where(c =>
            c.DbType == OracleDbType.Varchar2 || c.DbType == OracleDbType.Char ||
            c.DbType == OracleDbType.NVarchar2 || c.DbType == OracleDbType.NChar ||
            c.DbType == OracleDbType.Long).Select(c => c.Name).ToHashSet();

        var colNames = displayCols.Select(c => c.Name).ToList();
        var colSelect = string.Join(", ", colNames.Select(n => $"\"{n}\""));

        // Build WHERE conditions
        var conditions = new List<string>();
        var parameters = new List<OracleParameter>();
        int pIdx = 0;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchConds = stringCols
                .Select(col => $"UPPER(\"{col}\") LIKE :p{pIdx + stringCols.ToList().IndexOf(col)}")
                .ToList();
            if (searchConds.Any())
            {
                conditions.Add($"({string.Join(" OR ", searchConds)})");
                foreach (var col in stringCols)
                {
                    parameters.Add(new OracleParameter($"p{pIdx++}", $"%{search.ToUpper()}%"));
                }
            }
        }

        foreach (var (col, vals) in colFilters)
        {
            if (vals == null || vals.Count == 0) continue;
            var safCol = SafeIdent(col);
            var hasNull = vals.Contains("") || vals.Contains(null!);
            var nonNull = vals.Where(v => v != null && v != "").ToList();
            var parts = new List<string>();
            if (hasNull) parts.Add($"\"{safCol}\" IS NULL");
            if (nonNull.Any())
            {
                var inParams = nonNull.Select((_, i) => $":cf{pIdx + i}").ToList();
                parts.Add($"\"{safCol}\" IN ({string.Join(", ", inParams)})");
                foreach (var v in nonNull)
                    parameters.Add(new OracleParameter($"cf{pIdx++}", v));
            }
            if (parts.Any())
                conditions.Add($"({string.Join(" OR ", parts)})");
        }

        string whereClause = conditions.Any() ? $"WHERE {string.Join(" AND ", conditions)}" : "";

        // Sort
        string orderClause = "";
        if (!string.IsNullOrWhiteSpace(sort) && colNames.Contains(sort.ToUpper()))
            orderClause = $"ORDER BY \"{sort.ToUpper()}\" {dir}";

        // Count total
        using var cntCmd = conn.CreateCommand();
        cntCmd.CommandText = $"SELECT COUNT(*) FROM {innerSql} {whereClause}";
        foreach (var p in parameters) cntCmd.Parameters.Add(new OracleParameter(p.ParameterName, p.Value));
        var total = Convert.ToInt64(await cntCmd.ExecuteScalarAsync() ?? 0L);
        int pages = (int)Math.Ceiling((double)total / size);

        // Paginated query using ROW_NUMBER
        int rowStart = (page - 1) * size + 1;
        int rowEnd = page * size;
        var pageSql = $@"SELECT {colSelect} FROM (
            SELECT {colSelect}, ROW_NUMBER() OVER ({(string.IsNullOrEmpty(orderClause) ? "ORDER BY ROWNUM" : orderClause)}) AS rn__
            FROM {innerSql} {whereClause}
        ) WHERE rn__ BETWEEN :rn_start AND :rn_end";

        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = pageSql;
        foreach (var p in parameters) dataCmd.Parameters.Add(new OracleParameter(p.ParameterName, p.Value));
        dataCmd.Parameters.Add(new OracleParameter("rn_start", rowStart));
        dataCmd.Parameters.Add(new OracleParameter("rn_end", rowEnd));

        using var dataReader = await dataCmd.ExecuteReaderAsync();
        var rows = new List<List<object?>>();
        while (await dataReader.ReadAsync())
        {
            var row = new List<object?>();
            for (int i = 0; i < colNames.Count; i++)
            {
                if (dataReader.IsDBNull(i)) { row.Add(null); continue; }
                var val = dataReader.GetValue(i);
                row.Add(val?.ToString());
            }
            rows.Add(row);
        }

        return Results.Ok(new { ok = true, columns = colNames, rows, total, page, size, pages });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// POST /api/db/save  — update single cell
// ═══════════════════════════════════════════════════════════════════════════
app.MapPost("/api/db/save", async (HttpContext ctx) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
    try
    {
        var table = SafeIdent(body.GetProperty("table").GetString() ?? "");
        var col = SafeIdent(body.GetProperty("col").GetString() ?? "");
        var rowKey = SafeIdent(body.GetProperty("rowKey").GetString() ?? "");
        var value = body.TryGetProperty("value", out var vp) ? vp.GetString() : null;
        var keyValue = body.GetProperty("keyValue").GetString();

        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE \"{table}\" SET \"{col}\" = :val WHERE \"{rowKey}\" = :key";
        cmd.Parameters.Add(new OracleParameter("val", (object?)value ?? DBNull.Value));
        cmd.Parameters.Add(new OracleParameter("key", keyValue));
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// POST /api/db/delete  — delete row by PK
// ═══════════════════════════════════════════════════════════════════════════
app.MapPost("/api/db/delete", async (HttpContext ctx) =>
{
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
    try
    {
        var table = SafeIdent(body.GetProperty("table").GetString() ?? "");
        var pkCol = SafeIdent(body.GetProperty("pkCol").GetString() ?? "");
        var pkVal = body.GetProperty("pkVal").GetString();

        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM \"{table}\" WHERE \"{pkCol}\" = :pkval";
        cmd.Parameters.Add(new OracleParameter("pkval", pkVal));
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// POST /api/db/insert  — insert new row (multipart: col_XXX fields + files)
// ═══════════════════════════════════════════════════════════════════════════
app.MapPost("/api/db/insert", async (HttpContext ctx) =>
{
    try
    {
        var form = await ctx.Request.ReadFormAsync();
        var table = SafeIdent(form["table"].ToString());

        var colVals = new Dictionary<string, string?>();
        foreach (var key in form.Keys)
        {
            if (key.StartsWith("col_"))
                colVals[SafeIdent(key[4..])] = form[key].ToString() is "" ? null : form[key].ToString();
        }

        // Handle file upload → ZIP
        byte[]? zipBytes = null;
        if (form.Files.Count > 0)
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var file in form.Files)
                {
                    var entry = zip.CreateEntry(file.FileName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await file.CopyToAsync(entryStream);
                }
            }
            zipBytes = ms.ToArray();
        }

        var colList = colVals.Keys.ToList();
        var paramNames = colList.Select((_, i) => $":p{i}").ToList();

        if (zipBytes != null)
        {
            colList.Add("FILECONTENT");
            paramNames.Add(":pblob");
        }

        var colStr = string.Join(", ", colList.Select(c => $"\"{c}\""));
        var paramStr = string.Join(", ", paramNames);

        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO \"{table}\" ({colStr}) VALUES ({paramStr})";

        for (int i = 0; i < colVals.Count; i++)
        {
            var val = colVals[colList[i]];
            cmd.Parameters.Add(new OracleParameter($"p{i}", (object?)val ?? DBNull.Value));
        }
        if (zipBytes != null)
        {
            var blobParam = new OracleParameter("pblob", OracleDbType.Blob);
            blobParam.Value = zipBytes;
            cmd.Parameters.Add(blobParam);
        }

        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// POST /api/db/update  — update row (multipart: col_XXX + files + deleteFile)
// ═══════════════════════════════════════════════════════════════════════════
app.MapPost("/api/db/update", async (HttpContext ctx) =>
{
    try
    {
        var form = await ctx.Request.ReadFormAsync();
        var table = SafeIdent(form["table"].ToString());
        var pkCol = SafeIdent(form["pkCol"].ToString());
        var pkVal = form["pkVal"].ToString();
        var deleteFiles = form["deleteFile"].ToList();

        var colVals = new Dictionary<string, string?>();
        foreach (var key in form.Keys)
        {
            if (key.StartsWith("col_"))
                colVals[SafeIdent(key[4..])] = form[key].ToString() is "" ? null : form[key].ToString();
        }

        // Read existing ZIP if files involved
        bool hasFileOps = form.Files.Count > 0 || deleteFiles.Any();
        byte[]? newZipBytes = null;

        if (hasFileOps)
        {
            // Fetch existing BLOB
            byte[]? existing = null;
            using var conn2 = new OracleConnection(GetConnStr());
            await conn2.OpenAsync();
            using var fetchCmd = conn2.CreateCommand();
            fetchCmd.CommandText = $"SELECT FILECONTENT FROM \"{table}\" WHERE \"{pkCol}\" = :pk";
            fetchCmd.Parameters.Add(new OracleParameter("pk", pkVal));
            using var fr = await fetchCmd.ExecuteReaderAsync();
            if (await fr.ReadAsync() && !fr.IsDBNull(0))
            {
                var lob = fr.GetOracleBlob(0);
                existing = lob.Value;
            }

            // Build merged ZIP
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                // Carry over existing files (minus deleted)
                if (existing != null && existing.Length > 0)
                {
                    try
                    {
                        using var existingMs = new MemoryStream(existing);
                        using var existingZip = new ZipArchive(existingMs, ZipArchiveMode.Read);
                        var newFileNames = form.Files.Select(f => f.FileName).ToHashSet();
                        foreach (var entry in existingZip.Entries)
                        {
                            if (deleteFiles.Contains(entry.Name)) continue;
                            if (newFileNames.Contains(entry.Name)) continue; // will be overwritten
                            var newEntry = zip.CreateEntry(entry.Name, CompressionLevel.Optimal);
                            using var srcStream = entry.Open();
                            using var dstStream = newEntry.Open();
                            await srcStream.CopyToAsync(dstStream);
                        }
                    }
                    catch { /* not a valid ZIP, ignore */ }
                }
                // Add new files
                foreach (var file in form.Files)
                {
                    var entry = zip.CreateEntry(file.FileName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await file.CopyToAsync(entryStream);
                }
            }
            newZipBytes = ms.ToArray();
        }

        // Build SET clause
        var setClauses = colVals.Keys.Select((col, i) => $"\"{col}\" = :p{i}").ToList();
        if (newZipBytes != null) setClauses.Add("FILECONTENT = :pblob");

        if (!setClauses.Any())
            return Results.Ok(new { ok = true }); // nothing to update

        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE \"{table}\" SET {string.Join(", ", setClauses)} WHERE \"{pkCol}\" = :pk";

        int idx = 0;
        foreach (var val in colVals.Values)
            cmd.Parameters.Add(new OracleParameter($"p{idx++}", (object?)val ?? DBNull.Value));
        if (newZipBytes != null)
        {
            var blobParam = new OracleParameter("pblob", OracleDbType.Blob);
            blobParam.Value = newZipBytes;
            cmd.Parameters.Add(blobParam);
        }
        cmd.Parameters.Add(new OracleParameter("pk", pkVal));
        await cmd.ExecuteNonQueryAsync();
        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// GET /api/db/filelist  — list files in ZIP BLOB
// ═══════════════════════════════════════════════════════════════════════════
app.MapGet("/api/db/filelist", async (string table, string pkCol, string pkVal) =>
{
    try
    {
        var tbl = SafeIdent(table);
        var pk = SafeIdent(pkCol);
        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT FILECONTENT FROM \"{tbl}\" WHERE \"{pk}\" = :pkval";
        cmd.Parameters.Add(new OracleParameter("pkval", pkVal));
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.IsDBNull(0))
            return Results.Ok(new { ok = true, files = Array.Empty<object>() });

        var blob = reader.GetOracleBlob(0);
        var bytes = blob.Value;
        try
        {
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var files = zip.Entries.Select(e => new { name = e.Name, size = e.Length }).ToList();
            return Results.Ok(new { ok = true, files });
        }
        catch
        {
            return Results.Ok(new { ok = true, files = Array.Empty<object>() });
        }
    }
    catch (Exception ex)
    {
        return Results.Ok(new { ok = false, error = ex.Message });
    }
});

// ═══════════════════════════════════════════════════════════════════════════
// GET /api/db/filedownload  — download single file from ZIP BLOB
// ═══════════════════════════════════════════════════════════════════════════
app.MapGet("/api/db/filedownload", async (HttpContext ctx, string table, string pkCol, string pkVal, string filename) =>
{
    try
    {
        var tbl = SafeIdent(table);
        var pk = SafeIdent(pkCol);
        using var conn = new OracleConnection(GetConnStr());
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT FILECONTENT FROM \"{tbl}\" WHERE \"{pk}\" = :pkval";
        cmd.Parameters.Add(new OracleParameter("pkval", pkVal));
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.IsDBNull(0))
        {
            ctx.Response.StatusCode = 404;
            return;
        }

        var blob = reader.GetOracleBlob(0);
        var bytes = blob.Value;
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = zip.GetEntry(filename);
        if (entry == null)
        {
            ctx.Response.StatusCode = 404;
            return;
        }

        var ext = Path.GetExtension(filename).TrimStart('.').ToLower();
        var mimeMap = new Dictionary<string, string>
        {
            ["pdf"] = "application/pdf",
            ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg",
            ["gif"] = "image/gif", ["bmp"] = "image/bmp", ["webp"] = "image/webp",
            ["doc"] = "application/msword",
            ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ["xls"] = "application/vnd.ms-excel",
            ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ["csv"] = "text/csv", ["txt"] = "text/plain",
            ["zip"] = "application/zip",
        };
        var mime = mimeMap.TryGetValue(ext, out var m) ? m : "application/octet-stream";

        var encodedFilename = Uri.EscapeDataString(filename);
        ctx.Response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{encodedFilename}";
        ctx.Response.ContentType = mime;
        using var entryStream = entry.Open();
        await entryStream.CopyToAsync(ctx.Response.Body);
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { ok = false, error = ex.Message });
    }
});

app.Run();
