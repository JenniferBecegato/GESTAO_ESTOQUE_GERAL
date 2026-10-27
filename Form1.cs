using System.Text;
using System.Text.Json;
using System.Diagnostics;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GESTAO_ESTOQUE_GERAL;

public partial class Form1 : Form
{
    private readonly InventoryStore store;
    private Inventory state;
    private readonly string? databasePath;
    internal readonly WebView2 Browser = new() { Dock = DockStyle.Fill };
    internal TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly JsonSerializerOptions json = new() { PropertyNameCaseInsensitive = true };
    private bool busy;
    private bool exportingPdf;
    public Form1(string? databasePath = null)
    {
        InitializeComponent();
        this.databasePath = databasePath;
        store = new InventoryStore(databasePath); state = store.Load();
        Text = "Campos | Gestão de Estoque Geral";
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1440,900);
        MinimumSize = new Size(850,650);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(Browser);
        KeyPreview = true;
        KeyDown += (_,e) => { if(e.KeyCode == Keys.F2) { e.Handled = true; Advanced(); } };
        Shown += async (_,_) => await InitializeBrowser();
        FormClosed += (_,_) => { Browser.Dispose(); store.Dispose(); };
    }
    internal static string Resource(string name)
    {
        using var stream = typeof(Form1).Assembly.GetManifestResourceStream("GESTAO_ESTOQUE_GERAL." + name) ?? throw new IOException("Recurso não encontrado: " + name);
        using var reader = new StreamReader(stream,Encoding.UTF8); return reader.ReadToEnd();
    }
    private async Task InitializeBrowser()
    {
        try
        {
            string profile = Path.Combine(Path.GetDirectoryName(databasePath ?? InventoryStore.DataPath)!, "InterfaceWebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null,profile);
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.CoreWebView2.Settings.IsZoomControlEnabled = false;
            Browser.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            Browser.CoreWebView2.NewWindowRequested += (_,e) => e.Handled = true;
            Browser.CoreWebView2.NavigationStarting += (_,e) => { if(e.Uri.StartsWith("http:",StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("https:",StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("file:",StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
            Browser.CoreWebView2.WebMessageReceived += Receive;
            LoadInterface();
        }
        catch(Exception ex)
        {
            Ready.TrySetException(ex);
            MessageBox.Show(this,"Não foi possível abrir a interface. Verifique o Microsoft Edge WebView2 Runtime.\n" + ex.Message,"Campos",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
    internal void LoadInterface() => Browser.NavigateToString(Resource("Prototipo.html").Replace("</body>","<script>" + Resource("Desktop.js") + "</script></body>",StringComparison.OrdinalIgnoreCase));
    private void Reply(string id, object? result = null, string? error = null) => Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { id, result, error }));
    private void Commit(Action<Inventory> action)
    {
        var next = JsonSerializer.Deserialize<Inventory>(JsonSerializer.Serialize(state))!;
        action(next); store.Save(next); state = next;
    }
    private async void Receive(object? sender,CoreWebView2WebMessageReceivedEventArgs args)
    {
        string id = "";
        try
        {
            using var document = JsonDocument.Parse(args.WebMessageAsJson);
            var root = document.RootElement; id = root.GetProperty("id").GetString()!;
            string type = root.GetProperty("type").GetString()!;
            var payload = root.TryGetProperty("payload",out var value) ? value : default;
            switch(type)
            {
                case "load": Reply(id,state); break;
                case "ready": Ready.TrySetResult(); Reply(id,true); break;
                case "generateCode": Reply(id,BarcodeService.Generate(state)); break;
                case "linkCode":
                    Guid linkProductId=payload.GetProperty("productId").GetGuid();
                    string linkCode=payload.GetProperty("code").GetString()??"";
                    Commit(s=>InventoryRules.LinkCode(s,linkProductId,linkCode));Reply(id,state);break;
                case "getLabelProfile":
                    var profileFile=Path.Combine(Path.GetDirectoryName(databasePath??InventoryStore.DataPath)!,"label-profile.json");
                    Reply(id,File.Exists(profileFile)?JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(profileFile)):null);break;
                case "saveLabelProfile":
                    if(payload.ValueKind!=JsonValueKind.Object||payload.GetRawText().Length>2048)throw new ArgumentException("Configuração inválida.");
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(databasePath??InventoryStore.DataPath)!,"label-profile.json"),payload.GetRawText());Reply(id,true);break;
                case "barcode":
                    var labelProduct=state.Products.SingleOrDefault(p=>p.Id==payload.GetGuid()) ?? throw new ArgumentException("Salve o produto antes de imprimir a etiqueta.");
                    Reply(id,new { id=labelProduct.Id,code=labelProduct.Code,name=labelProduct.Name,unit=labelProduct.Unit,location=labelProduct.Location,svg=BarcodeService.Svg(labelProduct.Code) }); break;
                case "packageBarcode":
                    Guid packageProductId=payload.GetProperty("productId").GetGuid();int packageIndex=payload.GetProperty("packageIndex").GetInt32();
                    Commit(s=>{
                        var p=s.Products.Single(x=>x.Id==packageProductId);
                        if(packageIndex<0||packageIndex>=p.Packages.Count)throw new ArgumentException("Embalagem não encontrada.");
                        if(string.IsNullOrWhiteSpace(p.Packages[packageIndex].Code))p.Packages[packageIndex].Code=BarcodeService.Generate(s);
                        InventoryRules.Validate(p,s);
                    });
                    var packageProduct=state.Products.Single(x=>x.Id==packageProductId);var labelPackage=packageProduct.Packages[packageIndex];
                    Reply(id,new { inventory=state,code=labelPackage.Code,name=packageProduct.Name,unit=labelPackage.Name+" com "+labelPackage.Factor.ToString("0.##")+" "+packageProduct.Unit,location=packageProduct.Location,svg=BarcodeService.Svg(labelPackage.Code) });break;
                case "saveProduct":
                    var product = payload.Deserialize<Product>(json) ?? throw new ArgumentException("Produto inválido.");
                    Commit(s =>
                    {
                        InventoryRules.Validate(product,s);
                        var old = s.Products.FirstOrDefault(p => p.Id == product.Id);
                        // Quantidades e custos só podem ser alterados por movimentações.
                        if(old != null) { product.Balance = old.Balance; product.AverageCost = old.AverageCost; }
                        else { product.Balance = 0; product.AverageCost = null; }
                        s.Products.RemoveAll(p => p.Id == product.Id); s.Products.Add(product);
                    });
                    Reply(id,state); break;
                case "move":
                    var movement = payload.Deserialize<Movement>(json) ?? throw new ArgumentException("Movimentação inválida.");
                    if(movement.Date.Kind == DateTimeKind.Utc) movement.Date = movement.Date.ToLocalTime();
                    if(movement.Date > DateTime.Now.AddMinutes(1)) throw new ArgumentException("A movimentação não pode ter data futura.");
                    if(movement.Type != "Entrada" && movement.Type != "Saída") throw new ArgumentException("Tipo de movimentação inválido.");
                    if(movement.Type == "Entrada")
                    {
                        if(movement.Total.HasValue && (string.IsNullOrWhiteSpace(movement.Store) || movement.PurchaseDate.Date > movement.Date.Date)) throw new ArgumentException("Informe o local e uma data de compra anterior ou igual ao recebimento.");
                        if(movement.ReferenceTotal < 0) throw new ArgumentException("A referência não pode ser negativa.");
                        movement.PurchaseId = Guid.NewGuid();
                    }
                    Commit(s => InventoryRules.Apply(s,movement)); Reply(id,state); break;
                case "advanced": Reply(id,true); Advanced(payload.ValueKind == JsonValueKind.String ? payload.GetString() : null); break;
                case "print":
                    if(exportingPdf) throw new InvalidOperationException("Aguarde a geração do PDF atual.");
                    exportingPdf=true;
                    try
                    {
                        bool labels=payload.ValueKind==JsonValueKind.Object&&payload.TryGetProperty("labels",out var labelFlag)&&labelFlag.GetBoolean();
                        using var save=new SaveFileDialog { Filter="Documento PDF (*.pdf)|*.pdf",DefaultExt="pdf",AddExtension=true,FileName=(labels?"Etiquetas_":"Relatorio_Estoque_")+DateTime.Now.ToString("yyyyMMdd_HHmm")+".pdf" };
                        if(save.ShowDialog(this)==DialogResult.OK)
                        {
                            await ExportPdf(save.FileName,labels,labels?payload.GetProperty("width").GetDouble():0,labels?payload.GetProperty("height").GetDouble():0,false);
                            try { Process.Start(new ProcessStartInfo(save.FileName){UseShellExecute=true}); }
                            catch { MessageBox.Show(this,"PDF salvo em:\n"+save.FileName,"PDF gerado"); }
                        }
                        Reply(id,true);
                    }
                    finally { exportingPdf=false; }
                    break;
                case "export": Export(payload); Reply(id,true); break;
                default: throw new ArgumentException("Operação não reconhecida.");
            }
        }
        catch(Exception ex) { Reply(id,null,ex.Message); }
        await Task.CompletedTask;
    }
    private void Advanced(string? screen = null)
    {
        if(busy) return;
        busy = true;
        try
        {
            Guid? requestedLabel=null;
            using var advanced = new LegacyForm(databasePath);
            advanced.LabelRequested=id=>requestedLabel=id;
            advanced.Text = "Campos | Ferramentas de gestão";
            if(screen != null) advanced.OpenScreen(screen);
            advanced.ShowDialog(this);
            state = store.Load();
            Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { inventory = state }));
            if(requestedLabel.HasValue)_=Browser.CoreWebView2.ExecuteScriptAsync("desktopTest.openLabel("+JsonSerializer.Serialize(requestedLabel.Value)+")");
        }
        finally { busy = false; }
    }
    internal async Task ExportPdf(string path,bool labels=false,double width=0,double height=0,bool? landscape=null)
    {
        bool horizontal=landscape??false;
        await Browser.CoreWebView2.ExecuteScriptAsync("document.body.classList.toggle('pdf-portrait',"+(!horizontal?"true":"false")+")");
        if(labels)await Browser.CoreWebView2.ExecuteScriptAsync("window.desktopOrientLabels?.("+(horizontal?"true":"false")+")");
        if(!labels)await Browser.CoreWebView2.ExecuteScriptAsync("desktopTest.preparePdf()");
        var settings=PrintSettings(labels,width,height,horizontal);
        try
        {
            if(!await Browser.CoreWebView2.PrintToPdfAsync(path,settings))throw new IOException("Não foi possível gerar o PDF. Escolha outro local e tente novamente.");
        }
        finally { await Browser.CoreWebView2.ExecuteScriptAsync("document.body.classList.remove('pdf-portrait');"+(!labels?"desktopTest.cleanupPdf()":"")); }
    }
    private CoreWebView2PrintSettings PrintSettings(bool labels,double width,double height,bool landscape)
    {
        if(labels&&!((Math.Abs(width-210)<0.01&&Math.Abs(height-297)<0.01)||(Math.Abs(width-297)<0.01&&Math.Abs(height-210)<0.01)))throw new ArgumentException("As etiquetas são geradas somente em folha A4.");
        var settings=Browser.CoreWebView2.Environment.CreatePrintSettings();
        settings.ShouldPrintHeaderAndFooter=false;
        settings.ShouldPrintBackgrounds=true;
        settings.ScaleFactor=1;
        settings.PageWidth=210/25.4;
        settings.PageHeight=297/25.4;
        settings.Orientation=landscape?CoreWebView2PrintOrientation.Landscape:CoreWebView2PrintOrientation.Portrait;
        settings.MarginTop=settings.MarginBottom=settings.MarginLeft=settings.MarginRight=labels?0:10/25.4;
        return settings;
    }
    private void Export(JsonElement payload)
    {
        using var dialog = new SaveFileDialog { FileName = "Estoque_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv", Filter = "CSV para Excel (*.csv)|*.csv" };
        if(dialog.ShowDialog(this) != DialogResult.OK) return;
        string Cell(string text)
        {
            if(text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
            return "\"" + text.Replace("\"","\"\"") + "\"";
        }
        var rows = payload.EnumerateArray().Select(row => string.Join(";",row.EnumerateArray().Select(v => Cell(v.GetString() ?? ""))));
        File.WriteAllLines(dialog.FileName,rows,new UTF8Encoding(true));
    }
}









