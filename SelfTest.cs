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
                await Script("window.addEventListener('error',e=>window.lastScriptError=e.message)");
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
                await Script("window.savedPurchases=compras;compras=[{data:'2026-10-10',valor:100,ref:120,local:'Loja Outubro',unknown:false},{data:'2026-09-10',valor:50,ref:55,local:'Loja Setembro',unknown:false},{data:'2025-10-10',valor:30,ref:null,local:'Loja 2025',unknown:false}];show(0);document.getElementById('finance-period').value='2026-10';document.getElementById('finance-period').dispatchEvent(new Event('change'))");
                Check(await Script("[...document.querySelectorAll('#page .panel')].find(p=>p.querySelector('h2')?.textContent==='Resumo financeiro').textContent.includes('Loja Outubro')&&!document.querySelector('#page').textContent.includes('Loja Setembro')")=="true","Resumo financeiro filtra mês e ano selecionados");
                await Script("document.getElementById('finance-period').value='2025-10';document.getElementById('finance-period').dispatchEvent(new Event('change'))");
                Check(await Script("document.querySelector('#page').textContent.includes('Loja 2025')&&document.querySelector('#page').textContent.includes('Não calculada')")=="true","Mesmo mês de outro ano e compras sem referência");
                await Script("document.getElementById('finance-period').value='2025-01';document.getElementById('finance-period').dispatchEvent(new Event('change'))");
                Check(await Script("document.querySelector('#page').textContent.includes('Sem dados')")=="true","Mês sem compras não traz dados de outros períodos");
                await Script("compras=window.savedPurchases;show(0);document.getElementById('finance-period').value=new Date().getFullYear()+'-'+String(new Date().getMonth()+1).padStart(2,'0');document.getElementById('finance-period').dispatchEvent(new Event('change'))");
                await Script("show(8)");
                Check(await Script("(()=>{const main=document.querySelector('main'),header=document.querySelector('.bar'),nav=document.querySelector('nav');const h=header.getBoundingClientRect().top,n=nav.getBoundingClientRect().top;main.scrollTop=600;return main.scrollTop===600&&window.scrollY===0&&header.getBoundingClientRect().top===h&&nav.getBoundingClientRect().top===n;})()")=="true","Somente o conteúdo rola; cabeçalho e menu permanecem fixos");
                await Script("document.querySelector('main').scrollTop=0");
                string actualDescription = await Script("(()=>{const p=document.querySelector('#page').cloneNode(true);p.querySelectorAll('button').forEach(b=>b.remove());return p.textContent;})()");
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
                await Script("show(2);document.getElementById('code').value='7891000000010';document.getElementById('nome').value='Papel higiênico';document.getElementById('un').value='pacote';document.getElementById('min').value='5';document.getElementById('emb').value='Fardo';document.getElementById('factor').value='4';cadastrar();");
                await Wait("produtos.length===1");
                Check(await Script("produtos[0].saldo===0 && produtos[0].factor===4")=="true","Cadastro pelo HTML original e fator de embalagem");
                await Script("show(3);document.getElementById('codigo').value='7891000000010';buscar();document.getElementById('q').value='3';document.getElementById('tipoemb').value='4';document.getElementById('pessoa').value='Validação';document.getElementById('localcompra').value='Atacado';document.getElementById('preco').value='120';movimentar();");
                await Wait("produtos[0].saldo===12");
                Check(await Script("!document.getElementById('ref') && !document.getElementById('setor') && !document.getElementById('obs') && !document.getElementById('entry-label-action') && !document.getElementById('label-dialog')?.open")=="true","Entrada sem referência, setor, observação ou geração de etiquetas");
                Check(await Script("compras.length===1 && compras[0].valor===120 && compras[0].quantidade===12")=="true","Entrada pelo formulário: 3 fardos equivalem a 12 pacotes / R$ 120");
                await Script("show(4);document.getElementById('codigo').value='7891000000010';buscar();document.getElementById('q').value='2';document.getElementById('pessoa').value='Validação';document.getElementById('setor').value='Banheiro';movimentar();");
                await Wait("produtos[0].saldo===10");
                Check(await Script("hist.length===2 && saidasDemo[0].custo===20")=="true","Saída salva com custo médio preservado");
                await Script("show(3);document.getElementById('codigo').value='456';buscar();document.getElementById('link-existing-product').click()");
                await Wait("document.getElementById('link-code-dialog')?.open===true");
                Check(await Script("document.getElementById('link-product').options.length===1 && document.getElementById('link-save').disabled")=="true","Código não encontrado oferece lista de produtos sem selecionar automaticamente");
                await Capture("vincular-codigo");
                await Script("document.getElementById('link-product-search').value='Papel';document.getElementById('link-product-search').dispatchEvent(new Event('input'));document.getElementById('link-product').value=desktopTest.inventory.Products[0].Id;document.getElementById('link-product').dispatchEvent(new Event('change'));document.getElementById('link-save').click()");
                await Wait("desktopTest.inventory.Products[0].CodeAliases?.includes('456')===true");
                Check(await Script("selected?.code==='7891000000010' && selected.saldo===10 && hist.length===2")=="true","Salvar vínculo reconhece o novo código sem mudar saldo ou histórico");
                await Script("document.getElementById('codigo').value='7891000000010';buscar()");
                Check(await Script("selected?.saldo===10")=="true","Código antigo continua reconhecendo o mesmo produto");
                await Script("document.getElementById('codigo').value='';document.getElementById('codigo').focus()");
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
                await Script("window.duplicateLink='pending';desktopTest.request('linkCode',{productId:desktopTest.inventory.Products[1].Id,code:'456'}).then(()=>window.duplicateLink='accepted').catch(()=>window.duplicateLink='blocked')");
                await Wait("window.duplicateLink!=='pending'");
                Check(await Script("window.duplicateLink==='blocked'")=="true","Mesmo código não pode pertencer a dois produtos");
                var duplicateProduct=new Product { Code="456",Name="Duplicado",Unit="unidade" };
                bool aliasCollision=false;using(var dbAlias=new InventoryStore(dbPath)){try{InventoryRules.Validate(duplicateProduct,dbAlias.Load());}catch(ArgumentException){aliasCollision=true;}}
                Check(aliasCollision,"Novo cadastro não pode reutilizar um código já vinculado");
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
                await Script("desktopTest.openLabel(desktopTest.inventory.Products.find(p=>p.Code===window.internalTestCode).Id)");
                await Wait("document.getElementById('label-dialog')?.open===true");
                await Wait("document.getElementById('label-print').disabled===false");
                Check(await Script("document.getElementById('label-count').value==='1' && document.querySelectorAll('.barcode-label').length===1 && document.getElementById('label-target').value==='0'")=="true","Uma caixa recebida sugere uma etiqueta de embalagem");
                await Script("document.getElementById('label-target').value='unit';document.getElementById('label-target').dispatchEvent(new Event('change'))");
                Check(await Script("document.getElementById('label-count').value==='50'")=="true","Escolher produtos avulsos gera 50 etiquetas de unidade");
                await Script("document.getElementById('label-size').value='30,20';document.getElementById('label-size').dispatchEvent(new Event('change'));document.body.classList.add('printing-labels')");
                Check(await Script("document.querySelectorAll('.label-sheet').length===1 && document.querySelectorAll('.barcode-label').length===50 && !document.getElementById('label-media') && !document.getElementById('label-print-direct')")=="true","50 etiquetas compactas na mesma folha, somente A4 e PDF");
                await form.ExportPdf(Path.Combine(directory,"etiquetas-50-A4.pdf"),true,210,297);
                await form.ExportPdf(Path.Combine(directory,"etiquetas-50-A4-paisagem.pdf"),true,210,297,true);
                await Script("document.getElementById('label-close').click()");
                await Script("window.packReady=false;(()=>{const p=desktopTest.inventory.Products.find(x=>x.Code===window.internalTestCode);desktopTest.request('saveProduct',{...p,Packages:[...p.Packages,{Name:'Pacote',Factor:3,Code:''}]}).then(s=>{desktopTest.sync(s);window.packReady=true;});})()");
                await Wait("window.packReady");
                await Script("desktopTest.openLabel(desktopTest.inventory.Products.find(p=>p.Code===window.internalTestCode).Id,null,'5 pacotes com 3 unidades',{Quantity:5,Factor:3,Converted:15})");
                await Wait("document.getElementById('label-dialog')?.open===true && document.getElementById('label-print').disabled===false");
                Check(await Script("document.getElementById('label-count').value==='5' && document.querySelectorAll('.barcode-label').length===5 && new Set([...document.querySelectorAll('.barcode-label code')].map(c=>c.textContent)).size===1 && document.querySelector('.barcode-label code').textContent===window.internalTestCode")=="true","5 pacotes com 3 unidades geram 5 etiquetas iguais com o código do produto");
                await Script("window.packCode=document.querySelector('.barcode-label code').textContent;document.body.classList.add('printing-labels')");
                await form.ExportPdf(Path.Combine(directory,"etiquetas-5-pacotes.pdf"),true,210,297);
                await Script("document.getElementById('label-target').value='unit';document.getElementById('label-target').dispatchEvent(new Event('change'))");
                Check(await Script("document.getElementById('label-count').value==='15' && document.querySelector('.barcode-label code').textContent===window.internalTestCode")=="true","Alternativa avulsa: 15 etiquetas com o código da unidade");
                await Script("document.getElementById('label-close').click();show(4);document.getElementById('codigo').value=window.packCode;buscar()");
                Check(await Script("selected?.code===window.internalTestCode && document.getElementById('tipoemb').value==='1'")=="true","Código compartilhado identifica o produto; embalagem é selecionada na movimentação");
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
                Check(await Script("document.querySelectorAll('#all-reports-pdf .pdf-report-section').length===7 && abaRel==='Locais de compra'")=="true","PDF reúne as sete abas e preserva a aba selecionada: "+await Script("JSON.stringify({current,abaRel,count:document.querySelectorAll('#all-reports-pdf .pdf-report-section').length,error:window.lastScriptError})"));
                await form.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia","{\"media\":\"print\"}");
                Check(await Script("getComputedStyle(document.querySelector('nav')).display==='none' && getComputedStyle(document.querySelector('.badge-top')).display==='none' && getComputedStyle(document.querySelector('.report-tabs')).display==='none' && getComputedStyle(document.querySelector('.report-filter-panel')).display==='none' && getComputedStyle(document.querySelector('.pdf-filter-summary')).display==='block'")=="true","PDF sem menu, aviso do computador, botões e controles de filtro");
                await form.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia","{\"media\":\"\"}");
                await Script("desktopTest.cleanupPdf();categoriaRel='Copa';localRel='Atacado';inicioRel='2026-10-01';fimRel='2026-10-31';desenhaRelatorio();desktopTest.preparePdf()");
                Check(await Script("[...document.querySelectorAll('#all-reports-pdf .pdf-filter-summary')].every(x=>x.textContent.includes('Copa')&&x.textContent.includes('Atacado')&&x.textContent.includes('01/10/2026')) && [...document.querySelectorAll('#all-reports-pdf .pdf-report-section')].every(x=>!x.textContent.includes('Papel higiênico'))")=="true","Mesma categoria, local e período aplicados em todas as abas do PDF");
                await form.ExportPdf(Path.Combine(directory,"relatorio-completo-filtrado.pdf"));
                await Script("categoriaRel='';localRel='';desenhaRelatorio()");
                await Script("show(5);document.getElementById('purchases-product').value='Papel';document.getElementById('purchases-product').dispatchEvent(new Event('input'))");
                Check(await Script("document.querySelectorAll('#purchases-history-table tbody tr').length===1 && document.getElementById('purchases-history-table').textContent.includes('Papel higiênico') && !document.getElementById('purchases-history-table').textContent.includes('Vassoura')")=="true","Histórico de compras filtra o produto sem alterar as movimentações");
                await form.ExportPdf(Path.Combine(directory,"historico-compras-filtrado.pdf"));
                await Script("document.getElementById('purchases-start').value='2070-01-01';document.getElementById('purchases-start').dispatchEvent(new Event('input'))");
                Check(await Script("document.getElementById('purchases-history-table').textContent.includes('Nenhuma compra encontrada')")=="true","Histórico de compras respeita o período");
                await Script("document.getElementById('purchases-clear').click()");
                Check(await Script("document.querySelectorAll('#purchases-history-table tbody tr').length===2")=="true","Limpar filtros restaura as compras registradas");
                foreach(int screen in new[]{0,5,8,6})
                {
                    await Script($"show({screen})");
                    if(screen==5||screen==8)Check(await Script("!!document.getElementById('page-export-pdf')")=="true","Botão Exportar PDF na tela "+screen);
                    foreach(bool horizontal in new[]{false,true})
                    {
                        string target=Path.Combine(directory,$"tela-{screen}-"+(horizontal?"paisagem":"retrato")+".pdf");
                        await form.ExportPdf(target,landscape:horizontal);
                        Check(File.Exists(target)&&new FileInfo(target).Length>1000,"A4 "+(horizontal?"paisagem":"retrato")+" na tela "+screen);
                    }
                }
                using(var db = new InventoryStore(dbPath)) Check(db.Load().Products.Single(p=>p.Code=="7891000000010").Balance==10 && db.Load().Products.Single(p=>p.Code=="7891000000010").CodeAliases.Contains("456") && db.Load().Movements.Count==3,"Reabertura do SQLite preserva códigos vinculados, saldo e histórico");
                await Script("show(1);window.editTestId=desktopTest.inventory.Products.find(p=>p.Code==='7891000000010').Id;document.querySelector('[data-edit-product=\"'+window.editTestId+'\"]').click()");
                Check(await Script("document.getElementById('code').value==='7891000000010' && document.querySelector('#page h1').textContent==='Editar produto'")=="true","Lápis abre cadastro preenchido");
                await Script("document.getElementById('nome').value='Produto editado';document.getElementById('local').value='Armário editado';document.getElementById('emb').value='Caixa';document.getElementById('factor').value='100';document.getElementById('min').value='1';cadastrar()");
                await Wait("current===1");
                Check(await Script("(()=>{const p=desktopTest.inventory.Products.find(p=>p.Id===window.editTestId);return p.Name==='Produto editado' && p.Location==='Armário editado' && p.Balance===10 && p.CodeAliases.includes('456');})()")=="true","Edição mantém identidade, saldo e códigos vinculados");
                await Script("document.querySelector('[data-edit-product=\"'+window.editTestId+'\"]').click()");
                Check(await Script("!document.getElementById('purchase-total') && !document.getElementById('purchase-quantity') && !document.getElementById('purchase-unit-cost')")=="true","Cadastro contém definições de controle, sem dados de compra");
                Check(await Script("!document.getElementById('reference-price') && document.getElementById('min').value==='1' && desktopTest.inventory.Products.find(p=>p.Id===window.editTestId).Minimum===100 && document.getElementById('minimum-conversion').textContent.includes('100')")=="true","Sem campo de referência; mínimo de 1 caixa equivale a 100 unidades e reabre como 1 caixa");
                await Script("document.getElementById('emb').value='';document.getElementById('emb').dispatchEvent(new Event('input'))");
                await Script("document.getElementById('min').value='3';cadastrar()");
                await Wait("current===1");
                Check(await Script("(()=>{const p=desktopTest.inventory.Products.find(p=>p.Id===window.editTestId);return p.Minimum===3 && p.MinimumPackageQuantity===null;})()")=="true","Sem conversão o mínimo é contado na unidade de controle");
                await Script("show(3);document.getElementById('codigo').value='7891000000010';buscar();document.getElementById('q').value='20';document.getElementById('preco').value='80';calcFinanceiro()");
                Check(await Script("document.getElementById('entry-unit-cost').value.includes('4,00')")=="true","Entrada: 20 unidades por R$ 80 calculam R$ 4 por unidade");
                await Script("document.getElementById('tipoemb').add(new Option('Caixa — 100 unidades','100'));document.getElementById('tipoemb').value='100';document.getElementById('q').value='2';document.getElementById('preco').value='180';calcFinanceiro()");
                Check(await Script("document.getElementById('entry-unit-cost').value.includes('0,90') && document.getElementById('financeiro').textContent.includes('200')")=="true","Entrada: 2 caixas com 100 unidades por R$ 180 calculam R$ 0,90");
                await Script("document.getElementById('q').value='10';document.getElementById('tipoemb').value='1';document.getElementById('preco').value='200,00';document.getElementById('desconto').value='10,00';calcFinanceiro()");
                Check(await Script("document.getElementById('discount-percent').textContent==='5%' && document.getElementById('entry-unit-cost').value.includes('19,00') && ['q','preco','desconto','acrescimo'].every(k=>document.getElementById(k).type==='text')")=="true","Campos sem setas: desconto R$ 10,00 em R$ 200,00 mostra 5% e custo R$ 19,00");
                await Script("show(2)");
                Check(await Script("document.getElementById('min').type==='text' && document.getElementById('un').placeholder.includes('rolo')")=="true","Mínimo digitável e exemplo de unidade corrigido para caixa");
                await Script("show(3);document.getElementById('preco').value='200';document.getElementById('acrescimo').value='10,00';calcFinanceiro()");
                Check(await Script("document.getElementById('addition-percent').textContent==='5%' && document.getElementById('q').closest('#formmov > .two > div')!==document.getElementById('preco').closest('#formmov > .two > div') && document.querySelectorAll('#formmov>.two>div')[0].children.length===6 && document.querySelectorAll('#formmov>.two>div')[1].children.length===5")=="true","Acréscimo exibe 5% e Entrada distribui campos em duas colunas equilibradas");
                await Script("show(4)");
                Check(await Script("document.getElementById('q').type==='text' && !document.getElementById('obs') && !!(document.getElementById('tipoemb').compareDocumentPosition(document.getElementById('q')) & Node.DOCUMENT_POSITION_FOLLOWING)")=="true","Saída com unidade antes da quantidade, sem setas nem observação");
                await Script("window.stockSnapshot=JSON.parse(JSON.stringify(desktopTest.inventory));window.stockFixture=JSON.parse(JSON.stringify(window.stockSnapshot));window.stockFixture.Products.find(p=>p.Id===window.editTestId).Packages=[{Name:'Caixa',Factor:5,Code:''}];window.stockFixture.Products.find(p=>p.Id===window.editTestId).Unit='pacote';window.stockFixture.Products.find(p=>p.Id===window.editTestId).Balance=3;desktopTest.sync(window.stockFixture);show(1)");
                Check(await Script("document.querySelector('[data-edit-product=\"'+window.editTestId+'\"]').closest('tr').cells[2].textContent==='1 caixa (3 pacotes)'")=="true","Saldo parcial mostra 1 caixa com 3 pacotes restantes");
                Check(await Script("(()=>{const names=[...document.querySelectorAll('#page table tr')].slice(1).map(r=>r.cells[0].firstChild.textContent);return names.every((name,i)=>!i||names[i-1].localeCompare(name,'pt-BR',{sensitivity:'base',numeric:true})<=0);})()")=="true","Produtos em ordem alfabética após atualização dos dados");
                await Script("window.stockFixture.Products.find(p=>p.Id===window.editTestId).Minimum=5;desktopTest.sync(window.stockFixture);show(0)");
                Check(await Script("(()=>{const table=document.querySelector('.low-stock-scroll table'),rows=[...table.rows].slice(1),row=rows.find(r=>r.cells[0].textContent==='Produto editado'),names=rows.map(r=>r.cells[0].textContent);return table.rows[0].cells.length===5 && !table.querySelector('button') && row.cells[2].textContent==='1 caixa (3 pacotes)' && names.every((name,i)=>!i||names[i-1].localeCompare(name,'pt-BR',{sensitivity:'base',numeric:true})<=0);})()")=="true","Visão geral sem ações, ordenada e com saldo por embalagem igual a Produtos");
                await Script("window.stockFixture.Products.find(p=>p.Id===window.editTestId).Packages=[{Name:'Caixa',Factor:100,Code:''}];window.stockFixture.Products.find(p=>p.Id===window.editTestId).Balance=130;desktopTest.sync(window.stockFixture);show(1)");
                Check(await Script("document.querySelector('[data-edit-product=\"'+window.editTestId+'\"]').closest('tr').cells[2].textContent==='2 caixas (130 pacotes)'")=="true","Sulfite é contado em caixas e pacotes, sem converter em folhas");
                await Script("window.stockFixture.Products.find(p=>p.Id===window.editTestId).Balance=0;desktopTest.sync(window.stockFixture);show(1)");
                Check(await Script("document.querySelector('[data-edit-product=\"'+window.editTestId+'\"]').closest('tr').cells[2].textContent==='0 caixas (0 pacotes)'")=="true","Caixa é descontada quando os pacotes acabam");
                await Script("desktopTest.sync(window.stockSnapshot);show(1)");
                await Script("window.originalConfirm=window.confirm;window.confirm=()=>false;document.querySelector('[data-delete-product=\"'+window.editTestId+'\"]').click()");
                Check(await Script("desktopTest.inventory.Products.some(p=>p.Id===window.editTestId)")=="true","Cancelar exclusão preserva produto");
                await Script("window.confirm=()=>true;window.historyCount=desktopTest.inventory.Movements.length;document.querySelector('[data-delete-product=\"'+window.editTestId+'\"]').click()");
                await Wait("!desktopTest.inventory.Products.some(p=>p.Id===window.editTestId)");
                Check(await Script("desktopTest.inventory.Movements.length===window.historyCount && !document.querySelector('[data-edit-product=\"'+window.editTestId+'\"]')")=="true","Exclusão remove cadastro e linha, preservando histórico");
                await Script("window.confirm=window.originalConfirm");
                using(var db = new InventoryStore(dbPath)) Check(!db.Load().Products.Any(p=>p.Code=="7891000000010") && db.Load().Movements.Count==3,"Exclusão persistida no SQLite sem apagar movimentações");
                foreach(var viewport in new[]{new Size(850,650),new Size(1024,720),new Size(1440,900)}){
                    form.ClientSize=viewport;await Task.Delay(100);await Script("show(6)");
                    Check(await Script("(()=>{const main=document.querySelector('main'),r=main.getBoundingClientRect(),charts=[...document.querySelectorAll('.chartgrid')];return r.right<=innerWidth+1 && r.bottom<=innerHeight+1 && main.scrollWidth<=main.clientWidth+1 && charts.every(c=>[...c.children].every(p=>p.getBoundingClientRect().right<=r.right+1));})()")=="true","Relatórios sem cortes laterais e rolagem acessível em "+viewport.Width+"x"+viewport.Height);
                }
                await Script("show(2);document.getElementById('code').value='MISTO120';document.getElementById('nome').value='Papel com duas conversões';document.getElementById('un').value='rolo';document.getElementById('emb').value='Fardo';document.getElementById('factor').value='40';window.addRegistrationPackage({Name:'Pacote',Factor:4,Code:''});cadastrar()");
                await Wait("produtos.some(p=>p.code==='MISTO120')");
                Check(await Script("desktopTest.inventory.Products.find(p=>p.Code==='MISTO120').Packages.length===2")=="true","Cadastro salva fardo com 40 rolos e pacote com 4 rolos");
                await Script("show(3);document.getElementById('codigo').value='MISTO120';buscar();document.getElementById('tipoemb').value='40';document.getElementById('q').value='3';document.getElementById('pessoa').value='Teste';document.getElementById('localcompra').value='Loja';document.getElementById('preco').value='120';movimentar()");
                await Wait("produtos.find(p=>p.code==='MISTO120').saldo===120");
                await Script("show(4);document.getElementById('codigo').value='MISTO120';buscar();document.getElementById('tipoemb').value='4';document.getElementById('q').value='2';document.querySelector('#mixed-output>button').click();document.querySelector('.output-part select').value='1';document.querySelector('.output-part input').value='2';document.querySelector('.output-part input').dispatchEvent(new Event('input'));document.getElementById('pessoa').value='Teste'");
                Check(await Script("document.getElementById('calc').textContent.includes('10 rolo')")=="true","Prévia da saída combina 2 pacotes e 2 rolos em 10 rolos");
                await Script("window.beforeMixed=desktopTest.inventory.Movements.length;movimentar()");
                await Wait("produtos.find(p=>p.code==='MISTO120').saldo===110");
                Check(await Script("desktopTest.inventory.Movements.length===window.beforeMixed+1 && desktopTest.inventory.Movements.at(-1).Converted===10 && desktopTest.inventory.Movements.at(-1).Parts.length===2")=="true","Saída combinada grava uma movimentação e desconta exatamente 10 rolos");
                await Script("show(1)");
                Check(await Script("[...document.querySelectorAll('#page tr')].some(r=>r.textContent.includes('2 fardos + 7 pacotes + 2 rolos (110 rolos)'))")=="true","Saldo decomposto em fardos, pacotes e rolos avulsos");
                await Script("window.mixedBlocked='pending';desktopTest.request('move',{ProductId:desktopTest.inventory.Products.find(p=>p.Code==='MISTO120').Id,Type:'Saída',Person:'Teste',Parts:[{PackageIndex:0,Quantity:1000}]}).then(()=>window.mixedBlocked='failed').catch(()=>window.mixedBlocked='blocked')");
                await Wait("window.mixedBlocked!=='pending'");
                Check(await Script("window.mixedBlocked==='blocked' && produtos.find(p=>p.code==='MISTO120').saldo===110")=="true","Saída combinada acima do saldo bloqueada sem alterar estoque");
                await Script("show(1);document.querySelector('[data-edit-product=\"'+desktopTest.inventory.Products.find(p=>p.Code==='MISTO120').Id+'\"]').click();document.getElementById('un').value='folha';document.getElementById('un').dispatchEvent(new Event('input'))");
                Check(await Script("!document.getElementById('unit-migration').hidden")=="true","Mudança da unidade pede conversão explícita do saldo existente");
                await Script("document.getElementById('unit-conversion').value='2';document.getElementById('factor').value='80';document.querySelector('.package-factor').value='8';cadastrar()");
                await Wait("current===1");
                Check(await Script("(()=>{const p=desktopTest.inventory.Products.find(p=>p.Code==='MISTO120');return p.Balance===220 && p.AverageCost===0.5 && p.Unit==='folha' && desktopTest.inventory.Movements.at(-1).Unit==='rolo' && desktopTest.inventory.Movements.at(-1).Converted===10;})()")=="true","Migração converte saldo e custo, preservando unidade e quantidade do histórico");
                await Script("show(2);document.getElementById('code').value='NIVEIS-SULFITE';document.getElementById('nome').value='Sulfite por pacote';document.getElementById('emb').value='Caixa';document.getElementById('level-package').checked=true;document.getElementById('level-unit').checked=false;document.getElementById('factor').value='100';document.getElementById('min').value='1';document.getElementById('level-package-min').value='5';document.getElementById('level-package').dispatchEvent(new Event('change',{bubbles:true}));cadastrar()");
                await Wait("produtos.some(p=>p.code==='NIVEIS-SULFITE')");
                Check(await Script("(()=>{const p=desktopTest.inventory.Products.find(p=>p.Code==='NIVEIS-SULFITE');return p.Unit==='pacote' && p.ControlLevels.length===2 && p.Packages.length===1 && p.Packages[0].Factor===100 && p.Minimum===5 && p.ControlLevels[1].Minimum===5;})()")=="true","Caixa + pacote: conversão de 100 pacotes e mínimos independentes salvos, sem folhas");
                await Script("show(3);document.getElementById('codigo').value='NIVEIS-SULFITE';buscar()");
                Check(await Script("document.getElementById('tipoemb').options.length===2 && document.getElementById('tipoemb').options[0].textContent.includes('pacote') && document.getElementById('tipoemb').options[1].textContent.includes('Caixa')")=="true","Entrada de sulfite oferece apenas caixa e pacote cadastrados");
                await Script("show(2);document.getElementById('code').value='NIVEIS-PAPEL';document.getElementById('nome').value='Papel em três níveis';document.getElementById('emb').value='Fardo';document.getElementById('level-package').checked=true;document.getElementById('level-unit').checked=true;document.getElementById('un').value='rolo';document.getElementById('factor').value='10';document.getElementById('level-package-count').value='4';document.getElementById('min').value='1';document.getElementById('level-package-min').value='2';document.getElementById('level-unit-min').value='3';document.getElementById('level-package').dispatchEvent(new Event('change',{bubbles:true}));cadastrar()");
                await Wait("produtos.some(p=>p.code==='NIVEIS-PAPEL')");
                Check(await Script("(()=>{const p=desktopTest.inventory.Products.find(p=>p.Code==='NIVEIS-PAPEL');return p.Unit==='rolo' && p.ControlLevels.length===3 && p.Packages[0].Factor===40 && p.Packages[1].Factor===4 && p.ControlLevels.map(l=>l.Minimum).join(',')==='1,2,3';})()")=="true","Fardo + pacote + unidade: 10 pacotes por fardo, 4 rolos por pacote e três mínimos persistidos");
                await Script("show(1);document.querySelector('[data-edit-product=\"'+desktopTest.inventory.Products.find(p=>p.Code==='NIVEIS-PAPEL').Id+'\"]').click()");
                Check(await Script("document.getElementById('factor').value==='10' && document.getElementById('level-package-count').value==='4' && document.getElementById('level-package-min').value==='2' && document.getElementById('level-unit-min').value==='3'")=="true","Edição recupera níveis, conversões e mínimos");
                await Script("show(2);document.getElementById('code').value='NIVEIS-CANETA';document.getElementById('nome').value='Caneta por unidade';document.getElementById('emb').value='Caixa';document.getElementById('level-package').checked=false;document.getElementById('level-unit').checked=true;document.getElementById('un').value='caneta';document.getElementById('factor').value='100';document.getElementById('min').value='1';document.getElementById('level-unit-min').value='10';document.getElementById('level-unit').dispatchEvent(new Event('change',{bubbles:true}));cadastrar()");
                await Wait("produtos.some(p=>p.code==='NIVEIS-CANETA')");
                Check(await Script("(()=>{const p=desktopTest.inventory.Products.find(p=>p.Code==='NIVEIS-CANETA');return p.Unit==='caneta' && p.ControlLevels.length===2 && p.ControlLevels.every(l=>l.Kind!=='package') && p.Packages[0].Factor===100;})()")=="true","Caixa + unidade: caneta sem nível de pacote");
                await Script("show(4);document.getElementById('codigo').value='NIVEIS-PAPEL';buscar()");
                Check(await Script("document.getElementById('tipoemb').options.length===3")=="true","Saída oferece as três apresentações cadastradas do papel");
                await Script("window.balanceExample=JSON.parse(JSON.stringify(desktopTest.inventory));const example=window.balanceExample.Products.find(p=>p.Code==='NIVEIS-SULFITE');example.Balance=70;example.Packages[0].Factor=50;example.ControlLevels[0].Factor=50;example.ControlLevels[1].Minimum=20;desktopTest.sync(window.balanceExample);show(1)");
                Check(await Script("(()=>{const id=desktopTest.inventory.Products.find(p=>p.Code==='NIVEIS-SULFITE').Id,row=document.querySelector('[data-edit-product=\"'+id+'\"]').closest('tr');return row.cells[2].textContent==='2 caixas (70 pacotes)' && row.cells[3].textContent==='1 caixa (20 pacotes)';})()")=="true","Saldo de 70 pacotes em caixas de 50 mostra 2 caixas; mínimo mostra 1 caixa (20 pacotes)");
                await Script("window.thresholdReady=false;(()=>{const p=desktopTest.inventory.Products.find(p=>p.Code==='NIVEIS-SULFITE');desktopTest.request('saveProduct',{...p,ControlLevels:[{Kind:'upper',Name:'Caixa',Factor:50,Minimum:1},{Kind:'package',Name:'pacote',Factor:1,Minimum:20}],Packages:[{Name:'Caixa',Factor:50,Code:''}]}).then(s=>{desktopTest.sync(s);window.thresholdReady=true;});})()");
                await Wait("window.thresholdReady");
                await Script("window.thresholdInventory=JSON.parse(JSON.stringify(desktopTest.inventory));window.thresholdInventory.Products.find(p=>p.Code==='NIVEIS-SULFITE').Balance=40;desktopTest.sync(window.thresholdInventory);show(1)");
                Check(await Script("(()=>{const p=desktopTest.inventory.Products.find(p=>p.Code==='NIVEIS-SULFITE'),r=document.querySelector('[data-edit-product=\"'+p.Id+'\"]').closest('tr');return p.Minimum===20 && r.cells[4].textContent==='Normal';})()")=="true","Mínimo de 1 caixa (20 pacotes): saldo de 40 pacotes é Normal");
                await Script("window.thresholdInventory.Products.find(p=>p.Code==='NIVEIS-SULFITE').Balance=20;desktopTest.sync(window.thresholdInventory);show(1)");
                Check(await Script("(()=>{const p=desktopTest.inventory.Products.find(p=>p.Code==='NIVEIS-SULFITE');return document.querySelector('[data-edit-product=\"'+p.Id+'\"]').closest('tr').cells[4].textContent==='Estoque baixo';})()")=="true","Alerta de estoque baixo somente ao atingir o mínimo de 20 pacotes");
                using(var db=new InventoryStore(dbPath))Check(db.Load().Products.Single(p=>p.Code=="NIVEIS-SULFITE").Minimum==20,"Reabertura normaliza o mínimo dos cadastros existentes pelo menor nível");
                // Compara diretamente com o arquivo recebido, no mesmo motor e viewport.
                var navigated = new TaskCompletionSource();
                void Loaded(object? s,CoreWebView2NavigationCompletedEventArgs e) => navigated.TrySetResult();
                form.Browser.CoreWebView2.NavigationCompleted += Loaded;
                form.Browser.NavigateToString(Form1.Resource("Prototipo.html")); await navigated.Task.WaitAsync(TimeSpan.FromSeconds(20));
                form.Browser.CoreWebView2.NavigationCompleted -= Loaded;
                await Script("show(8);document.querySelector('#page .content > .notice').remove()");
                Check(await Script("(()=>{const p=document.querySelector('#page').cloneNode(true);p.querySelectorAll('button').forEach(b=>b.remove());return p.textContent;})()")==actualDescription,"Descrição original preservada, com remoção somente do aviso solicitado");
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









