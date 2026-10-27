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
    let lastSavedProduct=null,scanTimer;
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
            emb:p.Packages[0]?.Name||'',factor:p.Packages[0]?.Factor||1,packages:p.Packages,reference:p.Reference
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
        .products-heading{display:flex;align-items:center;justify-content:space-between;gap:20px;margin-bottom:20px}
        .products-heading button.action{margin:0;flex-shrink:0}
        body[data-screen="2"] .layout{height:calc(100vh - var(--app-header-height,68px));min-height:0}
        body[data-screen="2"] main{padding:20px 28px 12px;display:flex;flex-direction:column;min-height:0}
        body[data-screen="2"] #page{flex:1;display:flex;flex-direction:column;min-height:0}
        body[data-screen="2"] #page>h1{font-size:25px;margin-bottom:6px}
        body[data-screen="2"] #page>.muted{font-size:14px;margin-bottom:12px}
        body[data-screen="2"] .registration-panel{padding:20px;margin-bottom:0;flex:1;display:flex;flex-wrap:wrap;align-content:start;align-items:center;gap:0}
        .registration-panel>.two{width:100%;gap:20px}
        .registration-panel .form-column{display:grid;gap:clamp(10px,2.3vh,22px);align-content:start}
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
        .label-action{border:1px solid #dce2ec;border-radius:6px;background:#eef1f8;color:#20366c;padding:7px 10px;cursor:pointer;white-space:nowrap}
        #label-dialog{width:min(680px,95vw);border:1px solid #dce2ec;border-radius:12px;padding:22px;color:#23314b}
        #label-dialog::backdrop{background:#172a5066}
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
        @media print{
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
        @media(max-height:700px){
            body[data-screen="2"] main{padding:14px 24px 12px}
            body[data-screen="2"] .registration-panel{padding:16px}
            .registration-panel .form-column{gap:7px}
            .registration-panel label{font-size:13px;margin-bottom:4px}
            .registration-panel input,.registration-panel select{height:32px;padding:6px 9px}
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
    function arrangeRegistration(){
        const panel=document.querySelector('#page .panel');
        panel.classList.add('registration-panel');
        const columns=document.createElement('div');columns.className='two';
        const definitions=[
            [['code','Código de barras ou código interno'],['nome','Nome do produto'],['cat','Categoria'],['un','Unidade de controle'],['reference-price','Preço de referência por unidade (opcional)']],
            [['min','Estoque mínimo'],['local','Local de armazenamento'],['emb','Embalagem de compra (opcional)'],['factor','Quantas unidades de controle há na embalagem?'],['reference-origin','Origem e data da referência (opcional)']]
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
        help.className='muted registration-help';help.textContent='Exemplo: 1 fardo = 4 pacotes. Cadastre com saldo zero e registre a quantidade em Entrada. A referência e sua origem serão salvas no cadastro.';
        panel.replaceChildren(columns,help,button,message);
        const input=$('code'),group=document.createElement('div'),generate=document.createElement('button');
        group.className='code-entry';input.before(group);generate.type='button';generate.id='generate-barcode';generate.textContent='Gerar código';generate.title='Produto sem código de barras: gerar código interno único';
        generate.onclick=async()=>{generate.disabled=true;try{input.value=await request('generateCode');lastSavedProduct=null;input.dispatchEvent(new Event('change'));}catch(e){alert(e.message);}finally{generate.disabled=false;}};
        group.append(input,generate);
        const print=document.createElement('button');print.type='button';print.id='print-saved-label';print.className='action secondary';print.textContent='Imprimir etiqueta';print.hidden=true;print.onclick=()=>openLabel(lastSavedProduct);button.after(print);
        input.addEventListener('input',()=>{lastSavedProduct=null;print.hidden=true;});
    }
    async function openLabel(id){
        if(!id){alert('Salve o produto antes de imprimir a etiqueta.');return;}
        try{
            const data=await request('barcode',id);document.querySelector('#label-dialog')?.remove();
            const dialog=document.createElement('dialog');dialog.id='label-dialog';
            dialog.innerHTML='<h2>Etiqueta de código de barras</h2><p class="muted">Confira a prévia e selecione a quantidade de etiquetas.</p><div class="label-options"><div><label for="label-count">Quantidade</label><input id="label-count" type="number" min="1" max="100" value="1"></div><div><label for="label-size">Tamanho da etiqueta</label><select id="label-size"><option value="60,40">60 × 40 mm</option><option value="80,50">80 × 50 mm</option><option value="100,50">100 × 50 mm</option></select></div></div><div id="label-print-area"></div><p class="small muted">Imprima em escala 100%, sem cabeçalhos e rodapés. Escolha a impressora de etiquetas ou salve em PDF. O código identifica o produto neste estoque.</p><button type="button" class="action" id="label-print">Imprimir / Salvar PDF</button><button type="button" class="action secondary" id="label-close">Fechar</button>';
            document.body.append(dialog);
            function draw(){
                const count=Math.min(100,Math.max(1,Number($('label-count').value)||1)),[w,h]=$('label-size').value.split(',').map(Number);const area=$('label-print-area');area.replaceChildren();
                for(let i=0;i<count;i++){
                    const label=document.createElement('div');label.className='barcode-label';label.style.width=w+'mm';label.style.height=h+'mm';
                    const name=document.createElement('strong');name.textContent=data.name;
                    const image=document.createElement('img');image.src='data:image/svg+xml;base64,'+btoa(data.svg);image.alt='Código de barras '+data.code;image.style.width=(w-6)+'mm';
                    const code=document.createElement('code');code.textContent=data.code;const location=document.createElement('small');location.textContent=data.location||data.unit;
                    label.append(name,image,code,location);area.append(label);
                }
                let paper=$('label-paper');if(!paper){paper=document.createElement('style');paper.id='label-paper';document.head.append(paper);}paper.textContent=`@media print{@page{size:${w}mm ${h}mm;margin:0}}`;
            }
            function cleanup(){document.body.classList.remove('printing-labels');$('label-paper')?.remove();}
            $('label-count').oninput=draw;$('label-size').onchange=draw;
            $('label-close').onclick=()=>{cleanup();dialog.close();};dialog.addEventListener('close',cleanup);dialog.addEventListener('cancel',cleanup);
            $('label-print').onclick=async()=>{draw();await Promise.all([...dialog.querySelectorAll('img')].map(img=>img.decode()));document.body.classList.add('printing-labels');const [width,height]=$('label-size').value.split(',').map(Number);try{await request('print',{labels:true,width,height});}catch(e){alert(e.message);}finally{document.body.classList.remove('printing-labels');}};
            draw();dialog.showModal();
        }catch(e){alert(e.message);}
    }
    function setupScanner(){
        clearTimeout(scanTimer);const input=$('codigo');input.autocomplete='off';input.spellcheck=false;input.placeholder='Digite ou bipe o código de barras';
        const reset=()=>{selected=null;$('formmov').classList.add('hide');$('produto').replaceChildren();};
        input.onkeydown=e=>{if(e.key==='Enter'||e.key==='Tab'){e.preventDefault();clearTimeout(scanTimer);buscar();if(selected){$('q').focus();$('q').select();}}};
        input.addEventListener('input',()=>{reset();clearTimeout(scanTimer);const code=input.value.trim();scanTimer=setTimeout(()=>{if(current!==3&&current!==4)return;if(produtos.some(p=>p.code===code||p.packages.some(pk=>pk.Code&&pk.Code===code)))buscar();},350);});
        input.addEventListener('focus',()=>input.select());requestAnimationFrame(()=>input.focus());
    }
    function addContextMenu() {
        const menu = document.createElement('div');
        menu.id='desktop-menu'; menu.style.cssText='display:none;position:fixed;z-index:1000;background:white;border:1px solid #dce2ec;border-radius:8px;padding:6px;box-shadow:0 5px 20px #1a2c5033';
        for(const label of ['Ferramentas de gestão','Exportar tabela para Excel (CSV)','Imprimir / Salvar em PDF']) {
            const b = document.createElement('button'); b.textContent=label; b.style.cssText='display:block;border:0;background:white;padding:11px 14px;color:#20366c;text-align:left;width:100%;cursor:pointer';
            b.onclick=()=>{menu.style.display='none'; if(label.startsWith('Ferramentas')) request('advanced',names[current]).catch(e=>alert(e.message)); else if(label.startsWith('Exportar')) exportTable(); else request('print').catch(e=>alert(e.message));};
            menu.append(b);
        }
        document.body.append(menu);
        document.addEventListener('contextmenu',e=>{e.preventDefault();menu.style.left=Math.min(e.clientX,window.innerWidth-310)+'px';menu.style.top=Math.min(e.clientY,window.innerHeight-155)+'px';menu.style.display='block';});
        document.addEventListener('click',()=>menu.style.display='none');
        document.addEventListener('keydown',e=>{ if(e.key==='Escape') menu.style.display='none'; if(e.key==='F2') { e.preventDefault();request('advanced',names[current]); } });
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
        if(current===6){
            const date=s=>s?s.split('-').reverse().join('/'):'Sem limite';
            const summaryFor=tab=>{const summary=document.createElement('div');summary.className='pdf-filter-summary';summary.textContent=`Relatório: ${tab}\nPeríodo: ${date(inicioRel)} a ${date(fimRel)}    •    Local: ${localRel||'Todos os locais'}    •    Categoria: ${categoriaRel||'Todas as categorias'}`;return summary;};
            const previous=abaRel,scrollY=window.scrollY,root=document.createElement('div');root.id='all-reports-pdf';
            try{
                for(const tab of ['Resumo','Gastos','Economia','Consumo','Estoque','Preços','Locais de compra']){
                    abaRel=tab;desenhaRelatorio();
                    const section=document.createElement('section');section.className='pdf-report-section';section.dataset.report=tab;
                    const brand=document.querySelector('.bar').cloneNode(true);brand.querySelector('.badge-top')?.remove();
                    const title=document.createElement('h1');title.textContent='Relatórios de estoque — '+tab;
                    const content=$('relconteudo').cloneNode(true);content.removeAttribute('id');
                    section.append(brand,title,summaryFor(tab),content);root.append(section);
                }
            }finally{abaRel=previous;desenhaRelatorio();window.scrollTo(0,scrollY);}
            const panel=document.querySelector('#page .panel');panel.classList.add('report-filter-panel');
            const summary=summaryFor(previous);
            panel.after(summary);
            document.body.append(root);document.body.classList.add('printing-all-reports');
        }
    }
    function cleanupPdf(){document.body.classList.remove('printing-all-reports');$('all-reports-pdf')?.remove();}
    window.print=()=>request('print').catch(e=>alert(e.message));
    table=function(ps) {
        if(!ps.length) return '<table><tr><th>Produto</th><th>Categoria</th><th>Saldo</th><th>Mínimo</th><th>Situação</th></tr><tr><td colspan="5">Nenhum produto encontrado.</td></tr></table>';
        const container=document.createElement('div');container.innerHTML=referenceTable(ps);
        const rows=[...container.querySelectorAll('tr')];rows[0].insertAdjacentHTML('beforeend','<th>Etiqueta</th>');
        rows.slice(1).forEach((row,i)=>{const cell=document.createElement('td');cell.innerHTML=`<button type="button" class="label-action" data-label-product="${esc(ps[i].id)}">Imprimir etiqueta</button>`;row.append(cell);});
        return container.innerHTML;
    };
    render=function() {
        referenceRender();
        document.body.dataset.screen=String(current);
        if(current===8) document.querySelector('#page .content > .notice')?.remove();
        if(current===1){
            const title=page.querySelector('h1'),subtitle=title.nextElementSibling,button=page.querySelector('button.action');
            const heading=document.createElement('div'),text=document.createElement('div');heading.className='products-heading';
            title.before(heading);text.append(title,subtitle);heading.append(text,button);
        }
        if(current===0) {
            dataNotice(document.querySelector('#page > .notice'),'Os dados são salvos automaticamente neste computador. Use os atalhos para registrar entradas e saídas.');
            const financial=[...document.querySelectorAll('#page .panel')].find(x=>x.querySelector('h2')?.textContent==='Resumo financeiro ilustrativo');
            if(financial) {
                financial.querySelector('h2').textContent='Resumo financeiro';
                const totals={}; compras.forEach(c=>{if(c.valor!==null)totals[c.local]=(totals[c.local]||0)+c.valor;});
                const top=Object.entries(totals).sort((a,b)=>b[1]-a[1])[0];
                financial.querySelectorAll('.card b')[2].textContent=top?.[0]||'Sem dados';
                const refs=compras.filter(c=>c.ref!==null&&c.valor!==null);
                financial.querySelectorAll('.card b')[1].textContent=refs.length?referenceMoney(refs.reduce((a,c)=>a+c.ref-c.valor,0)):'Não calculada';
                financial.querySelector('p.muted').textContent=`Valores das compras registradas. ${compras.filter(c=>c.unknown).length} item(ns) sem custo conhecido; economia calculada em ${refs.length} de ${compras.length} itens com referência.`;
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
            if(current===3) {
                $('datacompra').value=localDate(new Date()); $('datarecebimento').value=localDate(new Date());
                const invoice=[...document.querySelectorAll('#page input')].find(x=>x.placeholder==='Número da nota'); if(invoice) invoice.id='invoice';
            }
        }
        if(current===5) enhanceHistory();
    };
    function enhanceHistory() {
        const note=document.querySelector('#page .panel p.muted');
        if(note) note.textContent='Movimentações registradas no computador. Consulte por período, produto, tipo, responsável e setor.';
        const panel=document.querySelector('#page .panel'); if(!panel)return;
        const filters=document.createElement('details'); filters.style.marginBottom='16px';
        filters.innerHTML='<summary style="cursor:pointer;color:#20366c">Filtrar movimentações</summary><div class="filtergrid"><div><label>Data inicial</label><input type="date" id="history-start"></div><div><label>Data final</label><input type="date" id="history-end"></div><div><label>Tipo</label><select id="history-type"><option>Todos</option><option>Entrada</option><option>Saída</option><option>Ajuste +</option><option>Ajuste −</option></select></div><div><label>Produto</label><input id="history-product"></div><div><label>Responsável</label><input id="history-person"></div><div><label>Setor</label><input id="history-sector"></div></div>';
        panel.insertBefore(filters,panel.querySelector('table'));
        function filter() {
            const data=inventory.Movements.filter(m=>(!$('history-start').value||m.Date.slice(0,10)>=$('history-start').value)&&(!$('history-end').value||m.Date.slice(0,10)<=$('history-end').value)&&($('history-type').value==='Todos'||m.Type===$('history-type').value)&&m.ProductName.toLowerCase().includes($('history-product').value.toLowerCase())&&m.Person.toLowerCase().includes($('history-person').value.toLowerCase())&&m.Sector.toLowerCase().includes($('history-sector').value.toLowerCase())).sort((a,b)=>b.Date.localeCompare(a.Date));
            panel.querySelector('table').outerHTML='<table><tr><th>Data</th><th>Tipo</th><th>Produto</th><th>Quantidade</th><th>Responsável / setor</th></tr>'+data.map(m=>`<tr><td>${esc(new Date(m.Date).toLocaleString('pt-BR'))}</td><td>${esc(m.Type)}</td><td>${esc(m.ProductName)}</td><td>${m.Converted} ${esc(m.Unit)}</td><td>${esc(m.Person)} / ${esc(m.Sector)}</td></tr>`).join('')+'</table>';
        }
        filters.querySelectorAll('input,select').forEach(c=>c.addEventListener('input',filter));
    }
    cadastrar=function(){save(async()=>{
        const code=$('code').value.trim(),name=$('nome').value.trim(),unit=$('un').value.trim();
        const minimum=Number($('min').value),factor=Number($('factor').value),emb=$('emb').value;
        if(!code||!name||!unit||!Number.isInteger(minimum)||minimum<0||(emb&&(!Number.isInteger(factor)||factor<1)))throw new Error('Preencha código, nome, unidade e quantidades válidas.');
        const reference=$('reference-price')?.value||'';
        const p={Code:code,Name:name,Unit:unit,Category:$('cat').value,Minimum:minimum,Target:minimum,Location:$('local').value.trim(),Active:true,Reference:reference===''?null:Number(reference),ReferenceSource:$('reference-origin')?.value.trim()||'',ReferenceDate:new Date().toISOString(),Packages:emb?[{Name:emb,Factor:factor,Code:''}]:[]};
        sync(await request('saveProduct',p));lastSavedProduct=inventory.Products.find(x=>x.Code===code).Id;$('print-saved-label').hidden=false;$('msg').textContent='Produto cadastrado. Você já pode imprimir a etiqueta. Registre o saldo inicial em Entrada.';
    });};
    buscar=function(){
        const code=$('codigo').value.trim(); const p=produtos.find(x=>x.code===code||x.packages.some(pk=>pk.Code&&pk.Code===code));
        if(p)$('codigo').value=p.code;
        referenceSearch(); $('codigo').value=code;
        if(!selected){const button=$('produto').querySelector('button');if(button)button.onclick=()=>{show(2);$('code').value=code;};return;}
        const base=inventory.Products.find(x=>x.Id===selected.id);
        $('tipoemb').innerHTML=`<option value="1">${esc(selected.un)} (unidade de controle)</option>`+base.Packages.map(pk=>`<option value="${pk.Factor}">${esc(pk.Name)} — ${pk.Factor} ${esc(selected.un)}</option>`).join('');
        const pack=base.Packages.findIndex(pk=>pk.Code&&pk.Code===code); if(pack>=0)$('tipoemb').selectedIndex=pack+1;
        if(current===3) { $('ref').value=base.Reference??''; calcFinanceiro(); }
        calc();
    };
    movimentar=function(){save(async()=>{
        if(!selected)throw new Error('Busque um produto antes de confirmar.');
        const quantity=Number($('q').value),factor=Number($('tipoemb').value),person=$('pessoa').value.trim();
        if(!Number.isInteger(quantity)||quantity<=0||!person)throw new Error('Informe uma quantidade inteira positiva e o responsável.');
        const p=inventory.Products.find(x=>x.Id===selected.id);
        const m={ProductId:p.Id,Type:current===3?'Entrada':'Saída',Date:new Date().toISOString(),Quantity:quantity,Factor:factor,Person:person,Sector:$('setor').value.trim(),Note:$('obs').value.trim(),Package:$('tipoemb').selectedOptions[0].textContent,ReferenceSource:p.ReferenceSource,ReferenceDate:p.ReferenceDate};
        if(current===3){
            const price=Number($('preco').value),discount=Number($('desconto').value),addition=Number($('acrescimo').value);
            const total=quantity*price-discount+addition,ref=$('ref').value;
            if(!$('localcompra').value.trim()||!$('datacompra').value||price<0||discount<0||addition<0||total<0||(ref!==''&&Number(ref)<0))throw new Error('Informe local, data e valores válidos.');
            m.Store=$('localcompra').value.trim();m.PurchaseDate=$('datacompra').value+'T00:00:00';
            m.Date=($('datarecebimento').value||localDate(new Date()))+'T'+new Date().toTimeString().slice(0,8);
            m.Total=total;m.ReferenceTotal=ref===''?null:Number(ref)*quantity*factor;m.Invoice=$('invoice')?.value.trim()||'';
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

