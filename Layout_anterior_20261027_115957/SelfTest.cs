using System.Text.Json;
using Microsoft.Web.WebView2.Core;
namespace GESTAO_ESTOQUE_GERAL;
internal static class SelfTest
{
    public static void Run()
    {
        ApplicationConfiguration.Initialize();
        string directory = Path.Combine(AppContext.BaseDirectory,"validacao-layout"); Directory.CreateDirectory(directory);
        foreach(string artifact in new[]{"erro.txt","resultado.txt"}) File.Delete(Path.Combine(directory,artifact));
        string dbPath = Path.Combine(directory,"teste-"+Guid.NewGuid().ToString("N")+".db");
        using var form = new Form1(dbPath);
        int checks = 0;
        void Check(bool condition,string name)
        {
            if(!condition)throw new Exception(name);
            checks++; File.AppendAllText(Path.Combine(directory,"progresso.txt"),$"OK {checks}: {name}\n");
        }
        async Task<string> Script(string script) => await form.Browser.CoreWebView2.ExecuteScriptAsync(script);
        async Task Wait(string expression)
        {
            for(int i=0;i<300;i++) { if(await Script(expression)=="true")return; await Task.Delay(50); }
            throw new TimeoutException("Interface não respondeu: "+expression);
        }
        async Task Capture(string name)
        {
            await Script("window.scrollTo(0,0)"); await Task.Delay(100);
            using var file = File.Create(Path.Combine(directory,name+".png"));
            await form.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
        }
        File.WriteAllText(Path.Combine(directory,"progresso.txt"),"Iniciando interface\n");
        form.Shown += async (_,_) =>
        {
            try
            {
                File.AppendAllText(Path.Combine(directory,"progresso.txt"),"Janela criada; aguardando WebView2\n");
                await form.Ready.Task.WaitAsync(TimeSpan.FromSeconds(40));
                form.Browser.CoreWebView2.ScriptDialogOpening += (_,e) => { File.AppendAllText(Path.Combine(directory,"progresso.txt"),"Aviso: " + e.Message + "\n"); e.Accept(); };
                Check(await Script("produtos.length===0 && hist.length===0 && compras.length===0")=="true","Banco vazio sem dados fictícios");
                Check(await Script("document.querySelector('nav').getBoundingClientRect().width===220")=="true","Largura do menu original: 220px");
                await Capture("visao-geral");
                await Script("show(8)");
                string actualDescription = await Script("document.querySelector('#page').innerHTML");
                string actualStructure = await Script("JSON.stringify([...document.querySelectorAll('style:not(#desktop-layout)')].map(x=>x.textContent))");
                Check(await Script("document.querySelectorAll('#page .content h2').length===15")=="true","Descrição integral com 15 tópicos");
                Check(await Script("!document.querySelector('#page .content > .notice')")=="true","Aviso marcado removido da descrição");
                await Capture("descricao");
                await Script("show(1)");
                Check(await Script("(()=>{const b=document.querySelector('.products-heading button'),h=document.querySelector('.products-heading'),r=b.getBoundingClientRect(),p=h.getBoundingClientRect();return r.right>=p.right-1&&r.top<p.top+40;})()")=="true","Botão Cadastrar produto no canto superior direito do cabeçalho");
                await Capture("produtos");
                foreach(var size in new[]{new Size(1440,900),new Size(1440,838),new Size(1280,720),new Size(1024,650)})
                {
                    form.ClientSize=size;await Task.Delay(100);await Script("show(2);window.scrollTo(0,0)");
                    Check(await Script("document.documentElement.scrollHeight<=innerHeight && document.documentElement.scrollWidth<=innerWidth && document.querySelector('.registration-panel button.action').getBoundingClientRect().bottom<=innerHeight")=="true","Cadastro inteiro sem scroll em "+size.Width+"x"+size.Height);
                    await Capture("cadastro-"+size.Width+"x"+size.Height);
                }
                form.ClientSize=new Size(1440,900);await Task.Delay(100);
                // Exercita o cadastro pelo formulário original, a conversão, persistência e saída.
                await Script("show(2);document.getElementById('code').value='7891000000010';document.getElementById('nome').value='Papel higiênico';document.getElementById('un').value='pacote';document.getElementById('min').value='5';document.getElementById('emb').value='Fardo';document.getElementById('factor').value='4';document.getElementById('reference-price').value='12';cadastrar();");
                await Wait("produtos.length===1");
                Check(await Script("produtos[0].saldo===0 && produtos[0].factor===4")=="true","Cadastro pelo HTML original e fator de embalagem");
                await Script("show(3);document.getElementById('codigo').value='7891000000010';buscar();document.getElementById('q').value='3';document.getElementById('tipoemb').value='4';document.getElementById('pessoa').value='Validação';document.getElementById('localcompra').value='Atacado';document.getElementById('preco').value='40';movimentar();");
                await Wait("produtos[0].saldo===12");
                Check(await Script("compras.length===1 && compras[0].valor===120 && compras[0].quantidade===12")=="true","Entrada pelo formulário: 3 fardos equivalem a 12 pacotes / R$ 120");
                await Script("show(4);document.getElementById('codigo').value='7891000000010';buscar();document.getElementById('q').value='2';document.getElementById('pessoa').value='Validação';document.getElementById('setor').value='Banheiro';movimentar();");
                await Wait("produtos[0].saldo===10");
                Check(await Script("hist.length===2 && saidasDemo[0].custo===20")=="true","Saída salva com custo médio preservado");
                await Script("window.testReject='pending';desktopTest.request('move',{ProductId:desktopTest.inventory.Products[0].Id,Type:'Saída',Quantity:100,Factor:1,Person:'Teste'}).then(()=>window.testReject='failed').catch(()=>window.testReject='blocked');");
                await Wait("window.testReject!=='pending'");
                Check(await Script("window.testReject==='blocked' && produtos[0].saldo===10")=="true","Bloqueio de saída acima do saldo sem alterar os dados");
                for(int i=0;i<9;i++)
                {
                    if(i==7)continue;
                    await Script($"show({i})");
                    Check(await Script("document.querySelector('#page h1')!==null")=="true","Tela "+i+" carregada");
                    if(i==2)await Capture("cadastro");
                }
                await Script("show(6)");
                foreach(string tab in new[]{"Resumo","Gastos","Economia","Consumo","Estoque","Preços","Locais de compra"})
                {
                    await Script("selecionaAba("+JsonSerializer.Serialize(tab)+")");
                    Check(await Script("document.querySelectorAll('.report-tabs button').length===7 && document.querySelector('#relconteudo').children.length>0")=="true","Aba de relatório: "+tab);
                    await Capture("relatorio-"+Array.IndexOf(new[]{"Resumo","Gastos","Economia","Consumo","Estoque","Preços","Locais de compra"},tab));
                }
                using(var db = new InventoryStore(dbPath)) Check(db.Load().Products.Single().Balance==10 && db.Load().Movements.Count==2,"Reabertura do SQLite preserva saldo e histórico");
                // Compara diretamente com o arquivo recebido, no mesmo motor e viewport.
                var navigated = new TaskCompletionSource();
                void Loaded(object? s,CoreWebView2NavigationCompletedEventArgs e) => navigated.TrySetResult();
                form.Browser.CoreWebView2.NavigationCompleted += Loaded;
                form.Browser.NavigateToString(Form1.Resource("Prototipo.html")); await navigated.Task.WaitAsync(TimeSpan.FromSeconds(20));
                form.Browser.CoreWebView2.NavigationCompleted -= Loaded;
                await Script("show(8);document.querySelector('#page .content > .notice').remove()");
                Check(await Script("document.querySelector('#page').innerHTML")==actualDescription,"Descrição original preservada, com remoção somente do aviso solicitado");
                Check(await Script("JSON.stringify([...document.querySelectorAll('style')].map(x=>x.textContent))")==actualStructure,"Todos os estilos CSS idênticos ao protótipo original");
                await Capture("descricao-prototipo-original");
                using(var actual = new Bitmap(Path.Combine(directory,"descricao.png")))
                using(var expected = new Bitmap(Path.Combine(directory,"descricao-prototipo-original.png")))
                {
                    int differences=0;
                    for(int y=70;y<actual.Height;y++) for(int x=0;x<actual.Width;x++) if(actual.GetPixel(x,y)!=expected.GetPixel(x,y))differences++;
                    Check(differences==0,"Descrição visualmente igual à referência após remover o aviso solicitado");
                }
                File.WriteAllText(Path.Combine(directory,"resultado.txt"),$"APROVADO: {checks} verificações. Descrição e CSS comparados diretamente ao protótipo; cadastro, entrada, saída, estoque, SQLite e sete relatórios validados.");
            }
            catch(Exception ex) { Environment.ExitCode=1;File.WriteAllText(Path.Combine(directory,"erro.txt"),ex.ToString()); }
            finally { form.Close(); }
        };
        form.Show();
        Application.Run(form);
    }
}



