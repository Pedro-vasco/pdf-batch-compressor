# PDF Batch Compressor

Aplicativo WPF (.NET 8) para Windows 10/11 x64 que comprime PDFs em lote de forma **totalmente offline**, usando o [Ghostscript](https://www.ghostscript.com/) embutido (portátil).

---

## Funcionalidades

- Seleciona **pasta de origem** (somente a pasta selecionada, sem subpastas).
- Seleciona **pasta de destino**.
- Lista todos os PDFs da pasta de origem com **checkbox** (todos marcados por padrão).
- Botões: **Marcar todos**, **Desmarcar todos**, **Comprimir selecionados**, **Cancelar**.
- Dropdown de **qualidade**: `screen` / `ebook` / `printer` / `prepress` (padrão: `ebook`).
- Saída com sufixo `_compressed.pdf`; em caso de conflito de nome gera `_compressed (2).pdf`, etc.
- **Barra de progresso** + **log detalhado** com tamanho antes/depois e resumo final.
- Roda **100% offline** — sem serviços de nuvem.

---

## Pré-requisitos

| Item | Versão mínima |
|------|---------------|
| Windows | 10 ou 11, 64-bit |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0.x (somente para compilar) |
| Ghostscript (win64) | 10.x recomendado |

> Ao publicar com `--self-contained true`, o .NET Runtime é incluído na pasta de saída — o usuário final não precisa instalar o .NET.

---

## Instalação do Ghostscript (portátil)

1. Baixe o instalador win64 em <https://www.ghostscript.com/releases/gsdnld.html>
   (ex.: `gs10031w64.exe`).
2. Execute o instalador normalmente (ou extraia com 7-Zip).
3. Copie os seguintes itens para a pasta `tools/gs/` dentro da pasta do executável:

```
tools/
└── gs/
    ├── gswin64c.exe   ← executável de linha de comando
    ├── gsdll64.dll    ← DLL principal
    └── lib/           ← pasta inteira de recursos
```

> **Nota:** dependendo da versão, pode haver DLLs adicionais (`freetype.dll`, etc.) na pasta `bin\` da instalação — copie todas que estiverem ao lado de `gswin64c.exe`.

---

## Como compilar (Visual Studio)

1. Abra o Visual Studio 2022 (ou mais recente).
2. **Abrir Projeto** → selecione `src/PdfBatchCompressor/PdfBatchCompressor.csproj`.
3. Pressione **F5** para compilar e executar em modo Debug.

### Como publicar (linha de comando)

```powershell
dotnet publish src/PdfBatchCompressor/PdfBatchCompressor.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o publish/PdfBatchCompressor
```

Depois copie a pasta `tools/gs/` (com os arquivos do Ghostscript) para dentro de `publish/PdfBatchCompressor/tools/gs/`.

---

## Como usar o aplicativo

1. Execute `PdfBatchCompressor.exe`.
2. Clique em **Selecionar…** em "Pasta de Origem" e escolha a pasta com os PDFs.
3. Clique em **Selecionar…** em "Pasta de Destino".
4. Desmarque os PDFs que não deseja comprimir (todos ficam marcados por padrão).
5. Escolha o perfil de qualidade:
   - `screen` — menor tamanho, qualidade mais baixa (72 dpi)
   - `ebook` — equilíbrio (150 dpi) **← padrão**
   - `printer` — boa qualidade (300 dpi)
   - `prepress` — alta qualidade, pouca compressão (300 dpi + cores)
6. Clique em **Comprimir selecionados**.
7. Acompanhe o log e a barra de progresso.

---

## CI / GitHub Actions

O workflow `.github/workflows/build.yml` é executado a cada push/PR para `main` e:

1. Compila o projeto em modo Debug (validação).
2. Publica em modo Release (`win-x64`, self-contained).
3. Faz upload do artefato `PdfBatchCompressor-win-x64`.

O artefato pode ser baixado na aba **Actions** do repositório. Após baixar, basta adicionar os arquivos do Ghostscript em `tools/gs/` para ter o app pronto para usar.

---

## Aviso de licença — Ghostscript (AGPL)

O Ghostscript é distribuído sob a licença **AGPL v3**. Isso significa:

- **Uso pessoal / interno:** livre, sem restrições.
- **Distribuição para terceiros (incluindo como parte de um produto):** o código-fonte do seu aplicativo deve também ser disponibilizado sob AGPL **ou** você deve adquirir uma licença comercial do Ghostscript em <https://www.artifex.com/>.

**Os binários do Ghostscript não estão incluídos neste repositório.** Cada usuário deve baixar e instalar o Ghostscript conforme as instruções acima, respeitando os termos da licença.

### Como atualizar / substituir o Ghostscript

Basta substituir os arquivos em `tools/gs/` pela nova versão. Não há nenhuma dependência de versão específica no código — qualquer versão recente do Ghostscript win64 deve funcionar.

---

## Estrutura do repositório

```
pdf-batch-compressor/
├── .github/
│   └── workflows/
│       └── build.yml          ← CI/CD
├── src/
│   └── PdfBatchCompressor/
│       ├── PdfBatchCompressor.csproj
│       ├── App.xaml / App.xaml.cs
│       └── MainWindow.xaml / MainWindow.xaml.cs
├── tools/
│   └── gs/                    ← coloque o Ghostscript aqui (não versionado)
│       └── .gitkeep
├── .gitignore
└── README.md
```
