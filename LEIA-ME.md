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

## Leitor de código de barras e etiquetas
- Na tela Entrada ou Saída, o campo Código recebe foco automaticamente. Digite e pressione Enter ou use leitor USB/Bluetooth configurado como teclado (HID). São aceitos Enter, Tab e leitura completa sem sufixo. F8 volta ao campo do código.
- A leitura apenas identifica o produto e a embalagem; confira quantidade, responsável e demais dados antes de confirmar. Ler um código nunca lança uma movimentação sozinho.
- Depois de confirmar, o campo é limpo e volta a receber foco para a próxima leitura. Alterar o código invalida o produto anterior, evitando lançar no item errado.
- Para itens sem código, clique em Gerar código no cadastro, preencha os demais campos e salve. O sistema cria um código numérico interno único, com dígito verificador.
- Use Imprimir etiqueta após salvar ou na coluna Etiqueta da lista de produtos. Há prévia, quantidade de 1 a 100 e tamanhos de 60×40, 80×50 e 100×50 mm.
- A etiqueta usa Code 128, inclui nome e código legível e pode ser impressa ou salva em PDF. Selecione papel do mesmo tamanho, escala 100% e desative cabeçalhos/rodapés na impressão. O código impresso é o mesmo aceito na entrada e saída.
- A integração foi validada por eventos de teclado simulados e pela prévia. A leitura física e o alinhamento devem ser conferidos com o leitor e a impressora usados no local.

## PDF para impressão
A opção Imprimir / Salvar em PDF gera um arquivo PDF diretamente. Escolha onde salvar; o documento é aberto no visualizador de PDF para conferir e imprimir. Os relatórios usam A4 na horizontal, sem menu lateral, botões, aviso do computador, data/URL/título do navegador ou controles de seleção. Os filtros aplicados aparecem como texto e os gráficos e tabelas são preservados. As etiquetas geram PDF no tamanho selecionado, uma etiqueta por página, também sem cabeçalhos/rodapés do navegador.

## Relatório completo em PDF
A exportação na tela Relatórios inclui as sete abas (Resumo, Gastos, Economia, Consumo, Estoque, Preços e Locais de compra) no mesmo arquivo, com uma seção por aba. Período, categoria e local selecionados são preservados durante toda a geração. A aba que estiver aberta na tela não limita o conteúdo exportado. Os filtros mantêm o significado da tela: período filtra compras e saídas; categoria filtra também o saldo atual; local filtra compras. A posição do estoque é atual. Se uma seção não tiver registros nos filtros aplicados, essa condição é informada. Ao terminar, a tela retorna à aba que estava selecionada.

## Argox OS-2140plus, bobina e folha A4
Ao confirmar uma entrada, o botão de etiquetas sugere a quantidade convertida: 1 caixa com fator 50 oferece 50 etiquetas, todas com o mesmo código do produto. A movimentação não é repetida ao imprimir. A lista de produtos sugere a quantidade da última entrada; o usuário pode alterar antes de imprimir.

Na prévia, escolha Bobina / Argox ou Folha A4, medidas da etiqueta, espaço entre elas, colunas da bobina e margem de A4. As configurações são salvas ao lado do banco em label-profile.json. As dimensões não podem ser determinadas com precisão por fotografia: meça o rótulo sem incluir o intervalo entre etiquetas. Na bobina, informe quantos rótulos existem lado a lado. Em A4, as linhas e colunas são calculadas conforme medidas e margem.

Escolher impressora e imprimir abre o seletor do Windows e envia uma única cópia do documento, que já contém a quantidade de etiquetas. Se houver uma impressora Argox instalada, ela é sugerida no modo bobina. O driver Windows correspondente deve estar instalado, com papel e sensor de intervalo configurados/calibrados para a mídia usada. Não há instalação automática de driver nem envio de teste físico.

Gerar PDF preserva o papel escolhido: A4 ou largura total da bobina por altura do rótulo. A última linha contém somente as etiquetas necessárias. Teste uma etiqueta e confira o leitor/alinhamento antes de imprimir a quantidade completa. Códigos numéricos válidos com 13 dígitos usam EAN-13; demais códigos usam Code 128.

## Atualização: etiquetas somente em PDF A4
As opções de bobina/Argox e impressão direta de etiquetas foram retiradas. A prévia de etiquetas oferece somente Gerar PDF, em A4 (210 × 297 mm), com várias etiquetas distribuídas por folha. O padrão compacto de 30 × 20 mm, margem de 5 mm e intervalo de 1 mm comporta até 78 etiquetas por folha. A quantidade continua vinculada às unidades recebidas: uma caixa com 50 unidades gera 50 etiquetas do mesmo código. O arquivo contém apenas a quantidade solicitada e usa folhas adicionais somente quando necessário. Configurações antigas de bobina são substituídas pelo perfil A4. Para imprimir, abra o PDF e selecione papel A4, tamanho real/100% e uma cópia.

## Orientação de todos os PDFs
Todos os botões de PDF apresentam a escolha A4 em Retrato (vertical) ou Paisagem (horizontal) antes de salvar. Histórico e Descrição do sistema também possuem Exportar PDF no canto superior direito. Tabelas quebram textos longos e continuam em novas páginas, sem limitar o documento à área de rolagem da tela. Os relatórios reorganizam os cartões e gráficos no modo retrato. As etiquetas recalculam linhas, colunas e páginas conforme a orientação, preservando o tamanho físico do rótulo e a quantidade exata. Não há impressão em bobina nem envio direto para impressoras de etiquetas; as etiquetas continuam somente em PDF A4.

## Exportação direta, sem pergunta de orientação
Exportar PDF abre diretamente a janela para escolher a pasta e o nome do arquivo. Os PDFs são gerados em A4 retrato, com conteúdo ajustado à página e paginação automática. A janela de orientação foi removida. Na impressão pelo Adobe Acrobat ou outro leitor, selecione papel A4 e Ajustar/Ajustar à área imprimível; orientação pode ser escolhida na própria impressão. PDFs têm diagramação fixa: escala personalizada de 100% pode cortar conteúdo se o papel ou a orientação da impressora forem diferentes. Na captura enviada, o papel de impressão estava em Carta (215,9 × 279,4 mm), diferente de A4 (210 × 297 mm).
