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
    window.print=()=>request('print').catch(e=>alert(e.message));
    table=function(ps) {
        if(!ps.length) return '<table><tr><th>Produto</th><th>Categoria</th><th>Saldo</th><th>Mínimo</th><th>Situação</th></tr><tr><td colspan="5">Nenhum produto encontrado.</td></tr></table>';
        return referenceTable(ps);
    };
    render=function() {
        referenceRender();
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
        }
        if(current===3||current===4) {
            dataNotice(document.querySelector('#page > .notice'),'Digite ou leia um código e pressione Enter ou clique em Buscar. O produto deve estar cadastrado.');
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
        sync(await request('saveProduct',p)); $('msg').textContent='Produto cadastrado. Registre o saldo inicial em Entrada.';
    });};
    buscar=function(){
        const code=$('codigo').value.trim(); const p=produtos.find(x=>x.code===code||x.packages.some(pk=>pk.Code&&pk.Code===code));
        if(p)$('codigo').value=p.code;
        referenceSearch(); $('codigo').value=code;
        if(!selected)return;
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
    window.desktopTest = {request, get inventory(){return inventory;},sync,exportTable};
    document.documentElement.style.visibility='hidden';
    request('load').then(data=>{
        sync(data);const now=new Date();inicioRel=localDate(new Date(now.getFullYear(),now.getMonth(),1));fimRel=localDate(new Date(now.getFullYear(),now.getMonth()+1,0));
        document.querySelector('.badge-top').textContent='DADOS SALVOS NESTE COMPUTADOR';
        document.querySelector('.badge-top').title='Clique com o botão direito para exportar, imprimir ou abrir ferramentas de gestão. Atalho: F2.';
        addContextMenu();show(0);document.documentElement.style.visibility='visible';request('ready');
    }).catch(e=>{document.documentElement.style.visibility='visible';page.innerHTML='<h1>Não foi possível carregar o estoque</h1><p>'+esc(e.message)+'</p>';});
})();
