// Integração local. O HTML/CSS do protótipo é preservado integralmente.
(() => {
    'use strict';
    const pending = new Map();
    let sequence = 0, inventory = {Products:[],Movements:[]}, saving = false;
    const referenceRender = render;
    const referenceReports = desenhaRelatorio;
    const referenceSearch = buscar;
    const referenceTable = table;
    const referenceMoney = dinheiro;
    let lastSavedProduct=null,editingProduct=null,scanTimer;
    let financeMonth=new Date().getMonth()+1,financeYear=new Date().getFullYear();
    const purchaseHistoryFilters={start:'',end:'',product:'',store:''};
    const $ = id => document.getElementById(id);
    const localDate = d => new Date(d).toLocaleDateString('sv-SE');
    function request(type,payload) {
        return new Promise((resolve,reject) => {
            const id = String(++sequence);
            const timeout = setTimeout(() => { pending.delete(id); reject(new Error('O sistema não respondeu. Tente novamente.')); },120000);
            pending.set(id,{resolve,reject,timeout});
            window.chrome.webview.postMessage({id,type,payload});
        });
    }
    window.chrome.webview.addEventListener('message',event => {
        const m = event.data;
        if(m.inventory) { sync(m.inventory); render(); return; }
        const callback = pending.get(m.id); if(!callback) return;
        clearTimeout(callback.timeout); pending.delete(m.id);
        if(m.error) callback.reject(new Error(m.error)); else callback.resolve(m.result);
    });
    function sync(data) {
        inventory = data;
        produtos = data.Products.filter(p => p.Active).map(p => ({
            id:p.Id,code:p.Code,nome:p.Name,cat:p.Category,un:p.Unit,saldo:p.Balance,min:p.Minimum,local:p.Location,
            emb:p.Packages[0]?.Name||'',factor:p.Packages[0]?.Factor||1,packages:p.Packages,aliases:p.CodeAliases||[],reference:p.Reference
        }));
        hist = [...data.Movements].sort((a,b) => b.Date.localeCompare(a.Date)).map(m => ({tipo:m.Type,nome:m.ProductName,q:m.Converted,un:m.Unit,pessoa:m.Person,setor:m.Sector,data:new Date(m.Date).toLocaleString('pt-BR'),id:m.Id}));
        compras = data.Movements.filter(m => m.Type === 'Entrada').map(m => ({
            id:m.Id,nome:m.ProductName,local:m.Store||'Não informado',cat:m.Category,valor:m.Total,ref:m.Total===null?null:m.ReferenceTotal,
            unidade:m.Unit,quantidade:m.Converted,data:m.PurchaseDate.slice(0,10),unknown:m.Total===null
        }));
        saidasDemo = data.Movements.filter(m => m.Type === 'Saída').map(m => ({nome:m.ProductName,cat:m.Category,q:m.Converted,un:m.Unit,custo:m.AppliedCost===null?null:m.AppliedCost*m.Converted,setor:m.Sector,pessoa:m.Person,data:m.Date.slice(0,10)}));
        estoqueDemo = data.Products.filter(p => p.Active).map(p => ({nome:p.Name,cat:p.Category,q:p.Balance,un:p.Unit,min:p.Minimum,desejado:p.Target,local:p.Location,custo:p.AverageCost,ultima:data.Movements.filter(m => m.ProductId===p.Id&&m.Type==='Saída').map(m=>m.Date.slice(0,10)).sort().at(-1)||'Sem saída'}));
    }
    async function save(action) {
        if(saving) return;
        saving = true;
        const buttons = [...document.querySelectorAll('button.action')]; buttons.forEach(b=>b.disabled=true);
        try { await action(); } catch(error) { alert(error.message); }
        finally { saving=false; buttons.forEach(b=>b.disabled=false); }
    }
    function dataNotice(element,text) { if(element) element.textContent=text; }
    const layoutStyle=document.createElement('style');
    layoutStyle.id='desktop-layout';
    layoutStyle.textContent=`
        @media screen{
            html,body{height:100%;overflow:hidden}
            body{display:flex;flex-direction:column}
            body>.bar{flex:0 0 auto}
            body>.layout{display:flex;flex:1;min-height:0;height:0}
            .layout>nav{height:100%;flex:0 0 220px;overflow-y:auto;overscroll-behavior:contain}
            .layout>main{height:100%;min-height:0;min-width:0;flex:1 1 0;overflow:auto;overscroll-behavior:contain;container-type:inline-size}
            #page,.panel,.chartgrid>*,.report-grid>*,.two>*{min-width:0}
            .chartgrid{grid-template-columns:repeat(2,minmax(0,1fr))}
            .chartbar{grid-template-columns:minmax(80px,135px) minmax(0,1fr) minmax(75px,105px)}
            .chartbar>*{min-width:0;overflow-wrap:anywhere}
            .report-tabs{display:flex;flex-wrap:wrap;gap:8px}
            .responsive-table{max-width:100%;overflow-x:auto}
            .responsive-table table{min-width:650px}
            .products-heading{flex-wrap:wrap}
            @container(max-width:950px){.chartgrid{grid-template-columns:minmax(0,1fr)}.report-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
            @container(max-width:600px){.two,.registration-panel .two,.filtergrid,.grid{grid-template-columns:minmax(0,1fr)!important}.report-grid{grid-template-columns:minmax(0,1fr)}.chartbar{grid-template-columns:minmax(70px,110px) minmax(0,1fr) 90px}}
            @media(max-width:1100px){.layout>main{padding:20px}.layout>nav{flex-basis:190px;min-width:190px;width:190px}.bar{gap:12px;flex-wrap:wrap}.bar img{max-width:100%;height:auto}}

        }
        .products-heading{display:flex;align-items:center;justify-content:space-between;gap:20px;margin-bottom:20px}
        .products-heading button.action{margin:0;flex-shrink:0}
        .financial-info-card{position:relative;padding-right:42px}
        .financial-info-card>.info-wrap{position:absolute;top:18px;right:16px;margin-left:0}
        .financial-info-card>.info-wrap .info-text{left:auto;right:0;transform:none}
        .low-stock-scroll{max-height:256px;overflow-y:auto;overscroll-behavior:contain}
        .low-stock-scroll th{height:40px;position:sticky;top:0;background:white;z-index:1}
        .low-stock-scroll td{height:54px;padding-top:8px;padding-bottom:8px}
        body[data-screen="2"] .layout{height:calc(100vh - var(--app-header-height,68px));min-height:0}
        body[data-screen="2"] main{padding:20px 28px 12px;display:flex;flex-direction:column;min-height:0}
        body[data-screen="2"] #page{flex:1;display:flex;flex-direction:column;min-height:0}
        body[data-screen="2"] #page>h1{font-size:25px;margin-bottom:6px}
        body[data-screen="2"] #page>.muted{font-size:14px;margin-bottom:12px}
        body[data-screen="2"] .registration-panel{padding:20px;margin-bottom:0;flex:0 0 auto;min-height:min(480px,calc(100vh - 200px));box-sizing:border-box;display:flex;flex-wrap:wrap;align-content:start;align-items:center;gap:0}
        .registration-panel>.two{width:100%;gap:20px}
        .registration-panel .form-column{display:grid;gap:clamp(5px,1vh,10px);align-content:start}
        .registration-panel .form-field{min-width:0}
        .registration-panel label{margin:0 0 6px;font-size:14px}
        .registration-panel input,.registration-panel select{padding:10px 12px;font-size:15px;height:clamp(38px,5.2vh,46px)}
        .registration-panel .registration-help{width:100%;font-size:12px;line-height:1.35;margin:16px 0 0}
        .registration-panel button.action{margin-top:10px;padding:9px 16px}
        .registration-panel .status:empty{display:none}
        .registration-panel .status{width:100%;margin-top:6px;font-size:13px}
        .code-entry{display:flex;gap:6px;align-items:stretch}
        .code-entry input{min-width:0;flex:1}
        .code-entry button{border:1px solid #dce2ec;border-radius:7px;color:#20366c;background:#eef1f8;padding:0 9px;cursor:pointer;white-space:nowrap;font-size:12px}
        .product-actions{display:flex;gap:6px;align-items:center;white-space:nowrap}.product-icon{width:32px;height:32px;padding:4px;display:inline-flex;align-items:center;justify-content:center}.product-icon svg{width:16px;height:16px}.product-delete{color:#a33232!important}
        .label-action{border:1px solid #dce2ec;border-radius:6px;background:#eef1f8;color:#20366c;padding:7px 10px;cursor:pointer;white-space:nowrap}
        #label-dialog{width:min(860px,95vw);max-height:92vh;overflow:auto;border:1px solid #dce2ec;border-radius:12px;padding:22px;color:#23314b}
        #label-dialog::backdrop{background:#172a5066}
        #link-code-dialog{width:min(760px,95vw);max-height:90vh;overflow:auto;border:1px solid #dce2ec;border-radius:12px;padding:24px;color:#23314b}
        #link-code-dialog::backdrop{background:#172a5066}
        #link-code-dialog h2{margin-top:0}
        #link-product{height:230px;padding:0}
        #link-product option{padding:10px 12px;border-bottom:1px solid #edf0f5;white-space:normal}
        #label-dialog h2{margin:0 0 10px}
        #label-dialog .label-options{display:flex;gap:14px;align-items:end;margin-bottom:18px}
        #label-dialog .label-options>div{flex:1}
        #label-print-area{display:flex;flex-wrap:wrap;gap:10px;max-height:45vh;overflow:auto;padding:10px;background:#f3f5fa}
        .barcode-label{box-sizing:border-box;width:60mm;height:40mm;flex:none;background:white;color:black;padding:3mm;display:flex;flex-direction:column;align-items:center;justify-content:center;overflow:hidden;gap:1mm}
        .barcode-label strong{font:600 10pt 'Segoe UI',sans-serif;max-height:10mm;overflow:hidden;text-align:center}
        .barcode-label img{width:54mm;height:15mm;object-fit:fill;display:block}
        .barcode-label code{font:11pt Consolas,monospace;color:black}
        .barcode-label small{font:8pt 'Segoe UI',sans-serif;max-width:100%;overflow:hidden;white-space:nowrap;text-overflow:ellipsis}
        .pdf-filter-summary,#all-reports-pdf{display:none}
        .purchase-history-print-summary{display:none}
        @media print{
            .responsive-table{overflow:visible!important}.responsive-table table{min-width:0!important}.history-toggle{display:none!important}.history-collapsible-content[hidden]{display:block!important}
            .purchase-history-print-summary{display:block!important;white-space:pre-line;background:#f3f5fa;padding:8px 10px;border-radius:6px;line-height:1.5}
            html,body{height:auto!important;max-height:none!important;overflow:visible!important}
            body:not(.printing-labels) main,body:not(.printing-labels) #page{height:auto!important;max-height:none!important;overflow:visible!important;display:block!important;min-width:0!important}
            body:not(.printing-labels) .panel{overflow:visible!important}
            body:not(.printing-labels) h1,body:not(.printing-labels) h2,body:not(.printing-labels) h3{break-after:avoid;overflow-wrap:anywhere}
            body:not(.printing-labels) .content p,body:not(.printing-labels) .content li{orphans:3;widows:3;overflow-wrap:anywhere}
            body:not(.printing-labels) .history-filter-controls{display:none!important}
            body.pdf-portrait:not(.printing-labels) .chartgrid{grid-template-columns:1fr!important}
            body.pdf-portrait:not(.printing-labels) .report-grid{grid-template-columns:repeat(2,minmax(0,1fr))!important}
            .low-stock-scroll{max-height:none!important;overflow:visible!important}
            .low-stock-scroll th{position:static!important}
            .low-stock-scroll th,.low-stock-scroll td{height:auto!important}
            body[data-screen="0"] .card{margin-bottom:0!important}
            body[data-screen="0"] .card b{font-size:19px!important}
            .low-stock-scroll th:nth-child(6),.low-stock-scroll td:nth-child(6),.finance-filters,.dashboard-quick-actions{display:none!important}
            body[data-screen="0"] #page>.grid{grid-template-columns:repeat(2,minmax(0,1fr))!important}
            body.printing-all-reports:not(.printing-labels)>.bar,body.printing-all-reports:not(.printing-labels)>.layout{display:none!important}
            body.printing-all-reports #all-reports-pdf{display:block!important}
            #all-reports-pdf .pdf-report-section{break-before:page}
            #all-reports-pdf .pdf-report-section:first-child{break-before:auto}
            #all-reports-pdf h1{font-size:22px;margin:16px 0 8px}
            body:not(.printing-labels){background:white!important;font-size:11px!important;color:#23314b;print-color-adjust:exact;-webkit-print-color-adjust:exact}
            body:not(.printing-labels) nav,body:not(.printing-labels) .badge-top,body:not(.printing-labels) button,body:not(.printing-labels) .report-tabs,body:not(.printing-labels) .info-wrap,body:not(.printing-labels) #desktop-menu,body:not(.printing-labels) #label-dialog{display:none!important}
            body:not(.printing-labels) #page>.notice,body:not(.printing-labels) .report-filter-panel{display:none!important}
            body:not(.printing-labels) .layout{display:block!important;min-height:0!important}
            body:not(.printing-labels) main{display:block!important;padding:15px 0 0!important;max-width:none!important;width:100%!important}
            body:not(.printing-labels) .bar{padding:0 0 12px!important;box-shadow:none!important;background:white!important}
            body:not(.printing-labels) #page h1{font-size:22px!important}
            body:not(.printing-labels) #page h2{font-size:14px!important}
            body:not(.printing-labels) .report-title{display:block!important}
            body:not(.printing-labels) .pdf-filter-summary{display:block!important;background:#f3f5fa;padding:10px 12px;margin:14px 0;border:1px solid #e4e7ef;border-radius:6px;line-height:1.6;white-space:pre-line}
            body:not(.printing-labels) .panel,body:not(.printing-labels) .card,body:not(.printing-labels) .metric{box-shadow:none!important;break-inside:avoid;padding:12px!important}
            body:not(.printing-labels) .panel h2,body:not(.printing-labels) .section-note{break-after:avoid}
            body:not(.printing-labels) .report-grid{grid-template-columns:repeat(4,minmax(0,1fr))!important;gap:10px!important;margin:12px 0!important}
            body:not(.printing-labels) .metric{min-height:85px!important}
            body:not(.printing-labels) .metric strong{font-size:19px!important}
            body:not(.printing-labels) .chartgrid{grid-template-columns:1fr 1fr!important;gap:12px!important}
            body:not(.printing-labels) .chartbar{grid-template-columns:115px 1fr 85px!important;font-size:11px!important}
            body:not(.printing-labels) .tablewrap{overflow:visible!important}
            body:not(.printing-labels) table{table-layout:fixed;width:100%!important;font-size:10px!important}
            body:not(.printing-labels) th,body:not(.printing-labels) td{padding:8px 5px!important;min-width:0!important;white-space:normal!important;overflow-wrap:anywhere}
            body:not(.printing-labels) thead{display:table-header-group}
            body:not(.printing-labels) tr{break-inside:avoid}
            body.printing-labels>*{display:none!important}
            body.printing-labels #label-dialog{display:block!important;position:static!important;width:auto!important;max-width:none!important;max-height:none!important;border:0!important;padding:0!important;margin:0!important;overflow:visible!important}
            body.printing-labels #label-dialog>*:not(#label-print-area){display:none!important}
            body.printing-labels #label-print-area{display:block!important;max-height:none!important;overflow:visible!important;background:white!important;padding:0!important}
            body.printing-labels .barcode-label{break-after:page;break-inside:avoid;margin:0;print-color-adjust:exact;-webkit-print-color-adjust:exact}
            body.printing-labels .barcode-label:last-child{break-after:auto}
        }
        @media(max-height:800px){
            body[data-screen="2"] main{padding:14px 24px 12px}
            body[data-screen="2"] .registration-panel{padding:16px}
            .registration-panel .form-column{gap:3px}
            .registration-panel label{font-size:12px;margin-bottom:2px}
            .registration-panel input,.registration-panel select{height:28px;padding:4px 9px;font-size:13px}
            .registration-panel .registration-help{margin-top:10px}
        }
        @media(max-width:700px){
            .products-heading{align-items:flex-start}
            body[data-screen="2"] main{padding:12px}
            .registration-panel .two{grid-template-columns:1fr 1fr;gap:12px}
        }
    `;
    document.head.append(layoutStyle);
    function updateHeaderHeight(){document.documentElement.style.setProperty('--app-header-height',document.querySelector('.bar').getBoundingClientRect().height+'px');}
    window.addEventListener('resize',updateHeaderHeight);
    updateHeaderHeight();
    const headerDate=document.createElement('time');headerDate.id='header-current-date';headerDate.style.cssText='margin-left:auto;white-space:nowrap;color:#20366c;font-size:14px';
    const headerBadge=document.querySelector('.bar .badge-top');
    if(headerBadge){headerBadge.style.marginLeft='20px';headerBadge.before(headerDate);}else document.querySelector('.bar').append(headerDate);
    function refreshHeaderDate(){const today=new Date();headerDate.textContent=today.toLocaleDateString('pt-BR',{timeZone:'America/Sao_Paulo'});headerDate.dateTime=today.toLocaleDateString('sv-SE',{timeZone:'America/Sao_Paulo'});}
    refreshHeaderDate();setInterval(refreshHeaderDate,60000);
    function arrangeRegistration(){
        const panel=document.querySelector('#page .panel');
        panel.classList.add('registration-panel');
        const columns=document.createElement('div');columns.className='two';
        $('nome').spellcheck=true;$('nome').lang='pt-BR';
        $('un').placeholder='Ex.: unidade, caixa, pacote';
        for(const key of ['min','factor']){$(key).type='text';$(key).inputMode='numeric';}
        const definitions=[
            [['code','Código de barras ou código interno'],['nome','Nome do produto'],['cat','Categoria'],['un','Menor item controlado nas saídas (ex.: pacote ou rolo)']],
            [['min','Estoque mínimo'],['local','Local de armazenamento'],['emb','Embalagem de compra (opcional)'],['factor','Unidades de controle em cada caixa / fardo']]
        ];
        for(const fields of definitions){
            const column=document.createElement('div');column.className='form-column';
            for(const [id,text] of fields){
                const input=$(id),field=document.createElement('div'),label=document.createElement('label');
                field.className='form-field';label.htmlFor=id;label.textContent=text;field.append(label,input);column.append(field);
            }
            columns.append(column);
        }
        const button=panel.querySelector('button.action'),message=$('msg'),help=document.createElement('p');
        help.className='muted registration-help';help.textContent='Sulfite: controle por pacote e cadastre 1 caixa = 100 pacotes; as 500 folhas do pacote não precisam ser cadastradas. Papel higiênico: controle por rolo apenas se houver retirada de rolos avulsos; nesse caso cadastre fardo e pacote em rolos.';
        panel.replaceChildren(columns,help,button,message);
        $('min').insertAdjacentHTML('afterend','<small id="minimum-conversion" class="muted"></small>');
        const input=$('code'),group=document.createElement('div'),generate=document.createElement('button');
        group.className='code-entry';input.before(group);generate.type='button';generate.id='generate-barcode';generate.textContent='Gerar código';generate.title='Produto sem código de barras: gerar código interno único';
        generate.onclick=async()=>{generate.disabled=true;try{input.value=await request('generateCode');lastSavedProduct=null;input.dispatchEvent(new Event('change'));}catch(e){alert(e.message);}finally{generate.disabled=false;}};
        group.append(input,generate);
        const print=document.createElement('button');print.type='button';print.id='print-saved-label';print.className='action secondary';print.textContent='Imprimir etiqueta';print.hidden=true;print.onclick=()=>openLabel(lastSavedProduct);button.after(print);
        input.addEventListener('input',()=>{lastSavedProduct=null;print.hidden=true;});
        function updatePurchase(){
            const packaged=!!$('emb').value;
            document.querySelector('label[for="factor"]').textContent=packaged?'Unidades de controle em cada '+$('emb').value.toLocaleLowerCase('pt-BR'):'Unidades por item avulso';
            document.querySelector('label[for="factor"]').textContent=packaged?'Quantos '+($('un').value.trim()||'itens controlados')+' há em cada '+$('emb').value.toLocaleLowerCase('pt-BR')+'?':'Itens por unidade de controle';
            $('factor').disabled=!packaged;
            const minimumFactor=packaged?Number($('factor').value):1,minimum=Number($('min').value),controlUnit=$('un').value.trim()||'unidade';
            document.querySelector('label[for="min"]').textContent='Estoque mínimo (em '+(packaged?$('emb').value.toLocaleLowerCase('pt-BR'):controlUnit)+')';
            $('minimum-conversion').textContent=packaged&&minimumFactor>0?`Equivale a ${minimum*minimumFactor} ${controlUnit} no estoque.`:'';

        }
        for(const key of ['factor','emb','un','min'])$(key).addEventListener('input',updatePurchase);
        window.updateRegistrationPurchase=updatePurchase;updatePurchase();
        const packages=document.createElement('div');packages.id='extra-packages';packages.style.cssText='width:100%;margin-top:12px';
        const add=document.createElement('button');add.type='button';add.className='action secondary';add.textContent='+ Adicionar outra embalagem';
        add.title='Use somente se também controlar uma embalagem intermediária. Todas as conversões usam o menor item controlado.';
        add.onclick=()=>addPackageField({Name:'',Factor:1,Code:''});panel.insertBefore(packages,help);panel.insertBefore(add,help);
        function addPackageField(pack={Name:'Pacote',Factor:4,Code:''}){
            const row=document.createElement('div');row.className='extra-package-row';row.dataset.original=JSON.stringify(pack);row.style.cssText='display:flex;gap:12px;align-items:end;margin-bottom:10px';
            row.innerHTML='<div style="flex:1"><label>Outra embalagem (ex.: Pacote)</label><input class="package-name"></div><div style="flex:1"><label>Unidades de controle nessa embalagem</label><input class="package-factor" type="text" inputmode="numeric"></div><button type="button" class="label-action">Remover</button>';
            row.querySelector('.package-factor').previousElementSibling.textContent='Quantidade de '+($('un').value.trim()||'itens controlados')+' nessa embalagem';
            row.querySelector('.package-name').value=pack.Name;row.querySelector('.package-factor').value=pack.Factor;row.querySelector('button').onclick=()=>row.remove();packages.append(row);
        }
        window.addRegistrationPackage=addPackageField;
        const migration=document.createElement('div');migration.id='unit-migration';migration.hidden=true;migration.style.cssText='width:100%;margin-top:10px';migration.innerHTML='<label for="unit-conversion">Conversão do saldo existente: quantas novas unidades existem em cada unidade antiga?</label><input id="unit-conversion" type="text" inputmode="numeric" placeholder="Ex.: 4 rolos por pacote antigo"><small>Para apenas renomear unidade para pacote, informe 1. Para converter pacote em 4 rolos, informe 4. O histórico anterior será preservado.</small>';panel.insertBefore(migration,help);
        $('un').addEventListener('input',()=>{migration.hidden=!editingProduct||$('un').value.trim()===editingProduct.Unit;});
        configureControlLevels(panel,help,add,packages);

    }
    function configureControlLevels(panel,help,legacyAdd,legacyPackages){
        legacyAdd.hidden=true;legacyPackages.hidden=true;
        const section=document.createElement('section');section.id='control-levels';section.style.cssText='width:100%;margin-top:12px';
        section.innerHTML=`<h2 style="font-size:16px;margin:0 0 8px">Apresentações controladas</h2>
        <div class="control-level-row" id="level-upper-row"><div><label for="emb">Compra / embalagem maior</label></div><div><label id="level-upper-count-label" for="factor">Quantidade dentro da embalagem</label></div><div><label for="min">Estoque mínimo de caixas / fardos</label></div></div>
        <div class="control-level-row"><div><label><input type="checkbox" id="level-package"> Controlar pacote</label></div><div id="level-package-count-field"><label for="level-package-count">Unidades em cada pacote</label><input id="level-package-count" type="text" inputmode="numeric" value="1"></div><div id="level-package-min-field"><label for="level-package-min">Estoque mínimo de pacotes</label><input id="level-package-min" type="text" inputmode="numeric" value="0"></div></div>
        <div class="control-level-row"><div><label><input type="checkbox" id="level-unit" checked> Controlar unidade avulsa</label><div id="level-unit-name"></div></div><div></div><div id="level-unit-min-field"><label for="level-unit-min">Estoque mínimo de unidades</label><input id="level-unit-min" type="text" inputmode="numeric" value="0"></div></div>
        <p id="level-summary" class="small muted" style="margin:6px 0" aria-live="polite"></p>`;
        const styles=document.createElement('style');styles.textContent='.control-level-row{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;align-items:end;padding:7px 0;border-bottom:1px solid #e4e7ef}.control-level-row input[type=checkbox]{width:auto;height:auto;margin-right:6px}.control-level-row label{font-size:13px}.control-level-row input:not([type=checkbox]),.control-level-row select{height:34px;padding:6px 9px}.control-level-row [hidden]{display:none!important}@media(max-width:850px){.control-level-row{grid-template-columns:repeat(2,minmax(0,1fr))}}';section.prepend(styles);
        panel.insertBefore(section,help);
        for(const id of ['emb','factor','min','un']){const input=$(id),old=input.closest('.form-field');old.hidden=true;if(id==='un'){$('level-unit-name').append(input);}else{const column=id==='emb'?0:id==='factor'?1:2;section.querySelector('#level-upper-row').children[column].append(input);}}
        [...$('emb').options].filter(o=>!['','Caixa','Fardo'].includes(o.value)).forEach(o=>o.remove());
        if(![...$('emb').options].some(o=>o.value==='Caixa'))$('emb').add(new Option('Caixa','Caixa'));
        if(![...$('emb').options].some(o=>o.value==='Fardo'))$('emb').add(new Option('Fardo','Fardo'));
        [...$('emb').options].forEach(o=>{if(o.value==='')o.textContent='Avulso — sem caixa / fardo';});
        $('un').placeholder='Nome da unidade avulsa (ex.: rolo, caneta)';
        panel.insertBefore(section,help);
        help.textContent='Marque somente o que será controlado. O alerta usa o mínimo do menor nível controlado. Ex.: mínimo de 1 caixa (20 pacotes) avisa quando restarem 20 pacotes ou menos.';
        let dirty=false;
        function read(){
            const upper=$('emb').value,hasPackage=$('level-package').checked,hasUnit=$('level-unit').checked;
            if(!upper&&!hasPackage&&!hasUnit)throw new Error('Selecione pelo menos uma apresentação: caixa/fardo, pacote ou unidade.');
            const integer=(id,positive=false)=>{const raw=$(id).value.trim(),n=Number(raw);if(!raw||!Number.isSafeInteger(n)||n<(positive?1:0))throw new Error('Informe quantidades inteiras válidas nas apresentações e mínimos.');return n;};
            const levels=[],unitName=$('un').value.trim()||'unidade';
            const packageFactor=hasPackage&&hasUnit?integer('level-package-count',true):1;
            if(upper)levels.push({Kind:'upper',Name:upper,Factor:hasPackage||hasUnit?integer('factor',true)*packageFactor:1,Minimum:integer('min')});
            if(hasPackage)levels.push({Kind:'package',Name:'pacote',Factor:packageFactor,Minimum:integer('level-package-min')});
            if(hasUnit)levels.push({Kind:'unit',Name:unitName,Factor:1,Minimum:integer('level-unit-min')});
            if(new Set(levels.map(l=>l.Name.toLocaleLowerCase('pt-BR'))).size!==levels.length)throw new Error('Use nomes diferentes para os níveis: caixa/fardo, pacote e unidade avulsa.');
            return levels;
        }
        function update(){
            const upper=!!$('emb').value,pack=$('level-package').checked,unit=$('level-unit').checked;
            $('factor').disabled=!upper||(!pack&&!unit);$('min').disabled=!upper;
            $('level-package-count-field').hidden=!pack||!unit;$('level-package-min-field').hidden=!pack;
            $('level-unit-name').hidden=!unit;$('level-unit-min-field').hidden=!unit;
            $('level-upper-count-label').textContent=pack?'Pacotes por '+($('emb').value||'embalagem'):unit?'Unidades por '+($('emb').value||'embalagem'):'Sem conversão';
            $('unit-migration').hidden=!editingProduct||((unit?$('un').value.trim()||'unidade':pack?'pacote':$('emb').value)===editingProduct.Unit);
            try{const levels=read(),base=levels.at(-1).Name;$('level-summary').textContent=levels.map(l=>`1 ${l.Name} = ${l.Factor} ${base} • mínimo: ${l.Minimum} ${l.Name}`).join(' | ');}catch{$('level-summary').textContent='Preencha as quantidades para conferir as conversões.';}
        }
        section.addEventListener('input',()=>{dirty=true;update();});section.addEventListener('change',()=>{dirty=true;update();});
        function load(product){
            const levels=product.ControlLevels||[],packs=product.Packages||[],base=product.Unit.toLocaleLowerCase('pt-BR'),baseIsPackage=base==='pacote'||base==='pacotes';
            const packageLevel=levels.find(l=>l.Kind==='package'),unitLevel=levels.find(l=>l.Kind==='unit'),upperLevel=levels.find(l=>l.Kind==='upper');
            const upper=upperLevel||packs.find(p=>['caixa','fardo'].includes(p.Name.toLocaleLowerCase('pt-BR'))),pack=packageLevel||packs.find(p=>p.Name.toLocaleLowerCase('pt-BR')==='pacote');
            $('emb').value=upper?.Name||'';$('level-package').checked=!!packageLevel||baseIsPackage||!!pack;
            $('level-unit').checked=levels.length?!!unitLevel:!baseIsPackage&&(!upper||base!==upper.Name.toLocaleLowerCase('pt-BR'));
            $('un').value=unitLevel?.Name||product.Unit;
            const lowerFactor=pack?.Factor||(baseIsPackage?1:1);$('factor').value=upper?upper.Factor/lowerFactor:1;
            $('level-package-count').value=pack?.Factor||1;
            $('min').value=upperLevel?.Minimum??(upper?product.MinimumPackageQuantity??Math.ceil(product.Minimum/upper.Factor):0);
            $('level-package-min').value=packageLevel?.Minimum??(!upper&&baseIsPackage?product.Minimum:0);
            $('level-unit-min').value=unitLevel?.Minimum??(!upper&&!pack&&!baseIsPackage?product.Minimum:0);
            dirty=false;update();
        }
        window.controlSetup={get dirty(){return dirty;},read,load};update();
    }
    async function openLabel(id,quantity=null,entryInfo='',entryContext=null){
        if(!id){alert('Salve o produto antes de imprimir a etiqueta.');return;}
        try{
            let data=await request('barcode',id);const unitData=data,product=inventory.Products.find(p=>p.Id===id);
            const latest=entryContext||[...inventory.Movements].reverse().find(m=>m.ProductId===id&&m.Type==='Entrada');
            const receivedUnits=latest?(latest.Converted??latest.Quantity*latest.Factor):quantity??(product.Packages[0]?.Factor??1);
            const packageCount=pack=>latest&&latest.Factor===pack.Factor?latest.Quantity:Math.max(1,Math.ceil(receivedUnits/pack.Factor));
            const resetRegistration=current===2&&lastSavedProduct===id;
            const initialPackage=latest?.Factor>1?product.Packages.findIndex(p=>p.Factor===latest.Factor):-1;
            quantity??=receivedUnits;document.querySelector('#label-dialog')?.remove();
            const dialog=document.createElement('dialog');dialog.id='label-dialog';
            dialog.innerHTML=`<h2>Etiquetas do produto</h2><p class="muted" id="label-entry-info"></p><div class="label-options"><div><label for="label-count">Quantidade de etiquetas</label><input id="label-count" type="number" min="1" max="10000" step="1"></div><div><label>Papel</label><input value="A4 — várias etiquetas por folha" readonly></div></div><div class="label-options"><div><label for="label-size">Formato</label><select id="label-size"><option value="30,20">Compacto: 30 × 20 mm</option><option value="40,20">40 × 20 mm</option><option value="60,40">60 × 40 mm</option><option value="80,50">80 × 50 mm</option><option value="100,50">100 × 50 mm</option><option value="custom">Personalizado</option></select></div><div><label for="label-width">Largura (mm)</label><input id="label-width" type="number" min="25" max="200" step="0.1" value="30"></div><div><label for="label-height">Altura (mm)</label><input id="label-height" type="number" min="15" max="280" step="0.1" value="20"></div></div><div class="label-options"><div><label for="label-gap">Espaço entre etiquetas (mm)</label><input id="label-gap" type="number" min="0" max="15" step="0.1" value="1"></div><div><label for="label-margin">Margem da folha A4 (mm)</label><input id="label-margin" type="number" min="0" max="30" step="0.1" value="5"></div></div><p class="small muted">As etiquetas são distribuídas em linhas e colunas na folha A4. O formato compacto de 30 × 20 mm permite até 78 etiquetas por folha. Imprima uma única cópia: a quantidade já está distribuída no documento.</p><p id="label-layout-summary" class="status"></p><div id="label-print-area"></div><button type="button" class="action" id="label-print">Gerar PDF</button><button type="button" class="action secondary" id="label-close">Fechar</button>`;
            document.body.append(dialog);$('label-entry-info').textContent=entryInfo||'Confira a quantidade, o papel e as medidas antes de imprimir.';$('label-count').value=quantity;
            const targetBox=document.createElement('div');targetBox.innerHTML='<label for="label-target">Onde a etiqueta será colocada?</label><select id="label-target"></select><p id="label-target-note" class="source-note"></p>';
            $('label-entry-info').after(targetBox);
            $('label-target').add(new Option('Em cada produto avulso — '+receivedUnits+' unidades','unit'));
            product.Packages.forEach((p,i)=>$('label-target').add(new Option('Em cada '+p.Name.toLocaleLowerCase('pt-BR')+' — '+packageCount(p)+' '+p.Name.toLocaleLowerCase('pt-BR'),String(i))));
            let saved={};try{saved=await request('getLabelProfile')||{};}catch{}
            if(saved.layoutVersion!==3)saved={width:30,height:20,gap:1,margin:5};
            for(const key of ['width','height','gap','margin'])if(saved[key]!==undefined)$('label-'+key).value=saved[key];
            $('label-size').value=[...$('label-size').options].some(o=>o.value===$('label-width').value+','+$('label-height').value)?$('label-width').value+','+$('label-height').value:'custom';
            let layout,labelLandscape=false;
            window.desktopOrientLabels=landscape=>{labelLandscape=landscape;draw();return layout;};
            function draw(){
                const count=Number($('label-count').value),w=Number($('label-width').value),h=Number($('label-height').value),gap=Number($('label-gap').value),margin=Number($('label-margin').value),media='a4';
                layout=null;const area=$('label-print-area');area.replaceChildren();
                if(!Number.isInteger(count)||count<1||count>10000||w<25||w>200||h<15||h>280||gap<0||gap>15||margin<0||margin>30){$('label-layout-summary').textContent='Informe quantidade inteira entre 1 e 10.000 e medidas válidas.';return;}
                const pageWidth=labelLandscape?297:210,pageHeight=labelLandscape?210:297;
                const cols=Math.floor((pageWidth-2*margin+gap)/(w+gap));
                const rows=Math.floor((pageHeight-2*margin+gap)/(h+gap));
                if(cols<1||rows<1){$('label-layout-summary').textContent='A etiqueta não cabe no papel com as margens informadas.';return;}
                const capacity=cols*rows,pages=Math.ceil(count/capacity);
                layout={labels:true,width:pageWidth,height:pageHeight,count,media,columns:cols,rows,pages};
                $('label-layout-summary').textContent=`${count} etiqueta(s) • ${cols} coluna(s) × ${rows} linha(s) • ${pages} folha(s) A4 • Até ${capacity} etiquetas por folha`;
                for(let page=0;page<pages;page++){
                    const sheet=document.createElement('div');sheet.className='label-sheet';sheet.style.cssText=`width:${pageWidth}mm;height:${pageHeight}mm;padding:${media==='a4'?margin:0}mm;display:grid;grid-template-columns:repeat(${cols},${w}mm);grid-auto-rows:${h}mm;gap:${gap}mm;align-content:start;justify-content:start;box-sizing:border-box;background:white;flex:none`;
                    for(let i=page*capacity;i<Math.min(count,(page+1)*capacity);i++){
                        const label=document.createElement('div');label.className='barcode-label';label.style.cssText=`width:${w}mm;height:${h}mm;padding:${h<25?1:2}mm;gap:0.4mm;break-after:auto`;
                        const name=document.createElement('strong');name.textContent=data.name;name.style.cssText=`font-size:${h<25?7:10}pt;max-height:${h<25?4:9}mm;line-height:1.1`;
                        const image=document.createElement('img');image.src='data:image/svg+xml;base64,'+btoa(data.svg);image.alt='Código '+data.code;image.style.width=(w-(h<25?2:4))+'mm';image.style.height=Math.max(7,Math.min(15,h-11))+'mm';
                        const code=document.createElement('code');code.textContent=data.code;code.style.fontSize=h<25?'7pt':'10pt';
                        label.append(name);
                        if(data.packageLabel){const packText=document.createElement('small');packText.textContent=data.packageLabel;packText.style.cssText='font-size:7pt;line-height:1.05;font-weight:600';label.append(packText);image.style.height=Math.max(7,Math.min(15,h-14))+'mm';}
                        label.append(image,code);if(h>=30){const place=document.createElement('small');place.textContent=data.location||data.unit;label.append(place);}sheet.append(label);
                    }
                    area.append(sheet);
                }
                let paper=$('label-paper');if(!paper){paper=document.createElement('style');paper.id='label-paper';document.head.append(paper);}
                paper.textContent=`@media print{@page{size:${pageWidth}mm ${pageHeight}mm;margin:0}body.printing-labels .label-sheet{break-after:page;break-inside:avoid;margin:0!important}body.printing-labels .label-sheet:last-child{break-after:auto}body.printing-labels .barcode-label{break-after:auto!important}}`;
                clearTimeout(window.labelProfileTimer);window.labelProfileTimer=setTimeout(()=>request('saveLabelProfile',{layoutVersion:3,media:'a4',width:w,height:h,gap,margin}).catch(()=>{}),300);
            }
            function cleanup(){document.body.classList.remove('printing-labels');$('label-paper')?.remove();}
            function finish(){cleanup();if(resetRegistration&&current===2&&lastSavedProduct===id){lastSavedProduct=null;show(2);}}
            for(const key of ['count','width','height','gap','margin'])$('label-'+key).oninput=()=>{if(key==='width'||key==='height')$('label-size').value='custom';draw();};
            $('label-size').onchange=()=>{if($('label-size').value!=='custom'){const [w,h]=$('label-size').value.split(',');$('label-width').value=w;$('label-height').value=h;}draw();};
            $('label-close').onclick=()=>{cleanup();dialog.close();};dialog.addEventListener('close',finish);dialog.addEventListener('cancel',finish);
            async function print(){
                draw();if(!layout){alert('Revise a quantidade e o tamanho das etiquetas.');return;}
                await Promise.all([...dialog.querySelectorAll('img')].map(img=>img.decode()));document.body.classList.add('printing-labels');
                try{if(await request('print',layout)){dialog.close();finish();}}catch(e){alert(e.message);}finally{document.body.classList.remove('printing-labels');}
            }
            $('label-print').onclick=()=>print();
            async function selectTarget(){
                $('label-print').disabled=true;$('label-target').disabled=true;
                try{
                    const target=$('label-target').value;
                    if(target==='unit'){
                        data=unitData;$('label-count').value=receivedUnits;
                        $('label-target-note').textContent=`${receivedUnits} etiquetas iguais para os produtos avulsos. O código é o mesmo da caixa. Selecione a unidade e a quantidade na movimentação.`;
                    }else{
                        const index=Number(target),pack=product.Packages[index];
                        data={...unitData,packageLabel:pack.Name+' c/ '+pack.Factor};
                        const count=packageCount(pack);
                        $('label-count').value=Math.max(1,count);
                        $('label-target-note').textContent=`${Math.max(1,count)} etiquetas iguais para ${pack.Name}. O código é o mesmo dos produtos avulsos. Selecione a embalagem e a quantidade na movimentação.`;
                    }
                    draw();
                }catch(e){alert(e.message);layout=null;}
                finally{$('label-target').disabled=false;$('label-print').disabled=!layout;}
            }
            $('label-target').onchange=selectTarget;$('label-target').value=initialPackage>=0?String(initialPackage):'unit';
            dialog.showModal();await selectTarget();
        }catch(e){alert(e.message);}
    }
    function openLinkCode(code){
        document.querySelector('#link-code-dialog')?.remove();
        const dialog=document.createElement('dialog');dialog.id='link-code-dialog';
        dialog.innerHTML='<h2>Vincular código a um produto existente</h2><p>Novo código: <strong id="link-new-code"></strong></p><label for="link-product-search">Buscar por nome, categoria ou código</label><input id="link-product-search" placeholder="Ex.: Café"><label for="link-product">Selecione um produto cadastrado</label><select id="link-product" size="6" aria-label="Produtos cadastrados"></select><p id="link-product-details" class="source-note">Escolha o produto ao qual esse código pertence.</p><p class="small muted">Confira se é a mesma apresentação e unidade. Este vínculo identifica uma unidade de controle; se o código for de caixa ou fardo, cadastre a embalagem com o fator correto. O código antigo continuará válido.</p><p id="link-error" role="alert" style="color:#a33232"></p><button type="button" class="action" id="link-save" disabled>Salvar vínculo</button><button type="button" class="action secondary" id="link-cancel">Cancelar</button>';
        document.body.append(dialog);$('link-new-code').textContent=code;
        function options(){
            const query=$('link-product-search').value.trim().toLocaleLowerCase('pt-BR');
            const matches=inventory.Products.filter(p=>(p.Name+' '+p.Code+' '+(p.CodeAliases||[]).join(' ')+' '+p.Category).toLocaleLowerCase('pt-BR').includes(query)).sort((a,b)=>a.Name.localeCompare(b.Name,'pt-BR'));
            $('link-product').replaceChildren();
            for(const p of matches){const option=new Option(`${p.Name} • ${p.Category} • ${p.Unit} • Saldo: ${p.Balance} • Código: ${p.Code}${p.Active?'':' • INATIVO'}`,p.Id);option.disabled=!p.Active;$('link-product').add(option);}
            $('link-product').selectedIndex=-1;$('link-save').disabled=true;$('link-product-details').textContent=matches.length?'Escolha o produto ao qual esse código pertence.':'Nenhum produto encontrado para esta busca.';
        }
        $('link-product-search').oninput=options;
        $('link-product').onchange=()=>{const p=inventory.Products.find(p=>p.Id===$('link-product').value);$('link-save').disabled=!p?.Active;$('link-product-details').textContent=p?`Produto: ${p.Name} • Cada leitura deste novo código representa 1 ${p.Unit}. Saldo atual: ${p.Balance} ${p.Unit}. Códigos vinculados: ${[p.Code,...(p.CodeAliases||[])].join(', ')}`:'Selecione um produto.';};
        $('link-cancel').onclick=()=>dialog.close();
        $('link-save').onclick=async()=>{
            const productId=$('link-product').value;if(!productId)return;
            $('link-save').disabled=true;$('link-cancel').disabled=true;$('link-error').textContent='';
            try{
                sync(await request('linkCode',{productId,code}));dialog.close();
                if(current===3||current===4){$('codigo').value=code;buscar();$('msg').textContent='Código vinculado ao produto. Confira os dados e confirme a movimentação quando estiver pronto.';$('q').focus();$('q').select();}
            }catch(error){$('link-error').textContent=error.message;}
            finally{if(!dialog.isConnected)return;$('link-save').disabled=false;$('link-cancel').disabled=false;}
        };
        options();dialog.showModal();$('link-product-search').focus();
    }
    atualizaLista=function(){const query=$('busca').value.toLocaleLowerCase('pt-BR');$('lista').innerHTML=table(produtos.filter(p=>(p.nome+' '+p.code+' '+p.aliases.join(' ')).toLocaleLowerCase('pt-BR').includes(query)));};
    function setupScanner(){
        clearTimeout(scanTimer);const input=$('codigo');input.autocomplete='off';input.spellcheck=false;input.placeholder='Digite ou bipe o código de barras';
        const reset=()=>{selected=null;$('formmov').classList.add('hide');$('produto').replaceChildren();};
        input.onkeydown=e=>{if(e.key==='Enter'||e.key==='Tab'){e.preventDefault();clearTimeout(scanTimer);buscar();if(selected){$('q').focus();$('q').select();}}};
        input.addEventListener('input',()=>{reset();clearTimeout(scanTimer);const code=input.value.trim();scanTimer=setTimeout(()=>{if(current!==3&&current!==4)return;if(produtos.some(p=>p.code===code||p.aliases.includes(code)||p.packages.some(pk=>pk.Code&&pk.Code===code)))buscar();},350);});
        input.addEventListener('focus',()=>input.select());requestAnimationFrame(()=>input.focus());
    }
    function addContextMenu() {
        const menu = document.createElement('div');
        menu.id='desktop-menu'; menu.style.cssText='display:none;position:fixed;z-index:1000;background:white;border:1px solid #dce2ec;border-radius:8px;padding:6px;box-shadow:0 5px 20px #1a2c5033';
        for(const label of ['Exportar tabela para Excel (CSV)','Imprimir / Salvar em PDF']) {
            const b = document.createElement('button'); b.textContent=label; b.style.cssText='display:block;border:0;background:white;padding:11px 14px;color:#20366c;text-align:left;width:100%;cursor:pointer';
            b.onclick=()=>{menu.style.display='none'; if(label.startsWith('Exportar')) exportTable(); else request('print').catch(e=>alert(e.message));};
            menu.append(b);
        }
        document.body.append(menu);
        document.addEventListener('contextmenu',e=>{if(e.target.closest('input,textarea,[contenteditable="true"]')){menu.style.display='none';return;}e.preventDefault();menu.style.left=Math.min(e.clientX,window.innerWidth-310)+'px';menu.style.top=Math.min(e.clientY,window.innerHeight-155)+'px';menu.style.display='block';});
        document.addEventListener('click',()=>menu.style.display='none');
        document.addEventListener('keydown',e=>{ if(e.key==='Escape') menu.style.display='none'; });
    }
    async function exportTable() {
        const tables=[...document.querySelectorAll('#page table')];
        const grid=tables.at(-1); if(!grid) {alert('Esta tela não possui tabela para exportação.');return;}
        const rows=[...grid.rows].map(r=>[...r.cells].map(c=>c.innerText));
        try{await request('export',rows);}catch(e){alert(e.message);}
    }
    function preparePdf(){
        cleanupPdf();
        document.querySelector('.pdf-filter-summary')?.remove();
        if(current===0){
            const summary=document.createElement('div');summary.className='pdf-filter-summary';
            summary.textContent=`Estoque: posição atual. Resumo financeiro: ${String(financeMonth).padStart(2,'0')}/${financeYear}, pela data da compra.`;
            document.querySelector('.dashboard-heading').after(summary);
        }
        if(current===6){
            const date=s=>s?s.split('-').reverse().join('/'):'Sem limite';
            const summaryFor=tab=>{const summary=document.createElement('div');summary.className='pdf-filter-summary';summary.textContent=`Relatório: ${tab}\nPeríodo: ${date(inicioRel)} a ${date(fimRel)}    •    Local: ${localRel||'Todos os locais'}    •    Categoria: ${categoriaRel||'Todas as categorias'}`;return summary;};
            const previous=abaRel,main=document.querySelector('main'),scrollY=main.scrollTop,root=document.createElement('div');root.id='all-reports-pdf';
            try{
                for(const tab of ['Resumo','Gastos','Economia','Consumo','Estoque','Preços','Locais de compra']){
                    abaRel=tab;desenhaRelatorio();
                    const section=document.createElement('section');section.className='pdf-report-section';section.dataset.report=tab;
                    const brand=document.querySelector('.bar').cloneNode(true);brand.querySelector('.badge-top')?.remove();
                    const title=document.createElement('h1');title.textContent='Relatórios de estoque — '+tab;
                    const content=$('relconteudo').cloneNode(true);content.removeAttribute('id');
                    section.append(brand,title,summaryFor(tab),content);root.append(section);
                }
            }finally{abaRel=previous;desenhaRelatorio();main.scrollTop=scrollY;}
            const panel=document.querySelector('#page .panel');panel.classList.add('report-filter-panel');
            const summary=summaryFor(previous);
            panel.after(summary);
            document.body.append(root);document.body.classList.add('printing-all-reports');
        }
    }
    function cleanupPdf(){document.body.classList.remove('printing-all-reports');$('all-reports-pdf')?.remove();}
    window.print=()=>request('print').catch(e=>alert(e.message));
    function stockText(product){
        const pack=product.Packages[0],balance=product.Balance;
        const name=(value,count)=>count===1?value:value.endsWith('s')?value:value+'s';
        if(!pack||pack.Factor<=1)return `${balance} ${name(product.Unit,balance)}`;
        if(product.Packages.length===1){const count=balance>0?Math.ceil(balance/pack.Factor):0;return `${count} ${name(pack.Name.toLocaleLowerCase('pt-BR'),count)} (${balance} ${name(product.Unit,balance)})`;}
        let remaining=balance;const pieces=[];
        for(const pack of [...product.Packages].filter(p=>p.Factor>1).sort((a,b)=>b.Factor-a.Factor)){
            const count=Math.floor(remaining/pack.Factor);
            if(count){pieces.push(`${count} ${name(pack.Name.toLocaleLowerCase('pt-BR'),count)}`);remaining-=count*pack.Factor;}
        }
        if(remaining||!pieces.length)pieces.push(`${remaining} ${name(product.Unit,remaining)}`);
        return pieces.join(' + ')+(pieces.length>1||remaining===0&&balance>0?` (${balance} ${name(product.Unit,balance)})`:'');
    }
    function minimumLevelsText(levels){
        const text=l=>`${l.Minimum} ${l.Name.toLocaleLowerCase('pt-BR').endsWith('s')||l.Minimum===1?l.Name.toLocaleLowerCase('pt-BR'):l.Name.toLocaleLowerCase('pt-BR')+'s'}`;
        if(levels.length===1)return text(levels[0]);
        return levels.slice(0,-1).map(text).join(' + ')+' ('+text(levels.at(-1))+')';
    }
    table=function(ps) {
        ps=[...ps].sort((a,b)=>a.nome.localeCompare(b.nome,'pt-BR',{sensitivity:'base',numeric:true}));
        if(!ps.length) return '<table><tr><th>Produto</th><th>Categoria</th><th>Saldo</th><th>Mínimo</th><th>Situação</th></tr><tr><td colspan="5">Nenhum produto encontrado.</td></tr></table>';
        const container=document.createElement('div');container.innerHTML=referenceTable(ps);
        const rows=[...container.querySelectorAll('tr')];if(current!==0)rows[0].insertAdjacentHTML('beforeend','<th>Ações</th>');
        rows.slice(1).forEach((row,i)=>{const product=inventory.Products.find(p=>p.Id===ps[i].id);if(product){if(product.Balance===0){const status=document.createElement('span');status.textContent='Em falta';status.style.cssText='display:inline-block;color:#b42318;background:#fee4e2;border-radius:20px;padding:7px 10px';row.cells[4].replaceChildren(status);}else if(product.Balance>product.Minimum){const status=document.createElement('span');status.textContent='Normal';status.style.cssText='display:inline-block;color:#067647;background:#dcfae6;border-radius:20px;padding:7px 10px';row.cells[4].replaceChildren(status);}row.cells[2].textContent=stockText(product);if(current===1){const code=document.createElement('small');code.className='product-barcode';code.textContent='Código: '+product.Code;code.style.cssText='display:block;margin-top:4px;font:12px Consolas,monospace;overflow-wrap:anywhere';row.cells[0].append(code);}const pack=product.Packages[0];row.cells[3].textContent=product.ControlLevels?.length?minimumLevelsText(product.ControlLevels):product.MinimumPackageQuantity!==null&&product.MinimumPackageQuantity!==undefined&&pack?`${product.MinimumPackageQuantity} ${pack.Name.toLocaleLowerCase('pt-BR')} (${product.Minimum} ${product.Unit})`:`${product.Minimum} ${product.Unit}`;}if(current===0)return;const cell=document.createElement('td');cell.innerHTML=`<div class="product-actions"><button type="button" class="label-action" data-label-product="${esc(ps[i].id)}">Imprimir etiqueta</button><button type="button" class="label-action product-icon" data-edit-product="${esc(ps[i].id)}" title="Editar produto" aria-label="Editar ${esc(ps[i].nome)}"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="m16 3 5 5L8 21H3v-5Z M14 5l5 5"/></svg></button><button type="button" class="label-action product-icon product-delete" data-delete-product="${esc(ps[i].id)}" title="Excluir produto" aria-label="Excluir ${esc(ps[i].nome)}"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18"/></svg></button></div>`;if(current!==1)cell.innerHTML=`<button type="button" class="label-action" data-label-product="${esc(ps[i].id)}">Imprimir etiqueta</button>`;row.append(cell);});
        return container.innerHTML;
    };
    render=function() {
        editingProduct=null;
        referenceRender();
        document.querySelector('main').scrollTop=0;
        document.body.dataset.screen=String(current);
        if(current===8) document.querySelector('#page .content > .notice')?.remove();
        if(current===5||current===8){
            const title=page.querySelector('h1'),subtitle=title.nextElementSibling,header=document.createElement('div'),text=document.createElement('div'),button=document.createElement('button');
            header.className='products-heading';title.before(header);text.append(title,subtitle);button.type='button';button.id='page-export-pdf';button.className='action secondary';button.textContent='Exportar PDF';button.onclick=()=>window.print();header.append(text,button);
        }
        if(current===1){
            const title=page.querySelector('h1'),subtitle=title.nextElementSibling,button=page.querySelector('button.action');
            const heading=document.createElement('div'),text=document.createElement('div');heading.className='products-heading';
            title.before(heading);text.append(title,subtitle);heading.append(text,button);
        }
        if(current===0) {
            const title=page.querySelector('h1'),subtitle=title.nextElementSibling,heading=document.createElement('div'),headingText=document.createElement('div'),exportButton=document.createElement('button');
            heading.className='products-heading dashboard-heading';title.before(heading);headingText.append(title,subtitle);exportButton.type='button';exportButton.id='dashboard-export-pdf';exportButton.className='action secondary';exportButton.textContent='Exportar PDF';exportButton.onclick=()=>window.print();heading.append(headingText,exportButton);
            page.querySelector('.grid>.card:last-child')?.classList.add('dashboard-quick-actions');
            const lowTable=[...page.querySelectorAll('.panel')].find(p=>p.querySelector('h2')?.textContent==='Produtos com estoque baixo')?.querySelector('table');
            if(lowTable){const wrapper=document.createElement('div');wrapper.className='low-stock-scroll';lowTable.before(wrapper);wrapper.append(lowTable);}
            dataNotice(document.querySelector('#page > .notice'),'Os dados são salvos automaticamente neste computador. Use os atalhos para registrar entradas e saídas.');
            const financial=[...document.querySelectorAll('#page .panel')].find(x=>x.querySelector('h2')?.textContent==='Resumo financeiro ilustrativo');
            if(financial) {
                financial.querySelector('h2').textContent='Resumo financeiro';
                const explanations=[
                    ['Gasto nas compras','Soma dos valores pagos nas compras do mês e ano selecionados, pela data da compra. Considera os descontos e acréscimos registrados em cada item. Compras sem custo informado não entram na soma e são indicadas na observação abaixo.'],
                    ['Economia comparada à referência','Compara o valor de referência com o valor pago, para a mesma quantidade e unidade: referência total menos valor pago. Só entram compras com referência e custo informados. Resultado positivo indica economia; negativo indica custo adicional. A referência por unidade deve corresponder à unidade de controle do produto.'],
                    ['Local com maior gasto','Estabelecimento em que foi gasto o maior valor em compras no mês e ano selecionados. Considera apenas compras com custo informado. É o local da compra, não o local onde o produto é armazenado.']
                ];
                financial.querySelectorAll('.card').forEach((card,i)=>{
                    card.classList.add('financial-info-card');adicionaAjuda(card,explanations[i][1]);
                    card.querySelector('.info-btn').setAttribute('aria-label','Informação: '+explanations[i][0]);
                });
                const months=['Janeiro','Fevereiro','Março','Abril','Maio','Junho','Julho','Agosto','Setembro','Outubro','Novembro','Dezembro'];
                const filters=document.createElement('div');filters.className='finance-filters';filters.style.cssText='max-width:310px;margin-bottom:18px';
                filters.innerHTML='<label for="finance-period">Mês e ano da compra</label><input type="month" id="finance-period" min="1900-01" max="2070-12" required aria-label="Selecionar mês e ano da compra">';
                financial.querySelector('h2').after(filters);
                $('finance-period').value=financeYear+'-'+String(financeMonth).padStart(2,'0');
                $('finance-period').addEventListener('click',()=>{try{$('finance-period').showPicker();}catch{}});
                function updateFinance(){
                    const period=$('finance-period');
                    if(!period.value||!period.validity.valid)return;
                    const [year,month]=period.value.split('-').map(Number);financeMonth=month;financeYear=year;
                    const key=financeYear+'-'+String(financeMonth).padStart(2,'0'),items=compras.filter(c=>c.data.startsWith(key));
                    const refs=items.filter(c=>c.ref!==null&&c.valor!==null),totals={};
                    items.forEach(c=>{if(c.valor!==null)totals[c.local]=(totals[c.local]||0)+c.valor;});
                    const top=Object.entries(totals).sort((a,b)=>b[1]-a[1])[0],cards=financial.querySelectorAll('.card b');
                    cards[0].textContent=referenceMoney(items.reduce((sum,c)=>sum+(c.valor??0),0));
                    cards[1].textContent=refs.length?referenceMoney(refs.reduce((sum,c)=>sum+c.ref-c.valor,0)):'Não calculada';
                    cards[2].textContent=top?.[0]||'Sem dados';cards[2].title=top?referenceMoney(top[1]):'Nenhuma compra com valor informado neste período';
                    financial.querySelector('p.muted').textContent=`Período: ${months[financeMonth-1]} de ${financeYear} (data da compra). ${items.length} item(ns) de compra; ${items.filter(c=>c.unknown).length} sem custo conhecido. Economia calculada em ${refs.length} de ${items.length} itens com referência. Valores negativos indicam custo adicional.`;
                }
                $('finance-period').onchange=updateFinance;updateFinance();
            }
        }
        if(current===2) {
            const numbers=[...document.querySelectorAll('#page input[type=number]')];
            const ref=numbers.find(x=>!x.id); if(ref) ref.id='reference-price';
            const source=[...document.querySelectorAll('#page input')].find(x=>x.placeholder==='Ex.: orçamento em 10/10/2026'); if(source) source.id='reference-origin';
            const note=[...document.querySelectorAll('#page p')].find(x=>x.textContent.startsWith('Campos ilustrativos:')); if(note) note.textContent='O preço de referência e a origem serão salvos no cadastro.';
            arrangeRegistration();
        }
        if(current===3||current===4) {
            dataNotice(document.querySelector('#page > .notice'),'Digite o código e pressione Enter ou bipe com o leitor em modo teclado. Aceita Enter, Tab ou leitura completa sem sufixo. F8 retorna ao campo do código.');
            setupScanner();
            $('q').type='text';$('q').inputMode='numeric';
            if(current===4){
                $('obs').previousElementSibling.remove();$('obs').remove();
                const unitLabel=$('tipoemb').previousElementSibling,quantityLabel=$('q').previousElementSibling;
                quantityLabel.before(unitLabel,$('tipoemb'));
            }
            if(current===3) {
                for(const key of ['setor','obs','ref']){const field=$(key);field.previousElementSibling?.remove();field.remove();}
                $('preco').previousElementSibling.textContent='Valor total da compra (R$)';
                for(const key of ['q','preco','desconto','acrescimo']){$(key).type='text';$(key).inputMode=key==='q'?'numeric':'decimal';}
                const discountRow=document.createElement('div');discountRow.style.cssText='display:flex;gap:12px;align-items:center';
                $('desconto').before(discountRow);discountRow.append($('desconto'));
                const percent=document.createElement('output');percent.id='discount-percent';percent.setAttribute('aria-live','polite');percent.style.cssText='white-space:nowrap;min-width:65px;color:#20366c';discountRow.append(percent);
                $('preco').insertAdjacentHTML('afterend','<label for="entry-unit-cost">Custo por unidade de controle (calculado)</label><input id="entry-unit-cost" readonly>');
                $('q').previousElementSibling.textContent='Quantidade comprada (na unidade ou embalagem selecionada)';
                $('datacompra').value=localDate(new Date()); $('datarecebimento').value=localDate(new Date());
                const invoice=[...document.querySelectorAll('#page input')].find(x=>x.placeholder==='Número da nota'); if(invoice) invoice.id='invoice';
                const additionRow=document.createElement('div');additionRow.style.cssText='display:flex;gap:12px;align-items:center';
                $('acrescimo').before(additionRow);additionRow.append($('acrescimo'));
                const additionPercent=document.createElement('output');additionPercent.id='addition-percent';additionPercent.setAttribute('aria-live','polite');additionPercent.style.cssText='white-space:nowrap;min-width:65px;color:#20366c';additionRow.append(additionPercent);
                const grid=document.querySelector('#formmov>.two'),columns=[];
                for(const keys of [['tipoemb','q','localcompra','datacompra','datarecebimento','invoice'],['pessoa','preco','entry-unit-cost','desconto','acrescimo']]){
                    const column=document.createElement('div');
                    for(const key of keys){
                        const input=$(key),control=['desconto','acrescimo'].includes(key)?input.parentElement:input,label=control.previousElementSibling;
                        const field=document.createElement('div');field.style.marginBottom='12px';field.append(label,control);
                        if(key==='q')field.append($('calc'));
                        column.append(field);
                    }
                    columns.push(column);
                }
                const summary=$('financeiro');grid.replaceChildren(...columns);grid.after(summary);
                calcFinanceiro();
            }
        }
        if(current===5) enhanceHistory();
        for(const table of document.querySelectorAll('#page table')){
            if(table.closest('.tablewrap,.low-stock-scroll,.responsive-table'))continue;
            const wrap=document.createElement('div');wrap.className='responsive-table';table.before(wrap);wrap.append(table);
        }
    };
    function collapsibleHistory(panel,id){
        const heading=panel.firstElementChild,bar=document.createElement('div'),content=document.createElement('div'),toggle=document.createElement('button');
        bar.style.cssText='display:flex;align-items:center;justify-content:space-between;gap:16px';
        content.id=id;content.className='history-collapsible-content';content.hidden=true;
        toggle.type='button';toggle.className='history-toggle';toggle.setAttribute('aria-controls',id);toggle.setAttribute('aria-expanded','false');
        toggle.style.cssText='border:0;background:transparent;color:#20366c;cursor:pointer;padding:8px;flex:none';
        const title=heading.tagName==='H2'?heading.textContent:'Histórico de movimentações';
        function update(){toggle.innerHTML=content.hidden?'&#9660;':'&#9650;';toggle.setAttribute('aria-expanded',String(!content.hidden));toggle.setAttribute('aria-label',(content.hidden?'Expandir ':'Recolher ')+title);}
        toggle.onclick=()=>{content.hidden=!content.hidden;update();};
        for(const child of [...panel.childNodes])if(child!==heading)content.append(child);
        bar.append(heading,toggle);panel.replaceChildren(bar,content);update();
    }
    function enhanceHistory() {
        const note=document.querySelector('#page .panel p.muted');
        if(note) note.textContent='Movimentações registradas no computador. Consulte por período, produto, tipo, responsável e setor.';
        const panel=document.querySelector('#page .panel'); if(!panel)return;
        const filters=document.createElement('details'); filters.className='history-filter-controls';filters.style.marginBottom='16px';
        filters.innerHTML='<summary style="cursor:pointer;color:#20366c">Filtrar movimentações</summary><div class="filtergrid"><div><label>Data inicial</label><input type="date" id="history-start"></div><div><label>Data final</label><input type="date" id="history-end"></div><div><label>Tipo</label><select id="history-type"><option>Todos</option><option>Entrada</option><option>Saída</option><option>Ajuste +</option><option>Ajuste −</option></select></div><div><label>Produto</label><input id="history-product"></div><div><label>Responsável</label><input id="history-person"></div><div><label>Setor</label><input id="history-sector"></div></div>';
        panel.insertBefore(filters,panel.querySelector('table'));
        function filter() {
            const data=inventory.Movements.filter(m=>(!$('history-start').value||m.Date.slice(0,10)>=$('history-start').value)&&(!$('history-end').value||m.Date.slice(0,10)<=$('history-end').value)&&($('history-type').value==='Todos'||m.Type===$('history-type').value)&&m.ProductName.toLowerCase().includes($('history-product').value.toLowerCase())&&m.Person.toLowerCase().includes($('history-person').value.toLowerCase())&&m.Sector.toLowerCase().includes($('history-sector').value.toLowerCase())).sort((a,b)=>b.Date.localeCompare(a.Date));
            panel.querySelector('table').outerHTML='<table><tr><th>Data</th><th>Tipo</th><th>Produto</th><th>Quantidade</th><th>Responsável / setor</th></tr>'+data.map(m=>`<tr><td>${esc(new Date(m.Date).toLocaleString('pt-BR'))}</td><td>${esc(m.Type)}</td><td>${esc(m.ProductName)}</td><td>${m.Converted} ${esc(m.Unit)}${m.Parts?.length>1?`<small style="display:block">${esc(m.Package)}</small>`:''}</td><td>${esc(m.Person)} / ${esc(m.Sector)}</td></tr>`).join('')+'</table>';
        }
        filters.querySelectorAll('input,select').forEach(c=>c.addEventListener('input',filter));filter();
        enhancePurchaseHistory();
        collapsibleHistory(panel,'movement-history-content');
    }
    function enhancePurchaseHistory(){
        const panel=[...page.querySelectorAll('.panel')].find(p=>p.querySelector('h2')?.textContent==='Histórico de compras por local');if(!panel)return;
        const controls=document.createElement('div');controls.className='history-filter-controls';controls.style.marginBottom='16px';
        controls.innerHTML='<div class="filtergrid"><div><label for="purchases-start">Data inicial da compra</label><input type="date" id="purchases-start"></div><div><label for="purchases-end">Data final da compra</label><input type="date" id="purchases-end"></div><div><label for="purchases-store">Local da compra</label><select id="purchases-store"><option value="">Todos os locais</option></select></div><div><label for="purchases-product">Produto ou código</label><input id="purchases-product" placeholder="Digite para pesquisar"></div></div><button type="button" class="action secondary" id="purchases-clear">Limpar filtros</button>';
        panel.querySelector('h2').after(controls);
        [...new Set(compras.map(c=>c.local))].sort((a,b)=>a.localeCompare(b,'pt-BR')).forEach(store=>$('purchases-store').add(new Option(store,store)));
        for(const [key,id]of Object.entries({start:'purchases-start',end:'purchases-end',product:'purchases-product',store:'purchases-store'}))$(id).value=purchaseHistoryFilters[key];
        const note=document.createElement('p');note.className='muted small';note.setAttribute('aria-live','polite');controls.after(note);
        const printSummary=document.createElement('p');printSummary.className='purchase-history-print-summary';note.after(printSummary);
        function apply(){
            Object.assign(purchaseHistoryFilters,{start:$('purchases-start').value,end:$('purchases-end').value,product:$('purchases-product').value.trim(),store:$('purchases-store').value});
            const f=purchaseHistoryFilters,query=f.product.toLocaleLowerCase('pt-BR'),invalid=f.start&&f.end&&f.start>f.end;
            const rows=inventory.Movements.filter(m=>{
                if(m.Type!=='Entrada'||invalid)return false;
                const p=inventory.Products.find(p=>p.Id===m.ProductId),search=m.ProductName+' '+(p?.Code||'')+' '+(p?.CodeAliases||[]).join(' ')+' '+(p?.Packages||[]).map(pk=>pk.Code).join(' ');
                return (!f.start||m.PurchaseDate.slice(0,10)>=f.start)&&(!f.end||m.PurchaseDate.slice(0,10)<=f.end)&&(!f.store||(m.Store||'Não informado')===f.store)&&search.toLocaleLowerCase('pt-BR').includes(query);
            }).sort((a,b)=>b.PurchaseDate.localeCompare(a.PurchaseDate)||b.Date.localeCompare(a.Date));
            const table=document.createElement('table');table.id='purchases-history-table';
            table.innerHTML='<thead><tr><th>Data</th><th>Produto</th><th>Local da compra</th><th>Quantidade</th><th>Valor</th><th>Economia</th></tr></thead><tbody>'+rows.map(m=>`<tr><td>${esc(m.PurchaseDate.slice(0,10).split('-').reverse().join('/'))}</td><td>${esc(m.ProductName)}</td><td>${esc(m.Store||'Não informado')}</td><td>${m.Converted} ${esc(m.Unit)}</td><td>${m.Total===null?'Custo desconhecido':referenceMoney(m.Total)}</td><td>${m.Total===null||m.ReferenceTotal===null?'Não calculada':referenceMoney(m.ReferenceTotal-m.Total)}</td></tr>`).join('')+(rows.length?'':'<tr><td colspan="6">Nenhuma compra encontrada para os filtros selecionados.</td></tr>')+'</tbody>';
            panel.querySelector('table').replaceWith(table);
            note.textContent=invalid?'A data inicial deve ser anterior ou igual à data final.':`${rows.length} item(ns) de compra • Total conhecido: ${referenceMoney(rows.reduce((sum,m)=>sum+(m.Total??0),0))}. Filtro pela data da compra.`;
            note.style.color=invalid?'#a33232':'';
            const date=s=>s?s.split('-').reverse().join('/'):'Sem limite';
            printSummary.textContent=`Filtros das compras: ${date(f.start)} a ${date(f.end)} • Local: ${f.store||'Todos os locais'} • Produto/código: ${f.product||'Todos os produtos'}`;
        }
        controls.querySelectorAll('input,select').forEach(c=>c.addEventListener('input',apply));
        $('purchases-clear').onclick=()=>{controls.querySelectorAll('input,select').forEach(c=>c.value='');apply();};apply();
        collapsibleHistory(panel,'purchase-history-content');
    }
    function editProduct(id){
        const product=inventory.Products.find(p=>p.Id===id);if(!product)return;
        show(2);editingProduct=product;
        $('page').querySelector('h1').textContent='Editar produto';
        document.querySelector('.registration-panel button.action').textContent='Salvar alterações';
        const fields={code:product.Code,nome:product.Name,cat:product.Category,un:product.Unit,min:product.MinimumPackageQuantity??product.Minimum,local:product.Location,emb:product.Packages[0]?.Name||'',factor:product.Packages[0]?.Factor||1};
        for(const [key,value] of Object.entries(fields)){
            const field=$(key);if(field.tagName==='SELECT'&&value&&![...field.options].some(o=>o.value===String(value)))field.add(new Option(value,value));field.value=value;
        }
        product.Packages.slice(1).forEach(pack=>window.addRegistrationPackage(pack));
        window.updateRegistrationPurchase();window.controlSetup.load(product);
        const cancel=document.createElement('button');cancel.type='button';cancel.className='action secondary';cancel.textContent='Cancelar edição';cancel.onclick=()=>show(1);$('msg').before(cancel);
    }
    async function deleteProduct(id){
        const product=inventory.Products.find(p=>p.Id===id);if(!product)return;
        if(!confirm(`Excluir o produto "${product.Name}" e seu cadastro? Ele será removido do estoque. As movimentações anteriores permanecerão no histórico.`))return;
        await save(async()=>{sync(await request('deleteProduct',id));if(lastSavedProduct===id)lastSavedProduct=null;show(1);});
    }
    document.addEventListener('click',event=>{
        const edit=event.target.closest('[data-edit-product]'),remove=event.target.closest('[data-delete-product]');
        if(edit)editProduct(edit.dataset.editProduct);else if(remove)deleteProduct(remove.dataset.deleteProduct);
    });
    cadastrar=function(){save(async()=>{
        const code=$('code').value.trim(),name=$('nome').value.trim();let unit=$('un').value.trim();
        const controlLevels=window.controlSetup.dirty||editingProduct?.ControlLevels?.length?window.controlSetup.read():null;
        if(controlLevels)unit=controlLevels.at(-1).Name;
        const minimum=Number($('min').value),factor=Number($('factor').value),emb=$('emb').value;
        if(!code||!name||!unit||(!controlLevels&&(!Number.isInteger(minimum)||minimum<0||(emb&&(!Number.isInteger(factor)||factor<1)))))throw new Error('Preencha código, nome, unidade e quantidades válidas.');
        const extraPackages=[...document.querySelectorAll('.extra-package-row')].map(row=>({...JSON.parse(row.dataset.original),Name:row.querySelector('.package-name').value.trim(),Factor:Number(row.querySelector('.package-factor').value)}));
        if(!controlLevels&&(extraPackages.some(p=>!p.Name||!Number.isInteger(p.Factor)||p.Factor<1)||(!emb&&extraPackages.length)))throw new Error('Informe a embalagem principal e as conversões inteiras positivas.');
        const convertedMinimum=minimum*(emb?factor:1);
        const unitConversion=editingProduct&&unit!==editingProduct.Unit?Number($('unit-conversion').value):null;
        if(unitConversion!==null&&(!Number.isInteger(unitConversion)||unitConversion<1))throw new Error('Informe quantas novas unidades existem em cada unidade antiga para converter o saldo.');
        const p={...editingProduct,UnitConversionFactor:unitConversion,Code:code,Name:name,Unit:unit,Category:$('cat').value,MinimumPackageQuantity:emb?minimum:null,Minimum:convertedMinimum,Target:Math.max(convertedMinimum,editingProduct?.Target??convertedMinimum),Location:$('local').value.trim(),Active:true,Packages:emb?[{...editingProduct?.Packages[0],Name:emb,Factor:factor,Code:editingProduct?.Packages[0]?.Code||''},...extraPackages]:[]};
        if(controlLevels){
            p.ControlLevels=controlLevels;p.Unit=unit;p.Packages=controlLevels.slice(0,-1).map(level=>({Name:level.Name,Factor:level.Factor,Code:editingProduct?.Packages.find(pack=>pack.Name.toLocaleLowerCase('pt-BR')===level.Name.toLocaleLowerCase('pt-BR'))?.Code||''}));
            p.MinimumPackageQuantity=null;p.Minimum=controlLevels.at(-1).Minimum*controlLevels.at(-1).Factor;p.Target=Math.max(p.Minimum,editingProduct?.Target??p.Minimum);
        }else p.ControlLevels=[];
        sync(await request('saveProduct',p));if(editingProduct){editingProduct=null;show(1);return;}lastSavedProduct=inventory.Products.find(x=>x.Code===code).Id;$('print-saved-label').hidden=false;$('msg').textContent='Produto cadastrado. Você já pode imprimir a etiqueta. Registre o saldo inicial em Entrada.';
    });};
    buscar=function(){
        const code=$('codigo').value.trim(); const p=produtos.find(x=>x.code===code||x.aliases.includes(code)||x.packages.some(pk=>pk.Code&&pk.Code===code));
        if(p)$('codigo').value=p.code;
        referenceSearch(); $('codigo').value=code;
        if(!selected){
            if(!code){$('produto').textContent='Digite ou bipe o código do produto.';return;}
            $('produto').innerHTML='<p>Código <strong>'+esc(code)+'</strong> não encontrado.</p><button type="button" class="action" id="link-existing-product">Vincular a um produto existente</button><button type="button" class="action secondary" id="register-new-product">Cadastrar novo produto</button>';
            $('link-existing-product').onclick=()=>openLinkCode(code);$('register-new-product').onclick=()=>{show(2);$('code').value=code;};return;
        }
        const base=inventory.Products.find(x=>x.Id===selected.id);
        $('tipoemb').innerHTML=`<option value="1">${esc(selected.un)} (unidade de controle)</option>`+base.Packages.map(pk=>`<option value="${pk.Factor}">${esc(pk.Name)} — ${pk.Factor} ${esc(selected.un)}</option>`).join('');
        if(current===4)$('tipoemb').querySelector('option').textContent=selected.un+' avulso (menor item controlado)';
        const pack=base.Packages.findIndex(pk=>pk.Code&&pk.Code===code); if(pack>=0)$('tipoemb').selectedIndex=pack+1;
        if(current===3) calcFinanceiro();
        if(current===4){
            $('mixed-output')?.remove();
            const mixed=document.createElement('div');mixed.id='mixed-output';mixed.style.cssText='margin-top:12px';
            const add=document.createElement('button');add.type='button';add.className='action secondary';add.textContent='+ Combinar outra apresentação';
            add.onclick=()=>{
                const row=document.createElement('div');row.className='output-part';row.style.cssText='display:flex;gap:8px;margin:10px 0';
                const select=$('tipoemb').cloneNode(true);select.removeAttribute('id');select.setAttribute('aria-label','Apresentação adicional');
                const input=document.createElement('input');input.type='text';input.inputMode='numeric';input.value='1';input.setAttribute('aria-label','Quantidade adicional');
                const remove=document.createElement('button');remove.type='button';remove.className='label-action';remove.textContent='×';remove.setAttribute('aria-label','Remover apresentação');remove.onclick=()=>{row.remove();updateMixedOutput();};
                row.append(select,input,remove);mixed.insertBefore(row,add);select.onchange=updateMixedOutput;input.oninput=updateMixedOutput;updateMixedOutput();
            };
            mixed.append(add);$('calc').after(mixed);$('q').addEventListener('input',updateMixedOutput);$('tipoemb').addEventListener('change',updateMixedOutput);
        }
        calc();
    };
    function outputParts(){
        return [{PackageIndex:$('tipoemb').selectedIndex-1,Quantity:Number($('q').value)},...[...document.querySelectorAll('.output-part')].map(row=>({PackageIndex:row.querySelector('select').selectedIndex-1,Quantity:Number(row.querySelector('input').value)}))];
    }
    function updateMixedOutput(){
        const p=inventory.Products.find(p=>p.Id===selected?.id);if(!p)return;
        const total=outputParts().reduce((sum,part)=>sum+part.Quantity*(part.PackageIndex<0?1:p.Packages[part.PackageIndex].Factor),0);
        $('calc').textContent=`Total da saída: ${total} ${p.Unit}.`;
    }
    function moneyInput(id){
        let value=$(id).value.trim();
        if(!value)return 0;
        if(value.includes(','))value=value.replace(/\./g,'').replace(',','.');
        return /^\d+(?:\.\d+)?$/.test(value)?Number(value):NaN;
    }
    calcFinanceiro=function(){
        if(current!==3||!$('preco'))return;
        const quantity=Number($('q').value),factor=Number($('tipoemb').value),total=moneyInput('preco')-moneyInput('desconto')+moneyInput('acrescimo'),units=quantity*factor;
        const price=moneyInput('preco'),discount=moneyInput('desconto');
        if($('discount-percent'))$('discount-percent').textContent=Number.isFinite(price)&&price>0&&Number.isFinite(discount)?(discount/price*100).toLocaleString('pt-BR',{maximumFractionDigits:2})+'%':'—';
        const addition=moneyInput('acrescimo');
        if($('addition-percent'))$('addition-percent').textContent=Number.isFinite(price)&&price>0&&Number.isFinite(addition)?(addition/price*100).toLocaleString('pt-BR',{maximumFractionDigits:2})+'%':'—';
        const valid=Number.isFinite(total)&&total>=0&&units>0;
        if($('entry-unit-cost'))$('entry-unit-cost').value=valid?referenceMoney(total/units):'';
        $('financeiro').textContent=valid?`Total pago: ${referenceMoney(total)} • Quantidade no estoque: ${units} ${selected?.un||''} • Custo por unidade: ${referenceMoney(total/units)}`:'Informe quantidade e valores válidos para calcular o custo por unidade.';
    };
    movimentar=function(){save(async()=>{
        if(!selected)throw new Error('Busque um produto antes de confirmar.');
        const quantity=Number($('q').value),factor=Number($('tipoemb').value),person=$('pessoa').value.trim();
        if(!Number.isInteger(quantity)||(current===3?quantity<=0:quantity<0)||!person)throw new Error('Informe uma quantidade inteira positiva e o responsável.');
        const p=inventory.Products.find(x=>x.Id===selected.id);
        const m={ProductId:p.Id,Type:current===3?'Entrada':'Saída',Date:new Date().toISOString(),Quantity:quantity,Factor:factor,Person:person,Sector:current===3?'':$('setor').value.trim(),Note:'',Package:$('tipoemb').selectedOptions[0].textContent,ReferenceSource:p.ReferenceSource,ReferenceDate:p.ReferenceDate};
        if(current===4){
            const parts=outputParts();
            if(parts.some(part=>!Number.isInteger(part.Quantity)||part.Quantity<0)||!parts.some(part=>part.Quantity>0))throw new Error('Informe quantidades inteiras positivas nas apresentações retiradas.');
            m.Parts=parts.filter(part=>part.Quantity>0);
        }
        if(current===3){
            const price=moneyInput('preco'),discount=moneyInput('desconto'),addition=moneyInput('acrescimo');
            const total=price-discount+addition;
            if(!$('localcompra').value.trim()||!$('datacompra').value||![price,discount,addition,total].every(Number.isFinite)||price<0||discount<0||addition<0||total<0)throw new Error('Informe local, data e valores válidos.');
            m.Store=$('localcompra').value.trim();m.PurchaseDate=$('datacompra').value+'T00:00:00';
            m.Date=($('datarecebimento').value||localDate(new Date()))+'T'+new Date().toTimeString().slice(0,8);
            m.Total=total;m.ReferenceTotal=null;m.Invoice=$('invoice')?.value.trim()||'';
        }
        const id=selected.id;sync(await request('move',m));const updated=produtos.find(p=>p.id===id);
        $('msg').textContent=`Movimentação registrada. Novo saldo: ${updated.saldo} ${updated.un}.`;
        $('formmov').classList.add('hide');$('produto').innerHTML='';selected=null;
        $('codigo').value='';requestAnimationFrame(()=>$('codigo')?.focus());
    });};
    desenhaRelatorio=function(){
        referenceReports();
        dataNotice(document.querySelector('#page > .notice'),'Os relatórios utilizam os dados salvos no computador. Selecione o período, o local e a categoria para consultar.');
        const filterNote=document.querySelector('#page .section-note');if(filterNote)filterNote.textContent='Datas filtram compras e saídas. Categoria também filtra o estoque atual. O local filtra somente compras, pois não é atributo das saídas nem do saldo. Estoque é uma posição atual.';
        const replacements=[['Saldo atual ao custo ilustrativo','Saldo atual ao custo médio'],['Registros de exemplo','Itens registrados'],['Valores ilustrativos das saídas.','Custo médio registrado em cada saída.'],['Posição ilustrativa, não é filtrada por data ou local de compra.','Posição atual, não é filtrada por data ou local de compra.']];
        for(const el of document.querySelectorAll('#relconteudo span,#relconteudo p.section-note'))for(const [from,to]of replacements)if(el.textContent===from)el.textContent=to;
        const source=[...document.querySelectorAll('#relconteudo p.source-note')].find(p=>p.textContent.startsWith('As saídas de exemplo'));if(source)source.textContent='Cada saída preserva o custo médio aplicado no momento do registro. Custos desconhecidos não entram na soma.';
        const visiblePurchases=compras.filter(c=>(!inicioRel||c.data>=inicioRel)&&(!fimRel||c.data<=fimRel)&&(!localRel||c.local===localRel)&&(!categoriaRel||c.cat===categoriaRel));
        const unknownPurchases=visiblePurchases.filter(c=>c.unknown).length;
        const unknownStock=estoqueDemo.filter(p=>(!categoriaRel||p.cat===categoriaRel)&&p.q>0&&p.custo===null).length;
        const unknownOutputs=saidasDemo.filter(m=>(!inicioRel||m.data>=inicioRel)&&(!fimRel||m.data<=fimRel)&&(!categoriaRel||m.cat===categoriaRel)&&m.custo===null).length;
        if(unknownPurchases||unknownStock||unknownOutputs){
            const note=document.createElement('p');note.className='source-note';note.textContent=`Custos desconhecidos: ${unknownPurchases} item(ns) de compra, ${unknownOutputs} saída(s) e ${unknownStock} produto(s) com saldo. Os totais monetários são parciais e somam somente valores conhecidos.`;$('relconteudo').append(note);
        }
        for(const row of document.querySelectorAll('#relconteudo tbody tr')) {
            const cells=[...row.cells];
            if(abaRel==='Estoque'&&cells.length===7){const p=estoqueDemo.find(p=>p.nome===cells[0].textContent);if(p?.custo===null&&row.closest('.panel')?.querySelector('h2')?.textContent.startsWith('Posição atual')){cells[3].textContent='Desconhecido';cells[4].textContent=p.q===0?referenceMoney(0):'Desconhecido';}}
            if(abaRel==='Consumo'&&cells.length===6){const m=saidasDemo.find(m=>m.nome===cells[1].textContent&&m.data===cells[0].textContent);if(m?.custo===null)cells[5].textContent='Desconhecido';}
            if(['Gastos','Economia','Locais de compra'].includes(abaRel)&&cells.length===7){const m=visiblePurchases.find(m=>m.data===cells[0].textContent&&m.nome===cells[1].textContent&&m.local===cells[2].textContent);if(m?.unknown)cells[4].textContent='Desconhecido';}
            if(abaRel==='Preços'&&cells.length===6){const m=visiblePurchases.find(m=>m.data===cells[0].textContent&&m.nome===cells[1].textContent&&m.local===cells[2].textContent);if(m?.unknown){cells[4].textContent='Desconhecido';cells[5].textContent='Desconhecido';}}
        }
    };
    document.addEventListener('click',e=>{const button=e.target.closest('[data-label-product]');if(button)openLabel(button.dataset.labelProduct);});
    document.addEventListener('keydown',e=>{if(e.key==='F8'&&(current===3||current===4)){e.preventDefault();$('codigo').focus();$('codigo').select();}});
    window.desktopTest = {request, get inventory(){return inventory;},sync,exportTable,openLabel,preparePdf,cleanupPdf};
    document.documentElement.style.visibility='hidden';
    request('load').then(data=>{
        sync(data);const now=new Date();inicioRel=localDate(new Date(now.getFullYear(),now.getMonth(),1));fimRel=localDate(new Date(now.getFullYear(),now.getMonth()+1,0));
        document.querySelector('.badge-top').textContent='DADOS SALVOS NESTE COMPUTADOR';
        document.querySelector('.badge-top').title='Clique com o botão direito para exportar, imprimir ou abrir ferramentas de gestão. Atalho: F2.';
        addContextMenu();show(0);document.documentElement.style.visibility='visible';request('ready');
    }).catch(e=>{document.documentElement.style.visibility='visible';page.innerHTML='<h1>Não foi possível carregar o estoque</h1><p>'+esc(e.message)+'</p>';});
})();







