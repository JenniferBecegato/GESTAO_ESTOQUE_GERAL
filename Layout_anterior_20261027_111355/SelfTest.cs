namespace GESTAO_ESTOQUE_GERAL;
internal static class SelfTest
{
    public static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(),"campos-test-" + Guid.NewGuid().ToString("N") + ".db");
        int checks = 0;
        void Check(bool condition,string message) { if(!condition) throw new Exception(message); checks++; }
        try
        {
            var s = new Inventory(); var p = new Product { Code = "001", Name = "Papel", Unit = "pacote", Minimum = 2, Target = 8, Packages = [new Package { Name = "Fardo", Code = "002", Factor = 4 }] };
            InventoryRules.Validate(p,s); s.Products.Add(p);
            InventoryRules.Apply(s,new Movement { ProductId = p.Id, Type = "Entrada", Quantity = 3, Factor = 4, Total = 120, Person = "Teste" });
            Check(p.Balance == 12 && p.AverageCost == 10,"Conversão/custo inicial incorreto");
            InventoryRules.Apply(s,new Movement { ProductId = p.Id, Type = "Entrada", Quantity = 4, Factor = 1, Total = 80, Person = "Teste" });
            Check(p.Balance == 16 && p.AverageCost == 12.5m,"Custo ponderado incorreto");
            var output = new Movement { ProductId = p.Id, Type = "Saída", Quantity = 2, Factor = 1, Person = "Teste" }; InventoryRules.Apply(s,output);
            Check(p.Balance == 14 && output.AppliedCost == 12.5m,"Custo da saída incorreto");
            bool rejected = false;
            try { InventoryRules.Apply(s,new Movement { ProductId = p.Id, Type = "Saída", Quantity = 100, Factor = 1, Person = "Teste" }); } catch(ArgumentException) { rejected = true; }
            Check(rejected && p.Balance == 14,"Saída excessiva não bloqueada");
            rejected = false; try { InventoryRules.Validate(new Product { Code = "002", Name = "Duplicado", Unit = "un" },s); } catch(ArgumentException) { rejected = true; }
            Check(rejected,"Código de embalagem duplicado não bloqueado");
            InventoryRules.Apply(s,new Movement { ProductId = p.Id, Type = "Entrada", Quantity = 1, Factor = 1, Person = "Teste" }); Check(!p.AverageCost.HasValue,"Entrada sem valor deve preservar custo desconhecido");
            InventoryRules.Apply(s,new Movement { ProductId = p.Id, Type = "Entrada", Quantity = 1, Factor = 1, Total = 10, Person = "Teste" }); Check(!p.AverageCost.HasValue,"Saldo desconhecido não deve virar custo zero");
            rejected = false; try { InventoryRules.Apply(s,new Movement { ProductId = p.Id, Type = "Ajuste +", Quantity = 1, Factor = 1, Person = "Teste" }); } catch(ArgumentException) { rejected = true; } Check(rejected,"Ajuste sem motivo");
            using(var db = new InventoryStore(path)) { db.Save(s); }
            using(var db = new InventoryStore(path)) { var loaded = db.Load(); Check(loaded.Products.Single().Balance == 16 && loaded.Movements.Count == 5,"Persistência SQLite inconsistente"); }
                        ApplicationConfiguration.Initialize();
            using(var form = new Form1(path))
            {
                form.Show(); Application.DoEvents(); Check(form.Controls.Count > 0,"Formulário vazio");
                using(var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,form.ClientRectangle); bitmap.Save(Path.Combine(AppContext.BaseDirectory,"dashboard-qa.png")); }
                var sidebar = form.Controls.OfType<FlowLayoutPanel>().Single();
                foreach(var button in sidebar.Controls.OfType<Button>())
                {
                    button.PerformClick(); Application.DoEvents(); Check(form.Controls.Count > 0,"Falha na tela " + button.Text);
                    if(button.Text == "Relatórios")
                    {
                        var main = form.Controls.OfType<Panel>().First(x => x.Dock == DockStyle.Fill);
                        var body = main.Controls.OfType<FlowLayoutPanel>().Single();
                        var report = body.Controls.OfType<ComboBox>().Single();
                        for(int i = 0; i < report.Items.Count; i++) { report.SelectedIndex = i; Application.DoEvents(); checks++; }
                        report.SelectedIndex = 0;
                        using var image = new Bitmap(form.Width,form.Height); form.DrawToBitmap(image,form.ClientRectangle); image.Save(Path.Combine(AppContext.BaseDirectory,"reports-qa.png"));
                    }
                }
                form.Close();
            }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"self-test-result.txt"),$"OK: {checks} verificações. Conversão, custo médio, saldo, códigos únicos, custo desconhecido, ajustes, SQLite e abertura de tela.");
        }
        finally { if(File.Exists(path)) File.Delete(path); }
    }
}

