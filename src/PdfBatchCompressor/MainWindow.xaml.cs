using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace PdfBatchCompressor;

public partial class MainWindow : Window
{
    // -----------------------------------------------------------------------
    // Model
    // -----------------------------------------------------------------------
    public sealed class PdfItem : INotifyPropertyChanged
    {
        private bool _selecionado;

        public string NomeArquivo { get; init; } = string.Empty;
        public string CaminhoCompleto { get; init; } = string.Empty;
        public string TamanhoFormatado { get; init; } = string.Empty;
        public long TamanhoBytes { get; init; }

        public bool Selecionado
        {
            get => _selecionado;
            set { _selecionado = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------
    private const string CompressedSuffix = "_compressed";
    private const int MaxUniqueNameAttempts = 100_000;

    // -----------------------------------------------------------------------
    private readonly ObservableCollection<PdfItem> _itens = [];
    private CancellationTokenSource? _cts;
    private bool _atualizandoChkTodos;

    private static readonly (string Chave, string Descricao)[] Qualidades =
    [
        ("screen",   "screen – menor tamanho, qualidade mais baixa"),
        ("ebook",    "ebook – equilíbrio entre tamanho e qualidade"),
        ("printer",  "printer – boa qualidade, arquivo maior"),
        ("prepress", "prepress – alta qualidade, pouca compressão"),
    ];

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------
    public MainWindow()
    {
        InitializeComponent();

        ListaPdfs.ItemsSource = _itens;

        foreach (var (_, desc) in Qualidades)
            CmbQualidade.Items.Add(desc);

        CmbQualidade.SelectedIndex = 1; // ebook padrão

        AtualizarBotoes();
    }

    // -----------------------------------------------------------------------
    // Folder selection
    // -----------------------------------------------------------------------
    private void BtnEscolherOrigem_Click(object sender, RoutedEventArgs e)
    {
        var pasta = EscolherPasta("Selecione a pasta de origem (somente essa pasta, sem subpastas)");
        if (pasta is null) return;

        TxtOrigem.Text = pasta;
        CarregarPdfs(pasta);
    }

    private void BtnEscolherDestino_Click(object sender, RoutedEventArgs e)
    {
        var pasta = EscolherPasta("Selecione a pasta de destino");
        if (pasta is null) return;

        TxtDestino.Text = pasta;
        AtualizarBotoes();
    }

    private static string? EscolherPasta(string descricao)
    {
        // OpenFolderDialog is available in .NET 8 WPF
        var dlg = new OpenFolderDialog
        {
            Title = descricao,
            Multiselect = false,
        };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    // -----------------------------------------------------------------------
    // PDF list
    // -----------------------------------------------------------------------
    private void CarregarPdfs(string pasta)
    {
        _itens.Clear();

        if (!Directory.Exists(pasta))
        {
            AtualizarContagem();
            AtualizarBotoes();
            return;
        }

        var arquivos = Directory.GetFiles(pasta, "*.pdf", SearchOption.TopDirectoryOnly)
                                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var caminho in arquivos)
        {
            var info = new FileInfo(caminho);
            _itens.Add(new PdfItem
            {
                NomeArquivo = info.Name,
                CaminhoCompleto = caminho,
                TamanhoBytes = info.Length,
                TamanhoFormatado = FormatarBytes(info.Length),
                Selecionado = true, // todos marcados por padrão
            });
        }

        AtualizarContagem();
        AtualizarBotoes();
        AtualizarChkTodos();
    }

    private void CheckBox_CheckChanged(object sender, RoutedEventArgs e)
    {
        AtualizarContagem();
        AtualizarBotoes();
        AtualizarChkTodos();
    }

    private void BtnMarcarTodos_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _itens) item.Selecionado = true;
        AtualizarContagem();
        AtualizarBotoes();
        AtualizarChkTodos();
    }

    private void BtnDesmarcarTodos_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _itens) item.Selecionado = false;
        AtualizarContagem();
        AtualizarBotoes();
        AtualizarChkTodos();
    }

    private void ChkTodos_Checked(object sender, RoutedEventArgs e)
    {
        if (_atualizandoChkTodos) return;
        foreach (var item in _itens) item.Selecionado = true;
        AtualizarContagem();
        AtualizarBotoes();
    }

    private void ChkTodos_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_atualizandoChkTodos) return;
        foreach (var item in _itens) item.Selecionado = false;
        AtualizarContagem();
        AtualizarBotoes();
    }

    private void AtualizarChkTodos()
    {
        _atualizandoChkTodos = true;
        try
        {
            if (ChkTodos is null) return;
            int total = _itens.Count;
            int marcados = _itens.Count(i => i.Selecionado);
            ChkTodos.IsChecked = total == 0 ? false
                               : marcados == total ? true
                               : null; // indeterminate
        }
        finally
        {
            _atualizandoChkTodos = false;
        }
    }

    private void AtualizarContagem()
    {
        int total = _itens.Count;
        int marcados = _itens.Count(i => i.Selecionado);
        TxtContagem.Text = total == 0
            ? "Nenhum PDF encontrado."
            : $"{total} PDF(s) encontrado(s), {marcados} selecionado(s).";
    }

    // -----------------------------------------------------------------------
    // Button state
    // -----------------------------------------------------------------------
    private void AtualizarBotoes()
    {
        bool processando = _cts is not null;
        bool podeComprimir = !processando
                             && Directory.Exists(TxtOrigem.Text)
                             && Directory.Exists(TxtDestino.Text)
                             && _itens.Any(i => i.Selecionado);

        BtnComprimir.IsEnabled = podeComprimir;
        BtnCancelar.IsEnabled = processando;
        BtnEscolherOrigem.IsEnabled = !processando;
        BtnEscolherDestino.IsEnabled = !processando;
        BtnMarcarTodos.IsEnabled = !processando;
        BtnDesmarcarTodos.IsEnabled = !processando;
        CmbQualidade.IsEnabled = !processando;
    }

    // -----------------------------------------------------------------------
    // Compression
    // -----------------------------------------------------------------------
    private async void BtnComprimir_Click(object sender, RoutedEventArgs e)
    {
        var gsExe = CaminhoGhostscript();
        if (!File.Exists(gsExe))
        {
            MessageBox.Show(this,
                $"Ghostscript não encontrado em:\n{gsExe}\n\n" +
                "Coloque os arquivos do Ghostscript na pasta tools\\gs\\ " +
                "ao lado do executável do programa.\n\n" +
                "Consulte o README para instruções de instalação.",
                "Ghostscript não encontrado",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var selecionados = _itens.Where(i => i.Selecionado).ToList();
        if (selecionados.Count == 0) return;

        var destino = TxtDestino.Text;
        var qualidade = Qualidades[CmbQualidade.SelectedIndex].Chave;

        TxtLog.Clear();
        BarraProgresso.Maximum = selecionados.Count;
        BarraProgresso.Value = 0;

        _cts = new CancellationTokenSource();
        AtualizarBotoes();

        var token = _cts.Token;

        long totalAntes = 0, totalDepois = 0;
        int ok = 0, falhas = 0;

        try
        {
            await Task.Run(async () =>
            {
                foreach (var item in selecionados)
                {
                    if (token.IsCancellationRequested) break;

                    var nomeBase = Path.GetFileNameWithoutExtension(item.NomeArquivo) + CompressedSuffix + ".pdf";
                    var saida = GerarCaminhoUnico(destino, nomeBase);

                    AppendLog($"Processando: {item.NomeArquivo} ...");

                    try
                    {
                        var (exitCode, _, stdErr) = await ExecutarGhostscriptAsync(
                            gsExe, item.CaminhoCompleto, saida, qualidade, token);

                        if (exitCode != 0 || !File.Exists(saida))
                        {
                            falhas++;
                            AppendLog($"  [ERRO] exit={exitCode}{(string.IsNullOrWhiteSpace(stdErr) ? "" : ": " + stdErr.Trim())}");
                        }
                        else
                        {
                            long depois = new FileInfo(saida).Length;
                            totalAntes += item.TamanhoBytes;
                            totalDepois += depois;
                            ok++;
                            AppendLog($"  [OK] {Path.GetFileName(saida)}  " +
                                      $"{FormatarBytes(item.TamanhoBytes)} → {FormatarBytes(depois)}  " +
                                      $"({CalcularReducao(item.TamanhoBytes, depois)})");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        AppendLog("  [CANCELADO]");
                        break;
                    }
                    catch (Exception ex)
                    {
                        falhas++;
                        AppendLog($"  [EXCEÇÃO] {ex.Message}");
                    }

                    IncrementarProgresso();
                }
            }, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }

        bool cancelado = token.IsCancellationRequested;

        // Resumo final
        AppendLog(string.Empty);
        AppendLog("─────────────────────────────────────────");
        if (cancelado)
            AppendLog("Operação cancelada pelo usuário.");

        AppendLog($"Concluído: {ok} OK, {falhas} falha(s).");
        if (ok > 0)
        {
            AppendLog($"Tamanho total antes : {FormatarBytes(totalAntes)}");
            AppendLog($"Tamanho total depois: {FormatarBytes(totalDepois)}");
            AppendLog($"Redução total       : {CalcularReducao(totalAntes, totalDepois)}");
        }

        TxtStatus.Text = cancelado
            ? $"Cancelado. {ok} OK, {falhas} falha(s)."
            : $"Concluído. {ok} OK, {falhas} falha(s)." +
              (ok > 0 ? $"  {FormatarBytes(totalAntes)} → {FormatarBytes(totalDepois)}" : "");

        AtualizarBotoes();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        TxtStatus.Text = "Cancelando…";
        BtnCancelar.IsEnabled = false;
    }

    // -----------------------------------------------------------------------
    // Ghostscript execution
    // -----------------------------------------------------------------------
    private static string CaminhoGhostscript()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "tools", "gs", "gswin64c.exe");
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> ExecutarGhostscriptAsync(
        string gsExe,
        string entrada,
        string saida,
        string qualidade,
        CancellationToken token)
    {
        var psi = new ProcessStartInfo
        {
            FileName = gsExe,
            Arguments = $"-sDEVICE=pdfwrite " +
                        $"-dCompatibilityLevel=1.4 " +
                        $"-dPDFSETTINGS=/{qualidade} " +
                        $"-dNOPAUSE -dQUIET -dBATCH " +
                        $"-sOutputFile=\"{saida}\" " +
                        $"\"{entrada}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var processo = new Process { StartInfo = psi, EnableRaisingEvents = true };
        processo.Start();

        var stdOutTask = processo.StandardOutput.ReadToEndAsync(token);
        var stdErrTask = processo.StandardError.ReadToEndAsync(token);

        await processo.WaitForExitAsync(token);

        string stdOut = await stdOutTask;
        string stdErr = await stdErrTask;

        return (processo.ExitCode, stdOut, stdErr);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static string GerarCaminhoUnico(string pasta, string nomeArquivo)
    {
        var caminho = Path.Combine(pasta, nomeArquivo);
        if (!File.Exists(caminho)) return caminho;

        var semExt = Path.GetFileNameWithoutExtension(nomeArquivo);
        var ext = Path.GetExtension(nomeArquivo);

        for (int i = 2; i < MaxUniqueNameAttempts; i++)
        {
            var candidato = Path.Combine(pasta, $"{semExt} ({i}){ext}");
            if (!File.Exists(candidato)) return candidato;
        }

        throw new IOException("Não foi possível gerar um nome de arquivo único no destino.");
    }

    private static string FormatarBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] unidades = ["B", "KB", "MB", "GB"];
        double valor = bytes;
        int i = 0;
        while (valor >= 1024 && i < unidades.Length - 1) { valor /= 1024; i++; }
        return $"{valor:0.##} {unidades[i]}";
    }

    private static string CalcularReducao(long antes, long depois)
    {
        if (antes <= 0) return "–";
        double pct = (1.0 - (double)depois / antes) * 100.0;
        return pct >= 0
            ? $"-{pct:0.#}%"
            : $"+{-pct:0.#}% (arquivo ficou maior)";
    }

    private void AppendLog(string texto)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLog(texto));
            return;
        }
        TxtLog.AppendText(texto + Environment.NewLine);
        TxtLog.ScrollToEnd();
    }

    private void IncrementarProgresso()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(IncrementarProgresso);
            return;
        }
        BarraProgresso.Value = Math.Min(BarraProgresso.Maximum, BarraProgresso.Value + 1);
        TxtStatus.Text = $"Processando… {BarraProgresso.Value}/{BarraProgresso.Maximum}";
    }
}
