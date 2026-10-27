using System.Data;
using System.Drawing.Printing;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GESTAO_ESTOQUE_GERAL;

public partial class Form1 : Form
{
    private readonly InventoryStore store;
    private Inventory state;
    private readonly Panel content = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(28), BackColor = Color.FromArgb(243,245,250) };
    private readonly FlowLayoutPanel nav = new() { Dock = DockStyle.Left, Width = 218, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12,24,12,12), BackColor = Navy };
    private readonly ToolTip tips = new() { AutoPopDelay = 20000, InitialDelay = 300, ReshowDelay = 100 };
    private readonly List<Control> owned = [];
    private static readonly Color Navy = Color.FromArgb(30,50,101);
    private static readonly Color Gold = Color.FromArgb(173,144,82);
    private readonly string[] categories = ["Higiene", "Limpeza", "Escritório", "Copa", "Descartáveis", "Outros"];
    private string current = "Visão geral";
    public Form1(string? databasePath = null)
    {
        InitializeComponent();
        Text = "Campos | Gestão de Estoque Geral";
        Font = new Font("Segoe UI", 10);
        ForeColor = Color.FromArgb(35,49,75);
        MinimumSize = new Size(1100,740);
        Size = new Size(1440,940);
        StartPosition = FormStartPosition.CenterScreen;
        store = new InventoryStore(databasePath); state = store.Load();
        var header = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.White, Padding = new Padding(26,15,26,15) };
        var logo = new PictureBox { Dock = DockStyle.Left, Width = 180, SizeMode = PictureBoxSizeMode.Zoom };
        using (var stream = typeof(Form1).Assembly.GetManifestResourceStream("GESTAO_ESTOQUE_GERAL.logo.png"))
            if (stream != null) { using var img = Image.FromStream(stream); logo.Image = new Bitmap(img); }
        header.Controls.Add(Label("    GESTÃO DE ESTOQUE  •  USO INTERNO", 11, false, DockStyle.Fill));
        header.Controls.Add(logo);
        var status = new StatusStrip { BackColor = Color.White };
        status.Items.Add(new ToolStripStatusLabel("Dados locais • " + InventoryStore.DataPath));
        Controls.Add(content); Controls.Add(nav); Controls.Add(header); Controls.Add(status);
        foreach (string title in new[] { "Visão geral", "Produtos", "Cadastro de produto", "Entrada", "Saída", "Histórico", "Relatórios", "Descrição do sistema" })
        {
            var b = Button(title, () => Navigate(title)); b.Width = 190; b.Height = 45; b.TextAlign = ContentAlignment.MiddleLeft;
            nav.Controls.Add(b);
        }
        Navigate("Visão geral");
        FormClosed += (_, _) => { store.Dispose(); tips.Dispose(); logo.Image?.Dispose(); };
    }
    private static Label Label(string text, float size = 10, bool bold = false, DockStyle dock = DockStyle.None) => new() { Text = text, AutoSize = dock == DockStyle.None, Dock = dock, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Padding = new Padding(0,6,0,6), TextAlign = ContentAlignment.MiddleLeft };
    private static Button Button(string text, Action action, bool secondary = false)
    {
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(115,38), Padding = new Padding(10,4,10,4), FlatStyle = FlatStyle.Flat, BackColor = secondary ? Color.FromArgb(235,239,247) : Navy, ForeColor = secondary ? Navy : Color.White, Cursor = Cursors.Hand, Margin = new Padding(0,5,10,5) };
        b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => action(); return b;
    }
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Verifique os dados", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void Commit(Action<Inventory> action)
    {
        var next = JsonSerializer.Deserialize<Inventory>(JsonSerializer.Serialize(state))!;
        action(next); store.Save(next); state = next;
    }
    private void Navigate(string title)
    {
        current = title;
        foreach (Button b in nav.Controls) { b.BackColor = b.Text == title ? Color.White : Navy; b.ForeColor = b.Text == title ? Navy : Color.FromArgb(210,220,240); }
        foreach (var c in owned) c.Dispose(); owned.Clear();
        while (content.Controls.Count > 0) content.Controls[0].Dispose();
        var body = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = false, Location = new Point(28,20) };
        content.Controls.Add(body);
        void SizeBody() { body.Width = Math.Max(760, content.ClientSize.Width - 74); foreach (Control c in body.Controls) c.Width = body.Width - 8; body.Height = body.GetPreferredSize(new Size(body.Width,0)).Height + 20; }
        content.SizeChanged += ResizeBody;
        void ResizeBody(object? s, EventArgs e) { if (!body.IsDisposed) SizeBody(); }
        body.Disposed += (_, _) => content.SizeChanged -= ResizeBody;
        body.Layout += (_,_) => { if(body.Height != body.GetPreferredSize(new Size(body.Width,0)).Height + 20) body.Height = body.GetPreferredSize(new Size(body.Width,0)).Height + 20; };
        Add(body, Label(title, 23, true));
        switch (title)
        {
            case "Visão geral": Dashboard(body); break;
            case "Produtos": Products(body); break;
            case "Cadastro de produto": ProductEditor(body); break;
            case "Entrada": MovementEditor(body, true); break;
            case "Saída": MovementEditor(body, false); break;
            case "Histórico": History(body); break;
            case "Relatórios": Reports(body); break;
            default: Description(body); break;
        }
        SizeBody();
    }
    private static void Add(FlowLayoutPanel parent, Control child) { child.Margin = new Padding(0,0,0,16); parent.Controls.Add(child); }
    private static FlowLayoutPanel Actions(params Control[] controls) { var p = new FlowLayoutPanel { AutoSize = true, WrapContents = true }; p.Controls.AddRange(controls); return p; }
    private static Panel Notice(string text)
    {
        var p = new Panel { Height = 62, BackColor = Color.FromArgb(249,246,237), Padding = new Padding(14,3,14,3) };
        p.Controls.Add(Label(text, 10, false, DockStyle.Fill)); return p;
    }
    private static string Money(decimal? value) => value.HasValue ? value.Value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")) : "Custo desconhecido";
    private static string Qty(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
    private Control Metrics(params (string Title, string Value, string Help)[] values)
    {
        var panel = new TableLayoutPanel { Height = 115, ColumnCount = values.Length, RowCount = 1 };
        foreach (var v in values)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / values.Length));
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0,0,12,0), Padding = new Padding(15,8,10,8) };
            card.Controls.Add(Label(v.Value, 20, true, DockStyle.Fill));
            var title = Label(v.Title + "  ⓘ", 9, false, DockStyle.Top); title.Height = 28;
            card.Controls.Add(title); tips.SetToolTip(title, v.Help); tips.SetToolTip(card, v.Help); panel.Controls.Add(card);
        }
        return panel;
    }
    private static DataGridView Grid(int height = 300)
    {
        var g = new DataGridView { Height = height, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, AutoGenerateColumns = true, EnableHeadersVisualStyles = false };
        g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(233,238,247), ForeColor = Navy, Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(5) };
        g.DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(5), SelectionBackColor = Color.FromArgb(216,226,245), SelectionForeColor = Navy, NullValue = "—" };
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248,250,253);
        g.RowTemplate.Height = 34; g.ColumnHeadersHeight = 42;
        return g;
    }
    private static DataTable Table(string[] headers, IEnumerable<object?[]> rows)
    {
        var dt = new DataTable(); foreach (string h in headers) dt.Columns.Add(h);
        foreach (var row in rows) dt.Rows.Add(row.Select(x => x ?? DBNull.Value).ToArray()); return dt;
    }
    private DataTable ProductTable(IEnumerable<Product> products) => Table(["Código", "Produto", "Categoria", "Saldo", "Mínimo", "Desejado", "Local", "Situação"], products.Select(p => new object?[] { p.Code, p.Name, p.Category, Qty(p.Balance) + " " + p.Unit, Qty(p.Minimum), Qty(p.Target), p.Location, !p.Active ? "Inativo" : p.Balance <= p.Minimum ? "Estoque baixo" : "Normal" }));
    private void Dashboard(FlowLayoutPanel body)
    {
        Add(body, Label("Materiais de uso interno • posição atual do estoque"));
        var active = state.Products.Where(p => p.Active).ToList();
        var month = state.Movements.Where(m => m.Type == "Entrada" && m.PurchaseDate.Year == DateTime.Today.Year && m.PurchaseDate.Month == DateTime.Today.Month).ToList();
        var refs = month.Where(m => m.ReferenceTotal.HasValue && m.Total.HasValue).ToList();
        Add(body, Metrics(("Produtos cadastrados", active.Count.ToString(), "Produtos ativos."), ("Precisam de atenção", active.Count(p => p.Balance <= p.Minimum).ToString(), "Saldo igual ou inferior ao estoque mínimo."), ("Compras no mês", Money(month.Sum(m => m.Total ?? 0)), $"{month.Count(m => !m.Total.HasValue)} itens sem custo informado; soma somente dos valores conhecidos."), ("Economia no mês", refs.Count > 0 ? Money(refs.Sum(m => m.ReferenceTotal - m.Total)) : "Não calculada", $"Referência menos valor pago. Cobertura: {refs.Count} de {month.Count} itens.")));
        Add(body, Actions(Button("+ Registrar entrada", () => Navigate("Entrada")), Button("− Registrar saída", () => Navigate("Saída"), true), Button("Cadastrar produto", () => Navigate("Cadastro de produto"), true)));
        if (active.Count == 0) Add(body, Notice("Comece cadastrando os produtos. Depois registre o saldo inicial na tela Entrada; os dados serão salvos neste computador."));
        Add(body, Label("Produtos com estoque baixo", 15, true));
        var g = Grid(250); g.DataSource = ProductTable(active.Where(p => p.Balance <= p.Minimum)); Add(body,g);
        int unknown = active.Count(p => p.Balance > 0 && !p.AverageCost.HasValue);
        Add(body, Metrics(("Valor conhecido em estoque", Money(active.Sum(p => p.Balance * (p.AverageCost ?? 0))), $"{unknown} produtos com saldo de custo desconhecido não entram neste valor."), ("Últimas movimentações", state.Movements.Count.ToString(), "Entradas, saídas e ajustes registrados no histórico.")));
        Add(body, Notice($"Valor do estoque: {(unknown > 0 ? "estimativa parcial — há " + unknown + " produto(s) com custo desconhecido." : "todos os saldos têm custo conhecido.")} O gasto de compra usa a data da compra; o histórico usa a data do recebimento/retirada."));
    }
    private static TextBox TextField(string value = "") => new() { Text = value, Dock = DockStyle.Top };
    private static NumericUpDown Number(decimal value = 0, int decimals = 0) => new() { DecimalPlaces = decimals, Minimum = 0, Maximum = 1000000000, Value = Math.Clamp(value,0,1000000000), Dock = DockStyle.Top, ThousandsSeparator = true };
    private static ComboBox Combo(IEnumerable<string> items, bool editable = false)
    {
        var c = new ComboBox { DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
        c.Items.AddRange(items.Cast<object>().ToArray()); if(c.Items.Count > 0) c.SelectedIndex = 0; return c;
    }
    private static TableLayoutPanel Fields() => new() { ColumnCount = 2, AutoSize = false, BackColor = Color.White, Padding = new Padding(18), GrowStyle = TableLayoutPanelGrowStyle.AddRows, ColumnStyles = { new ColumnStyle(SizeType.Percent,50), new ColumnStyle(SizeType.Percent,50) } };
    private static void Field(TableLayoutPanel table, string name, Control control)
    {
        int index = table.Controls.Count; int row = index / 2, col = index % 2;
        var p = new Panel { Height = 75, Dock = DockStyle.Fill, Margin = new Padding(0,0,20,6) };
        p.Controls.Add(control); var l = Label(name,9,true,DockStyle.Top); l.Height = 32; p.Controls.Add(l);
        if(table.RowCount <= row) { table.RowCount = row + 1; table.RowStyles.Add(new RowStyle(SizeType.Absolute,81)); }
        table.Controls.Add(p,col,row); table.Height = table.RowCount * 81 + 36;
    }
    private void Products(FlowLayoutPanel body)
    {
        var search = TextField(); search.PlaceholderText = "Buscar por nome ou código (inclui embalagens)";
        var cat = Combo(new[] { "Todas as categorias" }.Concat(categories));
        var inactive = new CheckBox { Text = "Mostrar inativos", AutoSize = true };
        var f = Fields(); Field(f,"Pesquisa",search); Field(f,"Categoria",cat); Add(body,f); Add(body,inactive);
        var g = Grid(360); List<Product> visible = [];
        void Refresh()
        {
            visible = state.Products.Where(p => (inactive.Checked || p.Active) && (cat.SelectedIndex == 0 || p.Category == cat.Text) && (p.Name + " " + p.Code + " " + string.Join(" ",p.Packages.Select(x => x.Code))).Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Name).ToList();
            g.DataSource = ProductTable(visible);
        }
        Product? Selected() => g.CurrentRow != null && g.CurrentRow.Index < visible.Count ? visible[g.CurrentRow.Index] : null;
        Add(body,Actions(Button("+ Cadastrar", () => Navigate("Cadastro de produto")), Button("Editar selecionado", () => { if(Selected() is {} p) OpenProduct(p); },true), Button("Ativar / inativar", () => Guard(() => { if (Selected() is not {} p) return; Commit(s => s.Products.Single(x => x.Id == p.Id).Active = !p.Active); Refresh(); }),true), Button("Imprimir etiqueta", () => { if(Selected() is {} p) PrintLabel(p); },true), Button("Exportar CSV", () => Export((DataTable)g.DataSource!),true)));
        Add(body,g); search.TextChanged += (_,_) => Refresh(); cat.SelectedIndexChanged += (_,_) => Refresh(); inactive.CheckedChanged += (_,_) => Refresh();
        g.CellDoubleClick += (_,e) => { if(e.RowIndex >= 0 && Selected() is {} p) OpenProduct(p); }; Refresh();
    }
    private void OpenProduct(Product p)
    {
        Navigate("Cadastro de produto"); var body = (FlowLayoutPanel)content.Controls[0];
        foreach (Control c in body.Controls.Cast<Control>().Skip(1).ToArray()) c.Dispose();
        ProductEditor(body,p);
        foreach(Control c in body.Controls) c.Width = body.Width - 8;
    }
    private void ProductEditor(FlowLayoutPanel body, Product? original = null, string codeValue = "")
    {
        var p = original == null ? new Product() : JsonSerializer.Deserialize<Product>(JsonSerializer.Serialize(original))!;
        var code = TextField(original?.Code ?? codeValue); var name = TextField(p.Name); var unit = TextField(p.Unit); var location = TextField(p.Location);
        var cat = Combo(categories,true); cat.Text = p.Category;
        var min = Number(p.Minimum); var target = Number(p.Target); var reference = Number(p.Reference ?? 0,4);
        var hasRef = new CheckBox { Text = "Usar preço de referência", Checked = p.Reference.HasValue, Dock = DockStyle.Top };
        var origin = TextField(p.ReferenceSource); var refDate = new DateTimePicker { Value = p.ReferenceDate, Format = DateTimePickerFormat.Short, Dock = DockStyle.Top };
        var active = new CheckBox { Text = "Produto ativo", Checked = p.Active, Dock = DockStyle.Top };
        var f = Fields(); Field(f,"Código de barras ou interno *",code); Field(f,"Nome do produto *",name); Field(f,"Categoria",cat); Field(f,"Unidade de controle *",unit); Field(f,"Estoque mínimo",min); Field(f,"Estoque desejado",target); Field(f,"Local de armazenamento",location); Field(f,"Situação",active); Field(f,"Referência opcional",hasRef); Field(f,"Preço de referência / unidade (R$)",reference); Field(f,"Origem da referência",origin); Field(f,"Data da referência",refDate); Add(body,f);
        Add(body, Label("Embalagens e códigos adicionais",15,true));
        var packs = new DataGridView { Height = 170, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, AllowUserToAddRows = true, AllowUserToDeleteRows = true };
        packs.Columns.Add("Name","Embalagem (ex.: Fardo)"); packs.Columns.Add("Code","Código de barras (opcional)"); packs.Columns.Add("Factor","Unidades por embalagem");
        foreach(var pack in p.Packages) packs.Rows.Add(pack.Name,pack.Code,pack.Factor);
        Add(body,packs);
        Add(body,Notice("O saldo inicial é registrado em Entrada. Cada embalagem possui fator próprio; os códigos devem ser únicos. Para remover uma embalagem, selecione sua linha e pressione Delete."));
        Add(body,Actions(Button("Salvar produto", () => Guard(() =>
        {
            packs.EndEdit();
            p.Code = code.Text.Trim(); p.Name = name.Text.Trim(); p.Unit = unit.Text.Trim(); p.Category = cat.Text.Trim(); p.Location = location.Text.Trim(); p.Minimum = min.Value; p.Target = target.Value; p.Active = active.Checked;
            p.Reference = hasRef.Checked ? reference.Value : null; p.ReferenceSource = origin.Text.Trim(); p.ReferenceDate = refDate.Value.Date;
            p.Packages = [];
            foreach(DataGridViewRow r in packs.Rows)
            {
                if(r.IsNewRow) continue;
                if(!decimal.TryParse(Convert.ToString(r.Cells[2].Value),out var factor)) throw new ArgumentException("Informe o fator de todas as embalagens.");
                p.Packages.Add(new Package { Name = Convert.ToString(r.Cells[0].Value)?.Trim() ?? "", Code = Convert.ToString(r.Cells[1].Value)?.Trim() ?? "", Factor = factor });
            }
            Commit(s => { InventoryRules.Validate(p,s); s.Products.RemoveAll(x => x.Id == p.Id); s.Products.Add(p); });
            Navigate("Produtos");
        })),Button("Gerar código interno",() => code.Text = "INT" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),true),Button("Cancelar",() => Navigate("Produtos"),true)));
    }
    private sealed record Choice(Guid Id,string Name) { public override string ToString() => Name; }
    private sealed record PackChoice(string Name,decimal Factor) { public override string ToString() => Name; }
    private void MovementEditor(FlowLayoutPanel body, bool entry)
    {
        Add(body,Label(entry ? "Recebimento e compras • converta caixas e fardos em unidades de controle." : "Retiradas para uso interno • informe o responsável e o destino."));
        var search = TextField(); search.PlaceholderText = "Leia ou digite o código e pressione Enter";
        var product = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
        product.Items.AddRange(state.Products.Where(p => p.Active).OrderBy(p => p.Name).Select(p => new Choice(p.Id,p.Name + " • " + p.Code)).Cast<object>().ToArray());
        var pack = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
        var quantity = Number(1); quantity.Minimum = 1;
        var person = TextField(); var sector = TextField(); var note = TextField();
        var date = new DateTimePicker { Value = DateTime.Now, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm", Dock = DockStyle.Top };
        var purchaseDate = new DateTimePicker { Value = DateTime.Today, Format = DateTimePickerFormat.Short, Dock = DockStyle.Top };
        var storeName = new ComboBox { Dock = DockStyle.Top }; storeName.Items.AddRange(state.Movements.Select(m => m.Store).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        var supplier = TextField(); var invoice = TextField();
        var priced = new CheckBox { Text = "Preço conhecido (desmarque para saldo inicial sem custo)", Checked = true, AutoSize = true, Dock = DockStyle.Top };
        var price = Number(0,4); var discount = Number(0,2); var addition = Number(0,2); var reference = Number(0,4);
        var refEnabled = new CheckBox { Text = "Comparar com referência", Dock = DockStyle.Top };
        var conversion = Label("Selecione um produto.",11,true); conversion.Height = 64; conversion.AutoSize = false;
        var f = Fields(); Field(f,"Código / leitor",search); Field(f,"Produto (pesquisa pelo nome)",product); Field(f,"Quantidade *",quantity); Field(f,"Unidade movimentada",pack); Field(f,"Responsável *",person); Field(f,"Setor / destino",sector); Field(f,"Data do recebimento / retirada",date); Field(f,"Observação",note);
        if(entry)
        {
            Field(f,"Local da compra * (quando houver preço)",storeName); Field(f,"Data da compra",purchaseDate); Field(f,"Fornecedor (se diferente)",supplier); Field(f,"Nota / comprovante",invoice); Field(f,"Tipo de entrada",priced); Field(f,"Preço por embalagem selecionada (R$)",price); Field(f,"Desconto deste item (R$)",discount); Field(f,"Acréscimo deste item (R$)",addition); Field(f,"Referência opcional",refEnabled); Field(f,"Referência por unidade de controle (R$)",reference);
        }
        Add(body,f); Add(body,conversion);
        var cart = new List<Movement>(); var cartGrid = Grid(160);
        Product? Selected() => product.SelectedItem is Choice ch ? state.Products.SingleOrDefault(p => p.Id == ch.Id) : null;
        void Update()
        {
            if(Selected() is not {} p || pack.SelectedItem is not PackChoice pk) return;
            var converted = quantity.Value * pk.Factor;
            conversion.Text = $"Saldo: {Qty(p.Balance)} {p.Unit} • Local: {p.Location}\nEquivale a {Qty(converted)} {p.Unit}" + (entry && priced.Checked ? $" • Total: {Money(quantity.Value * price.Value - discount.Value + addition.Value)} • Custo / unidade: {Money((quantity.Value * price.Value - discount.Value + addition.Value)/converted)}" : "");
        }
        void SelectProduct()
        {
            pack.Items.Clear();
            if(Selected() is not {} p) return;
            pack.Items.Add(new PackChoice(p.Unit + " (unidade de controle)",1)); foreach(var pk in p.Packages) pack.Items.Add(new PackChoice(pk.Name + " — " + Qty(pk.Factor) + " " + p.Unit,pk.Factor));
            pack.SelectedIndex = 0; reference.Value = p.Reference ?? 0; refEnabled.Checked = p.Reference.HasValue; Update();
        }
        void Find()
        {
            var code = search.Text.Trim(); if(code.Length == 0) return;
            var p = state.Products.FirstOrDefault(p => p.Active && (p.Code.Equals(code,StringComparison.OrdinalIgnoreCase) || p.Packages.Any(x => x.Code.Length > 0 && x.Code.Equals(code,StringComparison.OrdinalIgnoreCase))));
            if(p == null)
            {
                if(MessageBox.Show(this,"Código não encontrado. Deseja cadastrar o produto?","Localizar produto",MessageBoxButtons.YesNo,MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Navigate("Cadastro de produto"); var b = (FlowLayoutPanel)content.Controls[0]; foreach(Control c in b.Controls.Cast<Control>().Skip(1).ToArray()) c.Dispose(); ProductEditor(b,null,code); foreach(Control c in b.Controls) c.Width = b.Width-8;
                }
                return;
            }
            product.SelectedItem = product.Items.Cast<Choice>().First(x => x.Id == p.Id); SelectProduct();
            int packIndex = p.Packages.FindIndex(x => x.Code.Length > 0 && x.Code.Equals(code,StringComparison.OrdinalIgnoreCase)); if(packIndex >= 0) pack.SelectedIndex = packIndex + 1;
            quantity.Focus(); quantity.Select(0,quantity.Text.Length);
        }
        Movement Build()
        {
            var p = Selected() ?? throw new ArgumentException("Selecione o produto.");
            var pk = pack.SelectedItem as PackChoice ?? throw new ArgumentException("Selecione a embalagem.");
            decimal total = quantity.Value * price.Value - discount.Value + addition.Value;
            if(string.IsNullOrWhiteSpace(person.Text)) throw new ArgumentException("Informe o responsável.");
            if(date.Value > DateTime.Now.AddMinutes(1)) throw new ArgumentException("A data da movimentação não pode estar no futuro.");
            if(entry && priced.Checked && (string.IsNullOrWhiteSpace(storeName.Text) || total < 0 || purchaseDate.Value.Date > date.Value.Date)) throw new ArgumentException("Informe o local da compra, total válido e data de compra anterior ou igual ao recebimento.");
            return new Movement { ProductId = p.Id, Type = entry ? "Entrada" : "Saída", Date = date.Value, PurchaseDate = purchaseDate.Value.Date, Package = pk.Name, Quantity = quantity.Value, Factor = pk.Factor, Converted = quantity.Value * pk.Factor, Person = person.Text.Trim(), Sector = sector.Text.Trim(), Note = note.Text.Trim(), Store = entry && priced.Checked ? storeName.Text.Trim() : "", Supplier = supplier.Text.Trim(), Invoice = invoice.Text.Trim(), Total = entry && priced.Checked ? total : null, ReferenceTotal = entry && priced.Checked && refEnabled.Checked ? reference.Value * quantity.Value * pk.Factor : null, ReferenceSource = p.ReferenceSource, ReferenceDate = refEnabled.Checked ? p.ReferenceDate : null, ProductName = p.Name, Category = p.Category, Unit = p.Unit };
        }
        void ShowCart() => cartGrid.DataSource = Table(["Produto","Quantidade","Embalagem","Total","Local","Nota"],cart.Select(m => new object?[] { m.ProductName,Qty(m.Converted) + " " + m.Unit,m.Package,Money(m.Total),m.Store,m.Invoice }));
        if(entry)
        {
            Add(body,Actions(Button("Adicionar item à compra",() => Guard(() =>
            {
                var m = Build();
                if(cart.Count > 0 && (m.Store != cart[0].Store || m.PurchaseDate != cart[0].PurchaseDate || m.Invoice != cart[0].Invoice || m.Supplier != cart[0].Supplier || m.Person != cart[0].Person)) throw new ArgumentException("Os itens da mesma compra devem ter o mesmo local, fornecedor, data, nota e responsável.");
                cart.Add(m); ShowCart();
            }),true),Button("Remover item selecionado",() => { if(cartGrid.CurrentRow is {} row && row.Index < cart.Count) { cart.RemoveAt(row.Index); ShowCart(); } },true)));
            Add(body,cartGrid);
            Add(body,Notice("Para compra com vários produtos, adicione cada item e confirme ao final. Desconto e acréscimo são valores atribuídos ao item; distribua frete/desconto da nota uma única vez entre os itens."));
        }
        Add(body,Actions(Button(entry ? "Confirmar entrada / compra" : "Confirmar saída", () => Guard(() =>
        {
            var items = entry && cart.Count > 0 ? cart.ToList() : new List<Movement> { Build() };
            if(entry && cart.Count > 0 && MessageBox.Show(this,$"Confirmar os {items.Count} itens da lista? O formulário atual só entra se adicionado à compra.","Confirmar compra",MessageBoxButtons.OKCancel,MessageBoxIcon.Question) != DialogResult.OK) return;
            var purchaseId = Guid.NewGuid();
            Commit(s => { foreach(var m in items) { if(entry) m.PurchaseId = purchaseId; InventoryRules.Apply(s,m); } });
            MessageBox.Show(this,"Movimentação registrada e salva.","Estoque",MessageBoxButtons.OK,MessageBoxIcon.Information); Navigate(current);
        })),Button("Buscar código",Find,true)));
        product.SelectedIndexChanged += (_,_) => SelectProduct(); pack.SelectedIndexChanged += (_,_) => Update();
        foreach(var n in new[] {quantity,price,discount,addition}) n.ValueChanged += (_,_) => Update(); priced.CheckedChanged += (_,_) => Update();
        search.KeyDown += (_,e) => { if(e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Guard(Find); } };
        if(product.Items.Count > 0) product.SelectedIndex = 0;
    }
    private void History(FlowLayoutPanel body)
    {
        var start = new DateTimePicker { Value = DateTime.Today.AddMonths(-1), Format = DateTimePickerFormat.Short, Dock = DockStyle.Top };
        var end = new DateTimePicker { Value = DateTime.Today, Format = DateTimePickerFormat.Short, Dock = DockStyle.Top };
        var type = Combo(["Todos", "Entrada", "Saída", "Ajuste +", "Ajuste −"]); var query = TextField(); var person = TextField(); var sector = TextField();
        var f = Fields(); Field(f,"Data inicial",start); Field(f,"Data final",end); Field(f,"Tipo",type); Field(f,"Produto ou código",query); Field(f,"Responsável",person); Field(f,"Setor",sector); Add(body,f);
        var g = Grid(360);
        void Refresh()
        {
            if(start.Value.Date > end.Value.Date) { g.DataSource = new DataTable(); return; }
            var rows = state.Movements.Where(m => m.Date.Date >= start.Value.Date && m.Date.Date <= end.Value.Date && (type.SelectedIndex == 0 || m.Type == type.Text) && (m.ProductName + " " + state.Products.FirstOrDefault(p => p.Id == m.ProductId)?.Code).Contains(query.Text,StringComparison.OrdinalIgnoreCase) && m.Person.Contains(person.Text,StringComparison.OrdinalIgnoreCase) && m.Sector.Contains(sector.Text,StringComparison.OrdinalIgnoreCase)).OrderByDescending(m => m.Date);
            g.DataSource = Table(["Data", "Tipo", "Produto", "Quantidade", "Embalagem / fator", "Responsável", "Setor", "Local compra", "Valor / custo", "Nota / motivo"],rows.Select(m => new object?[] { m.Date.ToString("dd/MM/yyyy HH:mm"),m.Type,m.ProductName,Qty(m.Converted) + " " + m.Unit,Qty(m.Quantity) + " × " + m.Package + " [" + Qty(m.Factor) + "]",m.Person,m.Sector,m.Store,Money(m.Type == "Entrada" ? m.Total : m.AppliedCost * m.Converted),m.Note + (m.Invoice.Length > 0 ? " • NF " + m.Invoice : "") }));
        }
        Add(body,Actions(Button("Aplicar filtros",Refresh),Button("Registrar ajuste",() => Adjustment(),true),Button("Exportar CSV",() => Export((DataTable)g.DataSource!),true),Button("Imprimir / PDF",() => PrintTable((DataTable)g.DataSource!,"Histórico de movimentações"),true)));
        Add(body,g); Add(body,Notice("Os registros são preservados. Para corrigir quantidades, registre um ajuste com motivo e responsável. Alterações retroativas de data não recalculam custos: o custo médio segue a ordem de registro."));
        Refresh();
    }
    private void Adjustment()
    {
        using var dialog = new Form { Text = "Ajuste de estoque", Size = new Size(650,530), StartPosition = FormStartPosition.CenterParent, Font = Font, MinimizeBox = false, MaximizeBox = false };
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false, Padding = new Padding(20) };
        dialog.Controls.Add(body);
        var product = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 570 };
        product.Items.AddRange(state.Products.Where(p => p.Active).Select(p => new Choice(p.Id,p.Name + " • " + Qty(p.Balance) + " " + p.Unit)).Cast<object>().ToArray());
        var type = Combo(["Ajuste +", "Ajuste −"]); type.Width = 570; var q = Number(1); q.Width = 570; q.Minimum = 1;
        var person = TextField(); person.Width = 570; var reason = TextField(); reason.Width = 570;
        foreach(var pair in new (string,Control)[] { ("Produto",product),("Tipo",type),("Quantidade na unidade de controle",q),("Responsável",person),("Motivo obrigatório",reason) }) { body.Controls.Add(Label(pair.Item1)); body.Controls.Add(pair.Item2); }
        body.Controls.Add(Button("Salvar ajuste",() => Guard(() =>
        {
            if(product.SelectedItem is not Choice ch) throw new ArgumentException("Selecione o produto.");
            var p = state.Products.Single(x => x.Id == ch.Id);
            Commit(s => InventoryRules.Apply(s,new Movement { ProductId = ch.Id, Type = type.Text, Quantity = q.Value, Factor = 1, Package = p.Unit, Person = person.Text.Trim(), Note = reason.Text.Trim() }));
            dialog.Close(); Navigate("Histórico");
        })));
        dialog.ShowDialog(this);
    }
    private void Reports(FlowLayoutPanel body)
    {
        Add(body,Label("Gastos, economia, consumo e posição do estoque • dados reais registrados neste computador."));
        var start = new DateTimePicker { Value = new DateTime(DateTime.Today.Year,DateTime.Today.Month,1), Format = DateTimePickerFormat.Short, Dock = DockStyle.Top };
        var end = new DateTimePicker { Value = DateTime.Today, Format = DateTimePickerFormat.Short, Dock = DockStyle.Top };
        var local = Combo(new[] { "Todos os locais" }.Concat(state.Movements.Where(m => m.Store.Length > 0).Select(m => m.Store).Distinct().Order()));
        var cat = Combo(new[] { "Todas as categorias" }.Concat(state.Products.Select(p => p.Category).Distinct().Order()));
        var product = Combo(new[] { "Todos os produtos" }.Concat(state.Products.Select(p => p.Name).Distinct().Order()));
        var supplier = Combo(new[] { "Todos os fornecedores" }.Concat(state.Movements.Select(m => m.Supplier).Where(x => x.Length > 0).Distinct().Order()));
        var sector = TextField(); var person = TextField();
        var f = Fields(); Field(f,"Data inicial da compra / saída",start); Field(f,"Data final da compra / saída",end); Field(f,"Local da compra",local); Field(f,"Categoria",cat); Field(f,"Produto",product); Field(f,"Fornecedor",supplier); Field(f,"Setor (consumo)",sector); Field(f,"Responsável",person); Add(body,f);
        var report = Combo(["Resumo", "Gastos", "Economia", "Consumo", "Estoque", "Preços", "Locais de compra"]); Add(body,report);
        tips.SetToolTip(local,"Filtra compras pelo estabelecimento. Não altera os dados de consumo ou estoque.");
        var results = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = false };
        results.Layout += (_,_) => { int h = results.GetPreferredSize(new Size(results.Width,0)).Height + 10; if(results.Height != h) results.Height = h; };
        DataTable exported = new(); string subtitle = "";
        void Refresh()
        {
            if(start.Value.Date > end.Value.Date) { MessageBox.Show(this,"A data inicial deve ser anterior à final."); return; }
            while(results.Controls.Count > 0) results.Controls[0].Dispose();
            bool ProductFilter(Movement m) => (cat.SelectedIndex == 0 || m.Category == cat.Text) && (product.SelectedIndex == 0 || m.ProductName == product.Text) && m.Person.Contains(person.Text,StringComparison.OrdinalIgnoreCase);
            var purchases = state.Movements.Where(m => m.Type == "Entrada" && m.PurchaseDate >= start.Value.Date && m.PurchaseDate < end.Value.Date.AddDays(1) && ProductFilter(m) && (local.SelectedIndex == 0 || m.Store == local.Text) && (supplier.SelectedIndex == 0 || m.Supplier == supplier.Text)).ToList();
            var outputs = state.Movements.Where(m => m.Type == "Saída" && m.Date >= start.Value.Date && m.Date < end.Value.Date.AddDays(1) && ProductFilter(m) && m.Sector.Contains(sector.Text,StringComparison.OrdinalIgnoreCase)).ToList();
            var products = state.Products.Where(p => p.Active && (cat.SelectedIndex == 0 || p.Category == cat.Text) && (product.SelectedIndex == 0 || p.Name == product.Text)).OrderBy(p => p.Name).ToList();
            decimal spent = purchases.Sum(m => m.Total ?? 0); var refs = purchases.Where(m => m.Total.HasValue && m.ReferenceTotal.HasValue).ToList(); decimal saving = refs.Sum(m => (m.ReferenceTotal - m.Total) ?? 0);
            var unknown = purchases.Count(m => !m.Total.HasValue);
            subtitle = $"{start.Value:dd/MM/yyyy} a {end.Value:dd/MM/yyyy} • {cat.Text} • {local.Text} • {product.Text} • {supplier.Text} • Responsável: {person.Text} • Setor: {sector.Text}";
            var purchasesTable = Table(["Data compra","Produto","Local","Fornecedor","Quantidade","Total","Preço / unidade","Referência total","Economia / adicional"],purchases.OrderByDescending(m => m.PurchaseDate).Select(m => new object?[] { m.PurchaseDate.ToString("dd/MM/yyyy"),m.ProductName,m.Store,m.Supplier,Qty(m.Converted) + " " + m.Unit,Money(m.Total),Money(m.Total / m.Converted),m.ReferenceTotal.HasValue ? Money(m.ReferenceTotal) : "Não informada",m.ReferenceTotal.HasValue && m.Total.HasValue ? Money(m.ReferenceTotal-m.Total) : "Não calculada" }));
            void AddGrid(string title, DataTable table)
            {
                Add(results,Label(title + "  ⓘ",14,true)); var g = Grid(280); g.DataSource = table; Add(results,g); exported = table;
            }
            void Bars(string title,IEnumerable<(string Name,decimal Value)> data)
            {
                Add(results,Label(title + "  ⓘ",14,true));
                var values = data.OrderByDescending(x => x.Value).Take(8).ToList();
                if(values.Count == 0) { Add(results,Label("Nenhum registro nos filtros selecionados.")); return; }
                var max = Math.Max(1,values.Max(x => Math.Abs(x.Value)));
                var panel = new TableLayoutPanel { ColumnCount = 3, RowCount = values.Count, Height = values.Count * 40, BackColor = Color.White, Padding = new Padding(12), ColumnStyles = { new ColumnStyle(SizeType.Percent,32),new ColumnStyle(SizeType.Percent,48),new ColumnStyle(SizeType.Percent,20) } };
                int row = 0;
                foreach(var v in values)
                {
                    panel.Controls.Add(Label(v.Name),0,row);
                    var track = new Panel { Dock = DockStyle.Fill, Margin = new Padding(5,10,10,10), BackColor = Color.FromArgb(236,239,245) };
                    var bar = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = row % 2 == 0 ? Navy : Gold };
                    track.Controls.Add(bar); track.SizeChanged += (_,_) => bar.Width = Math.Max(1,(int)(track.Width * Math.Abs(v.Value)/max));
                    panel.Controls.Add(track,1,row); panel.Controls.Add(Label(Money(v.Value),9,true),2,row); row++;
                }
                Add(results,panel);
            }
            switch(report.Text)
            {
                case "Resumo":
                    Add(results,Metrics(("Gasto conhecido",Money(spent),$"{unknown} itens com custo desconhecido excluídos."),("Economia líquida",refs.Count > 0 ? Money(saving) : "Não calculada",$"Referência menos valor pago. {refs.Count}/{purchases.Count} itens com referência."),("Estoque baixo",products.Count(p => p.Balance <= p.Minimum).ToString(),"Posição atual; sem filtro por período ou local da compra."),("Estoque: valor conhecido",Money(products.Sum(p => p.Balance * (p.AverageCost ?? 0))),"Pode ser parcial; produtos com custo desconhecido são excluídos.")));
                    Bars("Gastos ao longo do período",purchases.GroupBy(m => m.PurchaseDate.ToString("yyyy-MM")).Select(g => (g.Key,g.Sum(m => m.Total ?? 0))));
                    Bars("Gastos por categoria",purchases.GroupBy(m => m.Category).Select(g => (g.Key,g.Sum(m => m.Total ?? 0))));
                    Bars("Produtos com maior gasto",purchases.GroupBy(m => m.ProductName).Select(g => (g.Key,g.Sum(m => m.Total ?? 0))));
                    Bars("Gastos por local de compra",purchases.GroupBy(m => m.Store.Length > 0 ? m.Store : "Não informado").Select(g => (g.Key,g.Sum(m => m.Total ?? 0))));
                    exported = purchasesTable;
                    break;
                case "Gastos":
                    string variation = "Selecione um mês completo";
                    if(start.Value.Day == 1 && end.Value.Date == start.Value.Date.AddMonths(1).AddDays(-1))
                    {
                        var previous = state.Movements.Where(m => m.Type == "Entrada" && m.PurchaseDate >= start.Value.Date.AddMonths(-1) && m.PurchaseDate < start.Value.Date && ProductFilter(m) && (local.SelectedIndex == 0 || m.Store == local.Text) && (supplier.SelectedIndex == 0 || m.Supplier == supplier.Text)).ToList();
                        decimal prev = previous.Sum(m => m.Total ?? 0);
                        variation = unknown > 0 || previous.Any(m => !m.Total.HasValue) ? "Custos incompletos" : prev == 0 ? "Sem base percentual" : Money(spent-prev) + " (" + ((spent-prev)/prev).ToString("P1") + ")";
                    }
                    Add(results,Metrics(("Total conhecido",Money(spent),"Soma de compras com preço informado."),("Compras / itens",purchases.Select(m => m.PurchaseId).Distinct().Count() + " / " + purchases.Count,"Itens agrupados pela compra registrada."),("Variação mês anterior",variation,"Diferença de gasto não equivale à economia.")));
                    Bars("Gasto por mês",purchases.GroupBy(m => m.PurchaseDate.ToString("yyyy-MM")).Select(g => (g.Key,g.Sum(m => m.Total ?? 0)))); AddGrid("Compras detalhadas",purchasesTable); break;
                case "Economia":
                    Add(results,Metrics(("Referência total",Money(refs.Sum(m => m.ReferenceTotal ?? 0)),"Somente itens comparáveis."),("Valor pago comparável",Money(refs.Sum(m => m.Total ?? 0)),"Mesmas quantidades e unidades da referência."),("Economia líquida",refs.Count > 0 ? Money(saving) : "Não calculada","Valores negativos indicam custo adicional."),("Cobertura de referências",refs.Count + " / " + purchases.Count,"Compras sem referência não entram na economia.")));
                    AddGrid("Comparação por produto",Table(["Produto","Referência total","Valor pago","Economia / adicional"],refs.GroupBy(m => m.ProductName).Select(g => new object?[] { g.Key,Money(g.Sum(m => m.ReferenceTotal ?? 0)),Money(g.Sum(m => m.Total ?? 0)),Money(g.Sum(m => (m.ReferenceTotal-m.Total) ?? 0)) })));
                    AddGrid("Detalhamento das comparações",purchasesTable); break;
                case "Consumo":
                    Add(results,Metrics(("Custo conhecido das saídas",Money(outputs.Sum(m => (m.AppliedCost ?? 0)*m.Converted)),"Custo médio aplicado no momento do registro; pode ser parcial."),("Registros de saída",outputs.Count.ToString(),"Local de compra e fornecedor não filtram consumo."),("Setores atendidos",outputs.Select(m => m.Sector).Distinct().Count().ToString(),"Setores com saídas no período.")));
                    Bars("Custo de consumo por produto",outputs.GroupBy(m => m.ProductName).Select(g => (g.Key,g.Sum(m => (m.AppliedCost ?? 0)*m.Converted))));
                    Bars("Custo de consumo por setor",outputs.GroupBy(m => m.Sector).Select(g => (g.Key,g.Sum(m => (m.AppliedCost ?? 0)*m.Converted))));
                    AddGrid("Saídas detalhadas",Table(["Data","Produto","Quantidade","Setor","Responsável","Custo estimado"],outputs.Select(m => new object?[] { m.Date.ToString("dd/MM/yyyy HH:mm"),m.ProductName,Qty(m.Converted) + " " + m.Unit,m.Sector,m.Person,Money(m.AppliedCost*m.Converted) })));
                    Add(results,Notice($"{outputs.Count(m => !m.AppliedCost.HasValue)} saída(s) com custo desconhecido. O custo do consumo é diferente do gasto de compra.")); break;
                case "Estoque":
                    Add(results,Metrics(("Produtos ativos",products.Count.ToString(),"Categoria e produto selecionados."),("Estoque baixo",products.Count(p => p.Balance <= p.Minimum).ToString(),"Saldo igual ou abaixo do mínimo."),("Sem estoque",products.Count(p => p.Balance == 0).ToString(),"Saldo atual zerado.")));
                    AddGrid("Lista para compra",Table(["Produto","Saldo","Mínimo","Desejado","Sugestão em unidades","Sugestão de embalagem","Local"],products.Where(p => p.Balance <= p.Minimum).Select(p =>
                    {
                        decimal needed = Math.Max(0,p.Target-p.Balance); var pk = p.Packages.FirstOrDefault();
                        return new object?[] {p.Name,Qty(p.Balance) + " " + p.Unit,Qty(p.Minimum),Qty(p.Target),Qty(needed) + " " + p.Unit,pk == null ? "—" : Qty(decimal.Ceiling(needed/pk.Factor)) + " " + pk.Name + " (" + Qty(pk.Factor) + " por embalagem)",p.Location};
                    })));
                    AddGrid("Posição atual do estoque",Table(["Produto","Categoria","Saldo","Custo médio / unidade","Valor estimado","Última saída","Local"],products.Select(p => new object?[] {p.Name,p.Category,Qty(p.Balance)+" "+p.Unit,Money(p.AverageCost),p.Balance == 0 ? Money(0) : Money(p.Balance*p.AverageCost),state.Movements.Where(m => m.ProductId == p.Id && m.Type == "Saída").Select(m => (DateTime?)m.Date).Max()?.ToString("dd/MM/yyyy") ?? "Sem saída",p.Location})));
                    Add(results,Notice("Estoque é a posição atual: período, local de compra, fornecedor, setor e responsável não se aplicam. Valores sem custo conhecido aparecem identificados.")); break;
                case "Preços":
                    AddGrid("Histórico de preço por unidade",purchasesTable);
                    AddGrid("Menor, maior e último preço pago",Table(["Produto","Unidade","Menor preço","Maior preço","Último preço","Última compra","Local da última compra"],purchases.Where(m => m.Total.HasValue).GroupBy(m => new { m.ProductId,m.Unit }).Select(g => { var last = g.OrderByDescending(m => m.PurchaseDate).ThenByDescending(m => m.Date).First(); return new object?[] { last.ProductName,last.Unit,Money(g.Min(m => m.Total/m.Converted)),Money(g.Max(m => m.Total/m.Converted)),Money(last.Total/last.Converted),last.PurchaseDate.ToString("dd/MM/yyyy"),last.Store }; })));
                    break;
                case "Locais de compra":
                    Bars("Gasto por estabelecimento",purchases.GroupBy(m => m.Store.Length == 0 ? "Não informado" : m.Store).Select(g => (g.Key,g.Sum(m => m.Total ?? 0))));
                    AddGrid("Detalhamento por local",Table(["Local","Total conhecido","Participação","Compras","Itens","Produtos","Última compra"],purchases.GroupBy(m => m.Store.Length == 0 ? "Não informado" : m.Store).OrderByDescending(g => g.Sum(m => m.Total ?? 0)).Select(g => new object?[] {g.Key,Money(g.Sum(m => m.Total ?? 0)),spent == 0 ? "—" : (g.Sum(m => m.Total ?? 0)/spent).ToString("P1"),g.Select(m => m.PurchaseId).Distinct().Count(),g.Count(),string.Join(", ",g.Select(m => m.ProductName).Distinct()),g.Max(m => m.PurchaseDate).ToString("dd/MM/yyyy")}))); break;
            }
            Add(results,Notice($"Compras: {unknown} item(ns) sem preço. Valores financeiros somam apenas custos conhecidos. Economia usa {refs.Count} de {purchases.Count} itens com referência e preço. Ajustes não são compras nem consumo."));
            foreach(Control c in results.Controls) { c.Width = Math.Max(720,body.Width-8); if(c is Label l && l.Text.EndsWith("ⓘ")) tips.SetToolTip(l,"Os valores consideram os filtros aplicáveis acima. Custos desconhecidos são sinalizados e não são tratados como zero."); }
        }
        Add(body,Actions(Button("Aplicar filtros",Refresh),Button("Exportar tabela CSV",() => Export(exported),true),Button("Imprimir tabela / PDF",() => PrintTable(exported,"Relatório • " + report.Text + "\n" + subtitle),true)));
        Add(body,results); report.SelectedIndexChanged += (_,_) => Refresh();
        results.SizeChanged += (_,_) => { foreach(Control c in results.Controls) c.Width = Math.Max(720,body.Width-8); };
        Refresh();
    }
    private void Export(DataTable table)
    {
        Guard(() =>
        {
            using var save = new SaveFileDialog { Filter = "CSV para Excel (*.csv)|*.csv", FileName = "Estoque_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv" };
            if(save.ShowDialog(this) != DialogResult.OK) return;
            string Escape(string text)
            {
                if(text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
                return "\"" + text.Replace("\"","\"\"") + "\"";
            }
            var lines = new List<string> { string.Join(";",table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName))) };
            lines.AddRange(table.Rows.Cast<DataRow>().Select(r => string.Join(";",r.ItemArray.Select(v => Escape(Convert.ToString(v) ?? "")))));
            File.WriteAllLines(save.FileName,lines,new UTF8Encoding(true));
            MessageBox.Show(this,"Arquivo CSV salvo. Pode ser aberto no Excel.","Exportação");
        });
    }
    private void PrintTable(DataTable table,string title)
    {
        Guard(() =>
        {
            using var document = new PrintDocument(); document.DefaultPageSettings.Landscape = true;
            document.DefaultPageSettings.Margins = new Margins(35,35,40,40); int row = 0; int page = 0;
            using var font = new Font("Segoe UI",8); using var heading = new Font("Segoe UI",12,FontStyle.Bold);
            document.BeginPrint += (_,_) => { row = 0; page = 0; };
            document.PrintPage += (_,e) =>
            {
                if(e.Graphics == null) return; page++;
                float x = e.MarginBounds.Left, y = e.MarginBounds.Top;
                e.Graphics.DrawString("CAMPOS • " + title,heading,Brushes.Navy,new RectangleF(x,y,e.MarginBounds.Width,65)); y += 70;
                if(table.Columns.Count == 0) return;
                float width = (float)e.MarginBounds.Width/table.Columns.Count;
                var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
                for(int col = 0;col < table.Columns.Count;col++) e.Graphics.DrawString(table.Columns[col].ColumnName,font,Brushes.Navy,new RectangleF(x+col*width,y,width-5,40),format);
                y += 44;
                while(row < table.Rows.Count && y + 52 < e.MarginBounds.Bottom)
                {
                    for(int col = 0;col < table.Columns.Count;col++) e.Graphics.DrawString(Convert.ToString(table.Rows[row][col]),font,Brushes.Black,new RectangleF(x+col*width,y,width-5,47),format);
                    e.Graphics.DrawLine(Pens.LightGray,x,y+49,e.MarginBounds.Right,y+49); y += 52; row++;
                }
                e.Graphics.DrawString("Página " + page + " • " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"),font,Brushes.Gray,x,e.MarginBounds.Bottom+8); e.HasMorePages = row < table.Rows.Count;
            };
            using var dialog = new PrintDialog { Document = document, UseEXDialog = true };
            if(dialog.ShowDialog(this) == DialogResult.OK) document.Print();
        });
    }
    private void PrintLabel(Product p)
    {
        Guard(() =>
        {
            using var doc = new PrintDocument();
            doc.PrintPage += (_,e) =>
            {
                if(e.Graphics == null) return;
                using var title = new Font("Segoe UI",18,FontStyle.Bold); using var code = new Font("Consolas",16);
                e.Graphics.DrawString("CAMPOS • " + p.Name,title,Brushes.Navy,50,50);
                e.Graphics.DrawString(p.Code,code,Brushes.Black,50,100);
                e.Graphics.DrawString("Unidade: " + p.Unit + " • Local: " + p.Location,Font,Brushes.Black,50,140);
                // Code 128-B, checksum e zonas de silêncio para leitores convencionais.
                var patterns = BarcodePatterns.Split(' ');
                if(p.Code.Any(c => c < 32 || c > 126)) throw new ArgumentException("A etiqueta com código de barras aceita caracteres ASCII de 32 a 126.");
                var values = new List<int> {104}; int checksum = 104;
                for(int i = 0;i < p.Code.Length;i++) { int v = p.Code[i]-32; values.Add(v); checksum += v*(i+1); }
                values.Add(checksum%103); values.Add(106);
                int modules = values.Sum(v => patterns[v].Sum(c => c-'0')) + 20;
                float scale = Math.Min(2.2f,650f/modules), x = 50 + 10*scale;
                foreach(var value in values)
                {
                    bool black = true;
                    foreach(char c in patterns[value]) { float w = (c-'0')*scale; if(black) e.Graphics.FillRectangle(Brushes.Black,x,185,w,75); x += w; black = !black; }
                }
            };
            using var dialog = new PrintDialog { Document = doc, UseEXDialog = true }; if(dialog.ShowDialog(this) == DialogResult.OK) doc.Print();
        });
    }
    private const string BarcodePatterns = "212222 222122 222221 121223 121322 131222 122213 122312 132212 221213 221312 231212 112232 122132 122231 113222 123122 123221 223211 221132 221231 213212 223112 312131 311222 321122 321221 312212 322112 322211 212123 212321 232121 111323 131123 131321 112313 132113 132311 211313 231113 231311 112133 112331 132131 113123 113321 133121 313121 211331 231131 213113 213311 213131 311123 311321 331121 312113 312311 332111 314111 221411 431111 111224 111422 121124 121421 141122 141221 112214 112412 122114 122411 142112 142211 241211 221114 413111 241112 134111 111242 121142 121241 114212 124112 124211 411212 421112 421211 212141 214121 412121 111143 111341 131141 114113 114311 411113 411311 113141 114131 311141 411131 211412 211214 211232 2331112";
    private void Description(FlowLayoutPanel body)
    {
        Add(body,Notice("Sistema local Windows Forms • identidade Campos • banco SQLite no computador. O sistema começa vazio, sem registros fictícios."));
        var text = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, Height = 590, ScrollBars = ScrollBars.Vertical, Font = Font, Text =
            "COMO USAR\r\n\r\n1. Cadastre produtos com código, categoria, unidade, mínimo e estoque desejado. Inclua embalagens e seus códigos/fatores. Gere um código interno para materiais sem código.\r\n\r\n2. Registre o saldo inicial em Entrada. Se o custo não for conhecido, desmarque a opção de preço conhecido. O produto mantém custo desconhecido enquanto houver saldo anterior sem avaliação.\r\n\r\n3. Nas compras, informe local, datas, preço por embalagem e responsável. Para vários produtos da mesma nota, adicione os itens à lista e confirme a compra. Desconto e acréscimo são atribuídos a cada item; distribua valores gerais da nota sem duplicá-los.\r\n\r\n4. Em Saída, selecione o produto por nome ou leia o código. Confira a conversão antes de confirmar. O programa bloqueia retiradas maiores que o saldo. Leitores configurados como teclado podem enviar o código e Enter.\r\n\r\n5. O histórico preserva produto, embalagem, fator, valores e responsável da época. Correções são registradas como ajustes com motivo obrigatório. Produtos podem ser inativados preservando os registros. A unidade de controle de um produto já movimentado não pode mudar.\r\n\r\nCUSTOS E RELATÓRIOS\r\n\r\nO custo médio ponderado considera o saldo anterior e o valor de cada entrada. Saídas guardam o custo aplicado. Ajuste positivo torna o custo desconhecido por não ter valor de aquisição. Movimentações são avaliadas na ordem de registro; datas anteriores não refazem custos passados.\r\n\r\nEconomia = referência total − valor pago, para a mesma unidade e quantidade. Valores negativos indicam custo adicional. Sem referência, a economia não é calculada. Gasto de compra e custo de consumo são informações diferentes.\r\n\r\nCompras são filtradas pela data da compra; consumo pela data da saída. Estoque apresenta a posição atual. Custos desconhecidos são sinalizados e não entram nas somas financeiras.\r\n\r\nA exportação CSV abre no Excel. A impressão usa as impressoras do Windows; selecione Microsoft Print to PDF para salvar PDF. Etiquetas de produtos incluem código de barras Code 128.\r\n\r\nARMAZENAMENTO\r\n\r\nOs dados são salvos automaticamente e de forma atômica no banco SQLite. Use uma instância do sistema por computador. Para copiar o banco, feche o programa antes.\r\n\r\nBanco: " + InventoryStore.DataPath };
        Add(body,text);
    }
}



