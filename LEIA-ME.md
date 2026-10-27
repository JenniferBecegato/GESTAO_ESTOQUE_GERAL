# Gestão de Estoque Geral — Campos

Aplicação local C# / Windows Forms (.NET 10) baseada no protótipo fornecido.

## Abrir
Abra `GESTAO_ESTOQUE_GERAL.slnx` no Visual Studio com suporte ao .NET 10 e desenvolvimento desktop. Execute com F5. Também é possível executar `bin\Release\net10.0-windows\GESTAO_ESTOQUE_GERAL.exe`.

## Recursos
- Cadastro, edição e inativação de produtos; mínimo, desejado, categoria e armazenamento.
- Múltiplas embalagens com fator de conversão e códigos únicos; código interno e etiqueta Code 128.
- Entradas com vários itens por compra, datas, local, fornecedor, nota, descontos e acréscimos por item.
- Saídas com bloqueio por saldo, responsável e setor; ajustes com motivo.
- Custo médio móvel, referência e identificação explícita de custos desconhecidos.
- Histórico com filtros; sete relatórios com indicadores, gráficos e tabelas.
- Exportação CSV compatível com Excel; impressão e PDF pela impressora Microsoft Print to PDF.
- SQLite nativo do Windows, sem serviço de banco ou dependências NuGet.

## Dados
Banco em `%LOCALAPPDATA%\Campos\GestaoEstoque\estoque.db`. Começa vazio; os dados do protótipo não foram importados. O estado completo (produtos e movimentos) é gravado em uma transação SQLite como documento JSON, garantindo que saldo e histórico sejam confirmados juntos. O arquivo é independente da pasta de compilação. Para copiá-lo, feche o sistema. Uma instância é permitida por vez.

## Regras importantes
- Produto novo começa com saldo zero; registre o saldo inicial em Entrada.
- Não saber o custo é diferente de custo zero. Desmarque preço conhecido quando necessário.
- Custo médio segue a ordem de registro; datas retroativas não reprocessam custos anteriores.
- Ajuste positivo sem aquisição avaliada torna o custo do saldo desconhecido.
- Distribua descontos/frete da nota manualmente entre os itens, sem duplicar valores.
- A confirmação com itens na lista usa somente a lista; o formulário precisa ser adicionado antes.
- Não altere a unidade de controle após movimentar o produto.
- CSV é o formato de exportação para Excel; não há geração nativa de XLSX.
- PDF usa a impressão do Windows. Gráficos são exibidos na tela; exportação e impressão abrangem a tabela do relatório.
- Valide a leitura das etiquetas na impressora/leitor usados no local.

## Validação
`GESTAO_ESTOQUE_GERAL.exe --self-test` executa testes isolados em banco temporário, valida conversão, custos, bloqueios, persistência e abre todas as telas e abas de relatório. Gera `self-test-result.txt` e imagens de revisão na pasta do executável. Impressão física depende do equipamento e não faz parte do teste automático.
