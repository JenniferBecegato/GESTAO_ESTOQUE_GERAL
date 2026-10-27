using System.Runtime.InteropServices;
using System.Text.Json;

namespace GESTAO_ESTOQUE_GERAL;

public sealed class Package
{
    public string Name { get; set; } = "Caixa";
    public string Code { get; set; } = "";
    public decimal Factor { get; set; } = 1;
}
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Outros";
    public string Unit { get; set; } = "unidade";
    public string Location { get; set; } = "";
    public decimal Minimum { get; set; }
    public decimal Target { get; set; }
    public decimal Balance { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? Reference { get; set; }
    public string ReferenceSource { get; set; } = "";
    public DateTime ReferenceDate { get; set; } = DateTime.Today;
    public bool Active { get; set; } = true;
    public List<Package> Packages { get; set; } = [];
}
public sealed class Movement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public string Package { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Factor { get; set; }
    public decimal Converted { get; set; }
    public decimal? Total { get; set; }
    public decimal? ReferenceTotal { get; set; }
    public string ReferenceSource { get; set; } = "";
    public DateTime? ReferenceDate { get; set; }
    public decimal? AppliedCost { get; set; }
    public string Person { get; set; } = "";
    public string Sector { get; set; } = "";
    public string Store { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Invoice { get; set; } = "";
    public string Note { get; set; } = "";
}
public sealed class Inventory
{
    public List<Product> Products { get; set; } = [];
    public List<Movement> Movements { get; set; } = [];
}
// SQLite é fornecido pelo Windows; não requer serviço nem download de pacotes.
public sealed class InventoryStore : IDisposable
{
    private IntPtr db;
    public static string DataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Campos", "GestaoEstoque", "estoque.db");
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open([MarshalAs(UnmanagedType.LPUTF8Str)] string file, out IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, IntPtr callback, IntPtr arg, out IntPtr error);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern void sqlite3_free(IntPtr ptr);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int bytes, out IntPtr stmt, IntPtr tail);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr stmt);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr stmt, int column);
    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr stmt);
    public InventoryStore(string? path = null)
    {
        path ??= DataPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (sqlite3_open(path, out db) != 0) throw new IOException("Não foi possível abrir o banco de estoque.");
        Execute("PRAGMA busy_timeout=5000; PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS inventory (id INTEGER PRIMARY KEY CHECK(id=1), data TEXT NOT NULL);");
    }
    private void Execute(string sql)
    {
        int code = sqlite3_exec(db, sql, IntPtr.Zero, IntPtr.Zero, out var error);
        string message = Marshal.PtrToStringUTF8(error) ?? "Falha de gravação SQLite.";
        if (error != IntPtr.Zero) sqlite3_free(error);
        if (code != 0) throw new IOException(message);
    }
    public Inventory Load()
    {
        if (sqlite3_prepare_v2(db, "SELECT data FROM inventory WHERE id=1", -1, out var stmt, IntPtr.Zero) != 0) throw new IOException("Falha ao consultar os dados.");
        try
        {
            int result = sqlite3_step(stmt);
            if (result == 101) return new Inventory();
            if (result != 100) throw new IOException("Falha ao ler o estoque.");
            return JsonSerializer.Deserialize<Inventory>(Marshal.PtrToStringUTF8(sqlite3_column_text(stmt, 0))!) ?? throw new IOException("Banco de dados inválido.");
        }
        finally { sqlite3_finalize(stmt); }
    }
    public void Save(Inventory state)
    {
        var json = JsonSerializer.Serialize(state).Replace("'", "''");
        Execute("BEGIN IMMEDIATE;");
        try { Execute("INSERT INTO inventory(id,data) VALUES(1,'" + json + "') ON CONFLICT(id) DO UPDATE SET data=excluded.data;"); Execute("COMMIT;"); }
        catch { Execute("ROLLBACK;"); throw; }
    }
    public void Dispose() { if (db != IntPtr.Zero) { sqlite3_close(db); db = IntPtr.Zero; } }
}
public static class InventoryRules
{
    public static void Validate(Product p, Inventory state)
    {
        if (string.IsNullOrWhiteSpace(p.Code) || string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Unit)) throw new ArgumentException("Informe código, nome e unidade de controle.");
        if (p.Minimum < 0 || p.Target < p.Minimum) throw new ArgumentException("O estoque desejado deve ser igual ou maior que o mínimo.");
        var codes = new List<string> { p.Code };
        foreach (var pack in p.Packages)
        {
            if (string.IsNullOrWhiteSpace(pack.Name) || pack.Factor <= 0 || pack.Factor != decimal.Truncate(pack.Factor)) throw new ArgumentException("Informe nome e fator inteiro positivo para cada embalagem.");
            if (!string.IsNullOrWhiteSpace(pack.Code)) codes.Add(pack.Code);
        }
        if (codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != codes.Count) throw new ArgumentException("Os códigos das unidades e embalagens devem ser diferentes.");
        var other = state.Products.Where(x => x.Id != p.Id).SelectMany(x => new[] { x.Code }.Concat(x.Packages.Select(y => y.Code))).Where(x => x.Length > 0);
        if (other.Any(x => codes.Contains(x, StringComparer.OrdinalIgnoreCase))) throw new ArgumentException("Um dos códigos já pertence a outro produto ou embalagem.");
        var old = state.Products.FirstOrDefault(x => x.Id == p.Id);
        if (old != null && old.Unit != p.Unit && state.Movements.Any(x => x.ProductId == p.Id)) throw new ArgumentException("A unidade de um produto movimentado não pode ser alterada.");
    }
    public static void Apply(Inventory state, Movement m)
    {
        var p = state.Products.Single(x => x.Id == m.ProductId);
        if (!p.Active) throw new ArgumentException("O produto está inativo.");
        if (m.Quantity <= 0 || m.Quantity != decimal.Truncate(m.Quantity) || m.Factor <= 0 || string.IsNullOrWhiteSpace(m.Person)) throw new ArgumentException("Informe quantidade inteira positiva e responsável.");
        m.Converted = checked(m.Quantity * m.Factor);
        if (m.Type != "Entrada" && m.Converted > p.Balance) throw new ArgumentException("A quantidade supera o saldo disponível.");
        if (m.Type.StartsWith("Ajuste") && string.IsNullOrWhiteSpace(m.Note)) throw new ArgumentException("O ajuste exige um motivo.");
        if (m.Total < 0) throw new ArgumentException("O valor total não pode ser negativo.");
        m.ProductName = p.Name; m.Category = p.Category; m.Unit = p.Unit;
        if (m.Type == "Entrada")
        {
            if (m.Total.HasValue && (p.Balance == 0 || p.AverageCost.HasValue)) p.AverageCost = ((p.Balance * (p.AverageCost ?? 0)) + m.Total.Value) / (p.Balance + m.Converted);
            else p.AverageCost = null;
            p.Balance += m.Converted;
        }
        else if (m.Type == "Ajuste +") { p.Balance += m.Converted; p.AverageCost = null; }
        else { m.AppliedCost = p.AverageCost; p.Balance -= m.Converted; }
        state.Movements.Add(m);
    }
}
