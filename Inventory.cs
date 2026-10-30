using System.Runtime.InteropServices;
using System.Text.Json;

namespace GESTAO_ESTOQUE_GERAL;

public sealed class Package
{
    public string Name { get; set; } = "Caixa";
    public string Code { get; set; } = "";
    public decimal Factor { get; set; } = 1;
}
public sealed class ControlLevel
{
    public string Kind { get; set; } = "unit";
    public string Name { get; set; } = "unidade";
    public decimal Factor { get; set; } = 1;
    public decimal Minimum { get; set; }
}
public sealed class PackagingStructure
{
    public decimal BundlesPerBox { get; set; }
    public decimal UnitsPerBundle { get; set; }
}
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public List<string> CodeAliases { get; set; } = [];
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Outros";
    public string Unit { get; set; } = "unidade";
    public string Location { get; set; } = "";
    public List<ControlLevel> ControlLevels { get; set; } = [];
    public decimal? MinimumPackageQuantity { get; set; }
    public decimal Minimum { get; set; }
    public decimal Target { get; set; }
    public decimal Balance { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal PurchaseQuantity { get; set; } = 1;
    public decimal? PurchaseTotal { get; set; }
    public decimal? PurchaseUnitCost { get; set; }
    public decimal? Reference { get; set; }
    public string ReferenceSource { get; set; } = "";
    public DateTime ReferenceDate { get; set; } = DateTime.Today;
    public bool Active { get; set; } = true;
    public List<Package> Packages { get; set; } = [];
    public PackagingStructure? PackagingStructure { get; set; }
}
public sealed class MovementPart
{
    public int PackageIndex { get; set; } = -1;
    public decimal Quantity { get; set; }
    public Package? NewPackage { get; set; }
    public string Name { get; set; } = "";
    public decimal Factor { get; set; }
}
public sealed class Movement
{
    public PackagingStructure? PackagingStructure { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<MovementPart> Parts { get; set; } = [];
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
            var loaded=JsonSerializer.Deserialize<Inventory>(Marshal.PtrToStringUTF8(sqlite3_column_text(stmt, 0))!) ?? throw new IOException("Banco de dados inválido.");
            foreach(var product in loaded.Products)if(product.ControlLevels.Count>0){product.MinimumPackageQuantity=null;product.Minimum=checked(product.ControlLevels[^1].Minimum*product.ControlLevels[^1].Factor);product.Target=Math.Max(product.Target,product.Minimum);}
            return loaded;
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
        if(p.ControlLevels.Count>0){
            if(p.ControlLevels.Count>3||p.ControlLevels.Select(l=>l.Kind).Distinct().Count()!=p.ControlLevels.Count||p.ControlLevels.Any(l=>string.IsNullOrWhiteSpace(l.Name)||l.Factor<1||l.Factor!=decimal.Truncate(l.Factor)||l.Minimum<0||l.Minimum!=decimal.Truncate(l.Minimum)||!new[]{"upper","package","unit"}.Contains(l.Kind)))throw new ArgumentException("Níveis de controle inválidos.");
            var baseLevel=p.ControlLevels[^1];
            if(baseLevel.Factor!=1||baseLevel.Name!=p.Unit||p.Packages.Count!=p.ControlLevels.Count-1)throw new ArgumentException("A unidade de controle deve corresponder ao menor nível cadastrado.");
            for(int i=0;i<p.ControlLevels.Count-1;i++)if(p.ControlLevels[i].Factor<p.ControlLevels[i+1].Factor||p.ControlLevels[i].Factor%p.ControlLevels[i+1].Factor!=0||p.Packages[i].Name!=p.ControlLevels[i].Name||p.Packages[i].Factor!=p.ControlLevels[i].Factor)throw new ArgumentException("Conversões entre níveis incompatíveis.");
            p.MinimumPackageQuantity=null;p.Minimum=checked(p.ControlLevels[^1].Minimum*p.ControlLevels[^1].Factor);p.Target=Math.Max(p.Target,p.Minimum);
        }
        if(p.MinimumPackageQuantity is decimal minimumPackages){
            if(minimumPackages<0||minimumPackages!=decimal.Truncate(minimumPackages)||p.Packages.Count==0)throw new ArgumentException("Informe o estoque mínimo inteiro na embalagem de compra.");
            p.Minimum=checked(minimumPackages*p.Packages[0].Factor);
            p.Target=Math.Max(p.Target,p.Minimum);
        }
        if (p.Minimum < 0 || p.Minimum != decimal.Truncate(p.Minimum) || p.Target < p.Minimum) throw new ArgumentException("Informe um mínimo inteiro não negativo e estoque desejado igual ou maior que o mínimo.");
        if(p.PurchaseQuantity<=0||p.PurchaseQuantity!=decimal.Truncate(p.PurchaseQuantity)||p.PurchaseTotal<0)throw new ArgumentException("Informe quantidade comprada inteira positiva e valor total não negativo.");
        var codes = new List<string> { p.Code };
        if(p.CodeAliases.Any(c=>string.IsNullOrWhiteSpace(c)||c.Length>128||c.Any(char.IsControl)))throw new ArgumentException("Código alternativo inválido.");
        codes.AddRange(p.CodeAliases);
        foreach (var pack in p.Packages)
        {
            if (string.IsNullOrWhiteSpace(pack.Name) || pack.Factor <= 0 || pack.Factor != decimal.Truncate(pack.Factor)) throw new ArgumentException("Informe nome e fator inteiro positivo para cada embalagem.");
            if (!string.IsNullOrWhiteSpace(pack.Code)) codes.Add(pack.Code);
        }
        if (codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != codes.Count) throw new ArgumentException("Os códigos das unidades e embalagens devem ser diferentes.");
        var other = state.Products.Where(x => x.Id != p.Id).SelectMany(x => new[] { x.Code }.Concat(x.CodeAliases).Concat(x.Packages.Select(y => y.Code))).Where(x => x.Length > 0);
        if (other.Any(x => codes.Contains(x, StringComparer.OrdinalIgnoreCase))) throw new ArgumentException("Um dos códigos já pertence a outro produto ou embalagem.");
        p.PurchaseUnitCost=p.PurchaseTotal/(p.PurchaseQuantity*(p.Packages.FirstOrDefault()?.Factor??1));
    }
    public static void LinkCode(Inventory state,Guid productId,string code)
    {
        code=code.Trim();
        var p=state.Products.SingleOrDefault(x=>x.Id==productId)??throw new ArgumentException("Produto não encontrado.");
        if(!p.Active)throw new ArgumentException("O produto selecionado está inativo.");
        if(p.Code.Equals(code,StringComparison.OrdinalIgnoreCase)||p.CodeAliases.Contains(code,StringComparer.OrdinalIgnoreCase))return;
        p.CodeAliases.Add(code);Validate(p,state);
    }
    public static void Apply(Inventory state, Movement m)
    {
        var p = state.Products.Single(x => x.Id == m.ProductId);
        if (!p.Active) throw new ArgumentException("O produto está inativo.");
        // Resolve into local values first: rejected operations must not save packaging.
        var packages = new List<Package>(p.Packages);
        if(m.PackagingStructure is {} structure){
            if(m.Type!="Entrada"&&m.Type!="Saída"||structure.BundlesPerBox<1||structure.UnitsPerBundle<1||structure.BundlesPerBox>10000||structure.UnitsPerBundle>10000||structure.BundlesPerBox!=decimal.Truncate(structure.BundlesPerBox)||structure.UnitsPerBundle!=decimal.Truncate(structure.UnitsPerBundle))throw new ArgumentException("Informe conteúdos inteiros positivos para caixa e fardo (até 10.000).");
        }
        var parts = new List<MovementPart>();
        if(m.Parts.Count>0){
            if(m.Type!="Saída" && m.Type!="Entrada")throw new ArgumentException("Apresentações disponíveis apenas em entrada e saída.");
            decimal total=0;var descriptions=new List<string>();
            foreach(var part in m.Parts){
                if(part.Quantity<=0||part.Quantity!=decimal.Truncate(part.Quantity))throw new ArgumentException("Apresentação ou quantidade inválida.");
                int index=part.PackageIndex;
                if(part.NewPackage is {} candidate){
                    if(index!=-1||string.IsNullOrWhiteSpace(candidate.Name)||candidate.Name.Trim().Length>80||candidate.Name.Any(char.IsControl)||candidate.Factor<1||candidate.Factor!=decimal.Truncate(candidate.Factor))throw new ArgumentException("Informe nome e conteúdo inteiro positivo para a embalagem.");
                    string name=candidate.Name.Trim();
                    index=packages.FindIndex(pack=>pack.Name.Equals(name,StringComparison.OrdinalIgnoreCase)&&pack.Factor==candidate.Factor);
                    if(index<0){index=packages.Count;packages.Add(new Package{Name=name,Factor=candidate.Factor});}
                }else if(index< -1||index>=p.Packages.Count)throw new ArgumentException("Embalagem não encontrada.");
                var pack=index<0?null:packages[index];
                decimal factor=pack?.Factor??1;
                if(factor<1||factor!=decimal.Truncate(factor))throw new ArgumentException("Conteúdo da embalagem inválido.");
                parts.Add(new MovementPart{PackageIndex=index,Quantity=part.Quantity,Name=pack?.Name??p.Unit,Factor=factor});
                total=checked(total+part.Quantity*factor);
                descriptions.Add(part.Quantity+" "+(pack==null?p.Unit:pack.Name+" com "+factor+" "+p.Unit));
            }
            m.Quantity=parts.Count==1?parts[0].Quantity:total;m.Factor=parts.Count==1?parts[0].Factor:1;m.Package=string.Join(" + ",descriptions);
        }
        if (m.Quantity <= 0 || m.Quantity != decimal.Truncate(m.Quantity) || m.Factor <= 0 || m.Factor != decimal.Truncate(m.Factor) || string.IsNullOrWhiteSpace(m.Person)) throw new ArgumentException("Informe quantidade e fator inteiros positivos e responsável.");
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
        if(packages.Count!=p.Packages.Count){p.Packages=packages;p.ControlLevels=[];p.MinimumPackageQuantity=null;}
        if(parts.Count>0)m.Parts=parts;
        if(m.PackagingStructure is {} remembered)p.PackagingStructure=new PackagingStructure{BundlesPerBox=remembered.BundlesPerBox,UnitsPerBundle=remembered.UnitsPerBundle};
        state.Movements.Add(m);
    }
}
