# MiniCompiler

Compilador didatico feito em C#/.NET. Ele implementa uma linguagem pequena com:

- tipos `int` e `bool`
- variaveis com escopo por bloco
- `if/else`
- `while`
- `print(...)`
- `read(...)`
- expressoes aritmeticas, comparacoes e operadores logicos
- lexer e parser manuais
- AST com Visitor
- tabela de simbolos
- analise semantica
- codigo intermediario TAC
- bytecode proprio
- maquina virtual de pilha
- diagnostico de erro com etapa, classe, linha e coluna
- frontend web local com entrada por codigo, GitHub e ZIP
- auto-correcao simples para `;` e `}` faltando
- validacao/compilacao de Python puro usando CPython como backend

## Como rodar

Na pasta do projeto:

```bash
dotnet run -- --file examples/fatorial.mini --show-tac --show-bytecode --run
```

Tambem funciona com:

```bash
dotnet run -- --dir examples
dotnet run -- --zip caminho/projeto.zip
dotnet run -- --github https://github.com/usuario/repositorio
dotnet run -- --source "int x = 2; print(x);" --run
dotnet run -- --source "try:\n    print('ok')\nexcept Exception:\n    print('erro')"
```

Se abrir sem argumentos, o programa mostra um menu simples para escolher arquivo, pasta, ZIP, GitHub ou codigo colado no terminal.

## Frontend

Para abrir a interface web:

```bash
dotnet run -- --web
```

Depois acesse:

```text
http://localhost:5055
```

A tela tem tres abas:

- `Codigo`: cola o codigo fonte e compila.
- `GitHub`: recebe o link do repositorio, clona com `git clone` e analisa os arquivos encontrados.
- `ZIP`: recebe um arquivo `.zip`, extrai em pasta temporaria e compila os codigos dentro dele.

Tambem da para trocar a porta:

```bash
dotnet run -- --web --url http://localhost:5060
```

## Extensoes aceitas

Ao analisar pasta, ZIP ou repositorio, ele procura arquivos:

- `.mini`
- `.mc`
- `.mcomp`
- `.txt`
- `.py`

## Python puro

O MiniCompiler tambem aceita codigo Python puro. Quando o arquivo tem extensao `.py` ou quando o codigo colado parece Python (`try:`, `except`, `for ...:`, `input(...)`, `range(...)`, `f-string`, etc.), ele nao tenta passar pelo parser didatico.

Nesse caso, o backend chama o Python instalado na maquina, valida a sintaxe com `ast.parse`, compila com `compile(...)` e devolve para o frontend:

- `Tokens`
- `TAC`, usando uma representacao intermediaria do AST Python
- `Bytecode`, usando o `dis` do CPython
- `Variaveis`, contando nomes atribuidos no codigo
- erro de sintaxe com linha, coluna e trecho marcado, quando houver

Esse modo nao executa `input()` automaticamente. Ele valida/compila o Python para evitar travar a interface durante a apresentacao.

## Exemplo da linguagem

```c
int n = 5;
int fat = 1;

while (n > 1) {
    fat = fat * n;
    n = n - 1;
}

print(fat);
```

## Mensagem de erro

Quando encontra erro, o compilador tenta mostrar:

- etapa do problema: entrada, lexico, sintatico, semantico, TAC, bytecode ou execucao
- origem do arquivo
- classe onde o erro foi tratado
- linha e coluna
- trecho do codigo com marcador
- mensagem objetiva
- painel amigavel no frontend, sem jogar stack trace na tela
- auto-correcao aplicada, quando o erro for simples o bastante

Exemplo de erro semantico:

```c
int x = true;
```

Saida esperada:

```text
ERRO ENCONTRADO
Etapa: Semantico
Origem: examples/erro_tipo.mini
Classe: SemanticAnalyzer
Posicao: linha 1, coluna 9
Trecho: int x = true;
                ^
Mensagem: A variavel 'x' e do tipo int, mas recebeu bool.
```

## Auto-correcao

Antes de compilar, o frontend passa cada arquivo por uma etapa de reparo simples. Ela tenta resolver casos bobos que costumam aparecer em apresentacao:

- linha de declaracao sem `;`, como `int x = 10`
- linha de atribuicao sem `;`, como `x = x + 1`
- `print(...)` ou `read(...)` sem `;`
- bloco aberto com `{` e sem `}` no final do arquivo

Quando alguma correcao acontece, a tela mostra a linha, a coluna e a alteracao feita. Se mesmo assim o codigo continuar errado, o sistema mostra o erro normal e continua online.

## Observacoes

O bytecode nao mira Assembly x86. Ele roda em uma VM de pilha propria para manter o projeto menor e mais facil de acompanhar. O TAC existe como uma camada intermediaria visivel com `--show-tac`.

## Integrantes

Andressa Galvão,
Deivide Sobral,
Jorlan Heider.

---

## Status do Projeto

Concluído e operacional.