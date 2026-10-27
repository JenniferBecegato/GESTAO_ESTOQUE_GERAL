# Gestão de Estoque Geral — Campos

Aplicação Windows Forms (.NET 10) com a interface HTML original do protótipo renderizada localmente pelo componente Microsoft WebView2. Não exige internet para as operações do estoque.

## Abrir
Abra `GESTAO_ESTOQUE_GERAL.slnx` no Visual Studio com suporte a .NET 10/desktop e execute com F5. O executável está em `bin\Release\net10.0-windows\GESTAO_ESTOQUE_GERAL.exe`.

## Interface do protótipo
`Prototipo.html` é uma cópia integral do arquivo recebido: os estilos, menus, cartões, formulários, abas de relatórios e os 15 tópicos da descrição foram preservados. `Desktop.js` conecta a interface ao banco real. Os avisos de demonstração das telas operacionais são substituídos por mensagens sobre os dados locais. A descrição permanece integralmente igual à referência fornecida, incluindo o texto de planejamento.

O HTML é incorporado ao executável; não depende do arquivo em Downloads. O componente WebView2 Runtime da Microsoft precisa estar instalado (já encontrado neste computador).

## Uso
1. Cadastre o produto e sua embalagem de compra.
2. Registre o saldo inicial ou compra em Entrada; informe responsável e preço por embalagem. A conversão é aplicada ao saldo.
3. Use Saída para registrar as retiradas; o sistema bloqueia quantidade acima do saldo.
4. Consulte o histórico e os sete relatórios com os mesmos menus do protótipo.

Clique com o botão direito na interface para exportar CSV para Excel, imprimir/salvar PDF ou abrir as ferramentas de gestão. F2 também abre essas ferramentas, que preservam os recursos adicionais da primeira versão: edição/inativação, múltiplas embalagens e códigos, compras com vários itens, saldo inicial de custo desconhecido, ajustes e etiquetas.

## Dados
O banco existente é preservado em `%LOCALAPPDATA%\Campos\GestaoEstoque\estoque.db`. A interface recebe somente os dados reais desse banco; os exemplos do protótipo não são importados. O estado de produtos e movimentações é confirmado junto, em uma transação SQLite. Os arquivos de navegação do WebView2 ficam na subpasta `InterfaceWebView2`, separada do banco.

O custo médio segue a ordem de registro. Datas retroativas não reprocessam custos anteriores. Os valores sem custo conhecido são identificados e os totais correspondentes aparecem como parciais. Desconto e acréscimo são atribuídos ao item.

## Verificação
Execute com `--self-test` para validar em um banco temporário: cadastro, conversão, entradas, saídas, saldo, persistência, telas e relatórios. O teste compara a descrição e todos os estilos diretamente com o HTML original e gera capturas em `validacao-layout`, ao lado do executável. O banco real não é alterado.
