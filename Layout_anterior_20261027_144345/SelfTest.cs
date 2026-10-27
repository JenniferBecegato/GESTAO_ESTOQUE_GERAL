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
            await Script("window.scrollTo(0,0);document.querySelector('main').scrollTop=0"); await Task.Delay(100);
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
                form.Browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled=false;
                form.Browser.CoreWebView2.ScriptDialogOpening += (_,e) => { File.AppendAllText(Path.Combine(directory,"progresso.txt"),"Aviso: " + e.Message + "\n"); e.Accept(); };
                Check(await Script("produtos.length===0 && hist.length===0 && compras.length===0")=="true","Banco vazio sem dados fictícios");
                Check(await Script("document.querySelector('nav').getBoundingClientRect().width===220")=="true","Largura do menu original: 220px");
                await Capture("visao-geral");
                await Script("window.dashboardInventory=desktopTest.inventory;desktopTest.sync({...window.dashboardInventory,Products:Array.from({length:6},(_,i)=>({Id:'00000000-0000-0000-0000-'+String(i+1).padStart(12,'0'),Code:'TEST'+i,Name:'Produto baixo '+(i+1),Category:'Limpeza',Unit:'unidade',Balance:0,Minimum:1,Target:5,Location:'Estoque',Active:true,Packages:[],AverageCost:null}))});show(0)");
                Check(await Script("(()=>{const w=document.querySelector('.low-stock-scroll'),r=[...w.querySelectorAll('tr')];return w.scrollHeight>w.clientHeight&&r[4].getBoundingClientRect().bottom<=w.getBoundingClientRect().bottom+1&&r[5].getBoundingClientRect().bottom>w.getBoundingClientRect().bottom;})()")=="true","Estoque baixo: quatro produtos visíveis e rolagem a partir do quinto");
                Check(await Script("document.getElementById('dashboard-export-pdf').getBoundingClientRect().right>=document.querySelector('.dashboard-heading').getBoundingClientRect().right-1")=="true","Exportar PDF no canto superior direito da página principal");
                await form.ExportPdf(Path.Combine(directory,"pagina-principal.pdf"));
                await Script("desktopTest.sync(window.dashboardInventory);show(0)");
                await Script("window.savedPurchases=compras;compras=[{data:'2026-10-10',valor:100,ref:120,local:'Loja Outubro',unknown:false},{data:'2026-09-10',valor:50,ref:55,local:'Loja Setembro',unknown:false},{data:'2025-10-10',valor:30,ref:null,local:'Loja 2025',unknown:false}];show(0);document.getElementById('finance-month').value='10';document.getElementById('finance-year').value='2026';document.getElementById('finance-year').dispatchEvent(new Event('change'))");
                Check(await Script("[...document.querySelectorAll('#page .panel')].find(p=>p.querySelector('h2')?.textContent==='Resumo financeiro').textContent.includes('Loja Outubro')&&!document.querySelector('#page').textContent.includes('Loja Setembro')")=="true","Resumo financeiro filtra mês e ano selecionados");
                await Script("document.getElementById('finance-year').value='2025';document.getElementById('finance-year').dispatchEvent(new Event('change'))");
                Check(await Script("document.querySelector('#page').textContent.includes('Loja 2025')&&document.querySelector('#page').textContent.includes('Não calculada')")=="true","Mesmo mês de outro ano e compras sem referência");
                await Script("document.getElementById('finance-month').value='1';document.getElementById('finance-month').dispatchEvent(new Event('change'))");
                Check(await Script("document.querySelector('#page').textContent.includes('Sem dados')")=="true","Mês sem compras não traz dados de outros períodos");
                await Script("compras=window.savedPurchases;show(0);document.getElementById('finance-month').value=String(new Date().getMonth()+1);document.getElementById('finance-year').value=String(new Date().getFullYear());document.getElementById('finance-year').dispatchEvent(new Event('change'))");
                await Script("show(8)");
                Check(await Script("(()=>{const main=document.querySelector('main'),header=document.querySelector('.bar'),nav=document.querySelector('nav');const h=header.getBoundingClientRect().top,n=nav.getBoundingClientRect().top;main.scrollTop=600;return main.scrollTop===600&&window.scrollY===0&&header.getBoundingClientRect().top===h&&nav.getBoundingClientRect().top===n;})()")=="true","Somente o conteúdo rola; cabeçalho e menu permanecem fixos");
                await Script("document.querySelector('main').scrollTop=0");
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
                var generatedCodes=new HashSet<string>();
                for(int i=0;i<100;i++)
                {
                    string generated=BarcodeService.Generate(new Inventory());
                    Check(generated.Length==13&&generated.All(char.IsDigit)&&generated.Select((c,index)=>(c-'0')*(index%2==0?1:3)).Sum()%10==0&&generatedCodes.Add(generated),"Código interno único com dígito verificador válido "+i);
                }
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
                await Wait("document.activeElement===document.getElementById('codigo')");
                Check(await Script("document.getElementById('codigo').value===''")=="true","Leitor pronto para o próximo código após confirmar");
                foreach(int screen in new[]{3,4})
                {
                    await Script($"show({screen})");await Wait("document.activeElement===document.getElementById('codigo')");
                    await Script("document.getElementById('codigo').value='7891000000010';document.getElementById('codigo').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true}));");
                    Check(await Script("selected!==null && document.getElementById('formmov').classList.contains('hide')===false && document.activeElement===document.getElementById('q')")=="true","Código lido com Enter na tela "+screen);
                    await Script("document.getElementById('codigo').value='7891000000010';document.getElementById('codigo').dispatchEvent(new KeyboardEvent('keydown',{key:'Tab',bubbles:true,cancelable:true}));");
                    Check(await Script("selected!==null")=="true","Código lido com Tab na tela "+screen);
                    await Script("document.getElementById('codigo').value='7891000000010';document.getElementById('codigo').dispatchEvent(new Event('input',{bubbles:true}));");
                    await Wait("selected!==null");Check(true,"Leitor sem sufixo na tela "+screen);
                    await Script("document.getElementById('codigo').value='NAO-CADASTRADO';document.getElementById('codigo').dispatchEvent(new Event('input',{bubbles:true}));");
                    Check(await Script("selected===null && document.getElementById('formmov').classList.contains('hide')")=="true","Código alterado impede usar o produto anterior");
                }
                await Script("show(2);document.getElementById('generate-barcode').click()");
                await Wait("document.getElementById('code').value.length===13");
                await Script("window.internalTestCode=document.getElementById('code').value;document.getElementById('nome').value='Vassoura sem código';document.getElementById('un').value='unidade';cadastrar();");
                await Wait("produtos.length===2");
                Check(await Script("!document.getElementById('print-saved-label').hidden")=="true","Código gerado salvo e botão de etiqueta disponível");
                await Script("document.getElementById('print-saved-label').click()");
                await Wait("document.getElementById('label-dialog')?.open===true");
                await Wait("document.querySelector('#label-print-area img').complete");
                Check(await Script("document.querySelector('#label-print-area code').textContent===window.internalTestCode")=="true","Etiqueta contém o mesmo código salvo no produto");
                await Capture("etiqueta-codigo-interno");
                await Script("document.getElementById('label-count').value='3';document.getElementById('label-count').dispatchEvent(new Event('input'));document.getElementById('label-size').value='80,50';document.getElementById('label-size').dispatchEvent(new Event('change'))");
                Check(await Script("document.querySelectorAll('.barcode-label').length===3 && document.getElementById('label-paper').textContent.includes('210mm 297mm')")=="true","Quantidade e tamanho de impressão das etiquetas");
                await Script("document.body.classList.add('printing-labels')");
                await form.ExportPdf(Path.Combine(directory,"etiquetas.pdf"),true,210,297);
                await Script("document.body.classList.remove('printing-labels')");
                await Script("document.getElementById('label-close').click()");
                Check(await Script("!document.getElementById('label-paper')")=="true","Configuração de etiqueta não altera outras impressões");
                await Script("show(3);document.getElementById('codigo').value=window.internalTestCode;document.getElementById('codigo').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true}));");
                Check(await Script("selected.nome==='Vassoura sem código'")=="true","Código interno impresso é reconhecido na movimentação");
                var symbols=BarcodeService.Encode("ABC123");
                Check(symbols[0]==104&&symbols[^1]==106&&symbols[^2]==(104+symbols.Skip(1).Take(6).Select((v,i)=>v*(i+1)).Sum())%103,"Code 128: início, conteúdo, checksum e parada");
                await Script("window.boxReady=false;(()=>{const p=desktopTest.inventory.Products.find(x=>x.Code===window.internalTestCode);desktopTest.request('saveProduct',{...p,Packages:[{Name:'Caixa',Factor:50,Code:''}]}).then(s=>{desktopTest.sync(s);window.boxReady=true;});})()");
                await Wait("window.boxReady");
                await Script("show(3);document.getElementById('codigo').value=window.internalTestCode;buscar();document.getElementById('q').value='1';document.getElementById('tipoemb').value='50';document.getElementById('pessoa').value='Validação';document.getElementById('localcompra').value='Atacado';document.getElementById('preco').value='100';movimentar()");
                await Wait("produtos.find(p=>p.code===window.internalTestCode).saldo===50");
                await Script("document.getElementById('entry-label-action').click()");
                await Wait("document.getElementById('label-dialog')?.open===true");
                Check(await Script("document.getElementById('label-count').value==='50' && document.querySelectorAll('.barcode-label').length===50")=="true","Entrada de 1 caixa com 50 unidades sugere exatamente 50 etiquetas");
                await Script("document.getElementById('label-size').value='30,20';document.getElementById('label-size').dispatchEvent(new Event('change'));document.body.classList.add('printing-labels')");
                Check(await Script("document.querySelectorAll('.label-sheet').length===1 && document.querySelectorAll('.barcode-label').length===50 && !document.getElementById('label-media') && !document.getElementById('label-print-direct')")=="true","50 etiquetas compactas na mesma folha, somente A4 e PDF");
                await form.ExportPdf(Path.Combine(directory,"etiquetas-50-A4.pdf"),true,210,297);
                await Script("document.getElementById('label-close').click()");
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
                    string pdfPath=Path.Combine(directory,"relatorio-"+tab.Replace(' ','_')+".pdf");
                    await form.ExportPdf(pdfPath);
                    Check(File.Exists(pdfPath)&&new FileInfo(pdfPath).Length>1000,"PDF gerado diretamente: "+tab);
                }
                await Script("desktopTest.preparePdf()");
                Check(await Script("document.querySelectorAll('#all-reports-pdf .pdf-report-section').length===7 && abaRel==='Locais de compra'")=="true","PDF reúne as sete abas e preserva a aba selecionada");
                await form.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia","{\"media\":\"print\"}");
                Check(await Script("getComputedStyle(document.querySelector('nav')).display==='none' && getComputedStyle(document.querySelector('.badge-top')).display==='none' && getComputedStyle(document.querySelector('.report-tabs')).display==='none' && getComputedStyle(document.querySelector('.report-filter-panel')).display==='none' && getComputedStyle(document.querySelector('.pdf-filter-summary')).display==='block'")=="true","PDF sem menu, aviso do computador, botões e controles de filtro");
                await form.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia","{\"media\":\"\"}");
                await Script("desktopTest.cleanupPdf();categoriaRel='Copa';localRel='Atacado';inicioRel='2026-10-01';fimRel='2026-10-31';desenhaRelatorio();desktopTest.preparePdf()");
                Check(await Script("[...document.querySelectorAll('#all-reports-pdf .pdf-filter-summary')].every(x=>x.textContent.includes('Copa')&&x.textContent.includes('Atacado')&&x.textContent.includes('01/10/2026')) && [...document.querySelectorAll('#all-reports-pdf .pdf-report-section')].every(x=>!x.textContent.includes('Papel higiênico'))")=="true","Mesma categoria, local e período aplicados em todas as abas do PDF");
                await form.ExportPdf(Path.Combine(directory,"relatorio-completo-filtrado.pdf"));
                await Script("categoriaRel='';localRel='';desenhaRelatorio()");
                using(var db = new InventoryStore(dbPath)) Check(db.Load().Products.Single(p=>p.Code=="7891000000010").Balance==10 && db.Load().Movements.Count==3,"Reabertura do SQLite preserva saldo e histórico");
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
                File.WriteAllText(Path.Combine(directory,"resultado.txt"),$"APROVADO: {checks} verificações. Descrição e CSS comparados diretamente ao protótipo; cadastro, entrada, saída, estoque, SQLite e sete relatórios validados.");
            }
            catch(Exception ex) { Environment.ExitCode=1;File.WriteAllText(Path.Combine(directory,"erro.txt"),ex.ToString()); }
            finally { form.Close(); }
        };
        form.Show();
        Application.Run(form);
    }
}







