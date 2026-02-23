using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NHX_Kit
{
    public partial class MainWindow : Window
    {
        private sealed class ActionItem
        {
            public required string Key { get; init; }
            public required string Title { get; init; }
            public required string Sub { get; init; }
            public required string Desc { get; init; }
            public required Func<Task> RunAsync { get; init; }
        }

        private readonly Dictionary<string, ActionItem> _actions = new();
        private ActionItem? _current;
        private bool _running;
        private static readonly Encoding ProcessEncoding = GetPreferredProcessEncoding();
        [DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

        private readonly BrushConverter _brushConv = new();

        public MainWindow()
        {
            InitializeComponent();
            BuildActions();

            WriteLog("NHX_Kit iniciado.");
            WriteLog("Executando como Administrador.", "OK");
            WriteLog("Selecione uma acao na barra lateral.");
        }

        private void BuildActions()
        {
            _actions["dns"] = new ActionItem
            {
                Key = "dns",
                Title = "Flush DNS",
                Sub = "Limpa o cache de resolucao de nomes de dominio.",
                Desc = "Executa ipconfig /flushdns para reiniciar o cache DNS do sistema.",
                RunAsync = async () =>
                {
                    WriteQ("Executando ipconfig /flushdns...");
                    var r = await RunProcessCaptureAsync("ipconfig.exe", "/flushdns");
                    WriteQ(r.Trim());
                }
            };

            _actions["release"] = new ActionItem
            {
                Key = "release",
                Title = "Release IP",
                Sub = "Libera o endereco IP atual do adaptador de rede.",
                Desc = "Executa ipconfig /release para soltar a concessao DHCP atual.",
                RunAsync = async () =>
                {
                    WriteQ("Executando ipconfig /release...", "WARN");
                    var outp = await RunProcessCaptureAsync("ipconfig.exe", "/release");
                    foreach (var line in SplitLines(outp).Where(l => l.Length > 0))
                        WriteQ(line);
                    WriteQ("Release concluido.", "OK");
                }
            };

            _actions["renew"] = new ActionItem
            {
                Key = "renew",
                Title = "Renew IP",
                Sub = "Solicita um novo endereco IP ao DHCP.",
                Desc = "Executa ipconfig /renew para renovar a configuracao de rede.",
                RunAsync = async () =>
                {
                    WriteQ("Executando ipconfig /renew...", "WARN");
                    var outp = await RunProcessCaptureAsync("ipconfig.exe", "/renew");
                    foreach (var line in SplitLines(outp).Where(l => l.Length > 0))
                        WriteQ(line);
                    WriteQ("Renew concluido.", "OK");
                }
            };

            _actions["winsock"] = new ActionItem
            {
                Key = "winsock",
                Title = "Reset Winsock",
                Sub = "Redefine o catalogo Winsock.",
                Desc = "Executa netsh winsock reset. Reinicio do sistema pode ser necessario.",
                RunAsync = async () =>
                {
                    await RunAndWriteLinesAsync("netsh.exe", "winsock reset", "Executando netsh winsock reset...", "Reset Winsock concluido.");
                }
            };

            _actions["ipreset"] = new ActionItem
            {
                Key = "ipreset",
                Title = "Reset Protocolo IP",
                Sub = "Reseta parametros TCP/IP da pilha de rede.",
                Desc = "Executa netsh int ip reset. Reinicio do sistema pode ser necessario.",
                RunAsync = async () =>
                {
                    await RunAndWriteLinesAsync("netsh.exe", "int ip reset", "Executando netsh int ip reset...", "Reset do protocolo IP concluido.");
                }
            };

            _actions["netchain"] = new ActionItem
            {
                Key = "netchain",
                Title = "Executar Cadeia de Rede",
                Sub = "Aplica os principais comandos de reparo em sequencia.",
                Desc = "Executa: flushdns, release, renew, winsock reset e int ip reset.",
                RunAsync = async () =>
                {
                    WriteQ("Iniciando cadeia de comandos de rede...", "WARN");
                    await RunAndWriteLinesAsync("ipconfig.exe", "/flushdns", "1/5 - Flush DNS");
                    await RunAndWriteLinesAsync("ipconfig.exe", "/release", "2/5 - Release IP", null, "WARN");
                    await RunAndWriteLinesAsync("ipconfig.exe", "/renew", "3/5 - Renew IP", null, "WARN");
                    await RunAndWriteLinesAsync("netsh.exe", "winsock reset", "4/5 - Reset Winsock", null, "WARN");
                    await RunAndWriteLinesAsync("netsh.exe", "int ip reset", "5/5 - Reset Protocolo IP", null, "WARN");
                    WriteQ("Cadeia de rede concluida. Reinicie o computador se solicitado.", "OK");
                }
            };

            _actions["netinfo"] = new ActionItem
            {
                Key = "netinfo",
                Title = "Diagnostico de Rede (ipconfig /all)",
                Sub = "Mostra configuracoes completas dos adaptadores.",
                Desc = "Executa ipconfig /all e registra a saida no log.",
                RunAsync = async () =>
                {
                    await RunAndWriteLinesAsync("ipconfig.exe", "/all", "Coletando diagnostico de rede...");
                }
            };

            _actions["proxy"] = new ActionItem
            {
                Key = "proxy",
                Title = "Resetar Proxy WinHTTP",
                Sub = "Remove configuracao de proxy da pilha WinHTTP.",
                Desc = "Executa netsh winhttp reset proxy.",
                RunAsync = async () =>
                {
                    await RunAndWriteLinesAsync("netsh.exe", "winhttp reset proxy", "Executando reset de proxy...");
                }
            };

            _actions["temp"] = new ActionItem
            {
                Key = "temp",
                Title = "Arquivos Temporarios",
                Sub = "Remove arquivos residuais da pasta TEMP.",
                Desc = "Exclui arquivos temporarios (ignora os que estiverem em uso).",
                RunAsync = async () =>
                {
                    string temp = Path.GetTempPath();
                    WriteQ($"Processando: {temp}");

                    var entries = Directory.EnumerateFileSystemEntries(temp).ToList();
                    WriteQ($"{entries.Count} itens encontrados.");

                    int processed = 0;
                    foreach (var p in entries)
                    {
                        TryDelete(p);
                        processed++;
                        if (processed % 25 == 0) WriteQ($"{processed}/{entries.Count} processados...");
                        await Task.Yield();
                    }

                    WriteQ("Limpeza concluida.", "OK");
                }
            };

            _actions["deepclean"] = new ActionItem
            {
                Key = "deepclean",
                Title = "Limpeza Temp + Prefetch",
                Sub = "Limpa TEMP do usuario, Windows Temp e Prefetch.",
                Desc = "Executa em cadeia: del /s /f /q %temp%\\*.*, C:\\Windows\\Temp\\*.* e C:\\Windows\\Prefetch\\*.*",
                RunAsync = async () =>
                {
                    WriteQ("Iniciando limpeza de TEMP e Prefetch...", "WARN");
                    await RunAndWriteLinesAsync("cmd.exe", "/c del /s /f /q %temp%\\*.*", "1/3 - Limpando %temp%...");
                    await RunAndWriteLinesAsync("cmd.exe", "/c del /s /f /q C:\\Windows\\Temp\\*.*", "2/3 - Limpando C:\\Windows\\Temp...");
                    await RunAndWriteLinesAsync("cmd.exe", "/c del /s /f /q C:\\Windows\\Prefetch\\*.*", "3/3 - Limpando C:\\Windows\\Prefetch...");
                    WriteQ("Limpeza Temp/Prefetch concluida.", "OK");
                }
            };

            _actions["lixo"] = new ActionItem
            {
                Key = "lixo",
                Title = "Esvaziar Lixeira",
                Sub = "Remove permanentemente os arquivos na Lixeira.",
                Desc = "Usa o PowerShell Clear-RecycleBin (se falhar, registra aviso).",
                RunAsync = async () =>
                {
                    WriteQ("Esvaziando Lixeira...");
                    // Mantido simples: chama o cmdlet do próprio Windows
                    var r = await RunProcessCaptureAsync("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"try{Clear-RecycleBin -Force -ErrorAction Stop; 'OK'}catch{ 'WARN: ' + $_.Exception.Message }\"");
                    WriteQ(r.Trim(), r.StartsWith("OK") ? "OK" : "WARN");
                }
            };

            _actions["icone"] = new ActionItem
            {
                Key = "icone",
                Title = "Cache de Icones",
                Sub = "Redefine o banco de dados de icones do Windows.",
                Desc = "Limpa icon cache (ie4uinit) e reinicia o Explorer.",
                RunAsync = async () =>
                {
                    WriteQ("Limpando cache via ie4uinit...");
                    await RunProcessNoCaptureAsync("ie4uinit.exe", "-ClearIconCache");

                    WriteQ("Reiniciando Explorer...", "WARN");
                    foreach (var p in Process.GetProcessesByName("explorer"))
                    {
                        try { p.Kill(true); } catch { /* ignore */ }
                    }
                    await Task.Delay(1200);
                    await RunProcessNoCaptureAsync("explorer.exe", "");
                    WriteQ("Cache limpo. Explorer reiniciado.", "OK");
                }
            };

            _actions["mini"] = new ActionItem
            {
                Key = "mini",
                Title = "Cache de Miniaturas",
                Sub = "Remove arquivos de pre-visualizacao armazenados.",
                Desc = "Deleta thumbcache_*.db da pasta do Explorer (ignora arquivos em uso).",
                RunAsync = async () =>
                {
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                           "Microsoft", "Windows", "Explorer");
                    if (!Directory.Exists(dir))
                    {
                        WriteQ("Pasta de miniaturas nao encontrada.", "WARN");
                        return;
                    }

                    var files = Directory.GetFiles(dir, "thumbcache_*.db").ToList();
                    WriteQ($"{files.Count} arquivo(s) encontrado(s).");

                    int n = 0;
                    foreach (var f in files)
                    {
                        try
                        {
                            File.Delete(f);
                            n++;
                            WriteQ("Removido: " + Path.GetFileName(f));
                        }
                        catch
                        {
                            WriteQ("Em uso, ignorado: " + Path.GetFileName(f), "WARN");
                        }
                        await Task.Yield();
                    }

                    WriteQ($"{n} arquivo(s) removido(s).", "OK");
                }
            };

            _actions["wu"] = new ActionItem
            {
                Key = "wu",
                Title = "Resetar Windows Update",
                Sub = "Reinicia servicos e limpa cache do Windows Update.",
                Desc = "Para wuauserv, limpa SoftwareDistribution e inicia o servico.",
                RunAsync = async () =>
                {
                    WriteQ("Parando wuauserv...");
                    await RunProcessNoCaptureAsync("sc.exe", "stop wuauserv");
                    await Task.Delay(1200);

                    string sd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution");
                    WriteQ("Limpando SoftwareDistribution...");
                    if (Directory.Exists(sd))
                    {
                        foreach (var p in Directory.EnumerateFileSystemEntries(sd))
                        {
                            TryDelete(p);
                            await Task.Yield();
                        }
                    }

                    WriteQ("Iniciando wuauserv...");
                    await RunProcessNoCaptureAsync("sc.exe", "start wuauserv");
                    WriteQ("Windows Update resetado.", "OK");
                }
            };

            _actions["sfc"] = new ActionItem
            {
                Key = "sfc",
                Title = "Verificar Arquivos SFC",
                Sub = "Verifica e repara arquivos de sistema corrompidos.",
                Desc = "Executa sfc /scannow e registra a saida.",
                RunAsync = async () =>
                {
                    WriteQ("Iniciando sfc /scannow (pode demorar)...", "WARN");
                    var outp = await RunProcessCaptureAsync("sfc.exe", "/scannow");
                    foreach (var line in SplitLines(outp).Where(l => l.Length > 0))
                        WriteQ(line);
                    WriteQ("SFC concluido.", "OK");
                }
            };

            _actions["disk"] = new ActionItem
            {
                Key = "disk",
                Title = "CHKDSK Somente Leitura",
                Sub = "Analisa integridade do disco C: sem alteracoes.",
                Desc = "Executa chkdsk C: (modo leitura) e registra a saida.",
                RunAsync = async () =>
                {
                    WriteQ("Executando chkdsk C: (somente leitura)...");
                    var outp = await RunProcessCaptureAsync("chkdsk.exe", "C:");
                    foreach (var line in SplitLines(outp).Where(l => l.Length > 0))
                        WriteQ(line);
                    WriteQ("CHKDSK concluido.", "OK");
                }
            };

            _actions["dism"] = new ActionItem
            {
                Key = "dism",
                Title = "DISM CheckHealth",
                Sub = "Verifica estado da imagem do Windows (rapido).",
                Desc = "Executa DISM /Online /Cleanup-Image /CheckHealth.",
                RunAsync = async () =>
                {
                    await RunAndWriteLinesAsync("dism.exe", "/Online /Cleanup-Image /CheckHealth", "Executando DISM CheckHealth...");
                    WriteQ("DISM CheckHealth concluido.", "OK");
                }
            };

            _actions["mem"] = new ActionItem
            {
                Key = "mem",
                Title = "Liberar Memoria (GC)",
                Sub = "Forca coleta de lixo .NET.",
                Desc = "Executa GC.Collect / WaitForPendingFinalizers / GC.Collect.",
                RunAsync = async () =>
                {
                    WriteQ("Executando coleta de lixo .NET...");
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    await Task.Delay(400);
                    WriteQ("Coleta concluida.", "OK");
                }
            };
        }

        private static IEnumerable<string> SplitLines(string s)
        {
            return s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(x => x.TrimEnd());
        }

        private async Task RunAndWriteLinesAsync(string file, string args, string startMsg, string? finishMsg = null, string startLevel = "INFO")
        {
            WriteQ(startMsg, startLevel);
            var outp = await RunProcessCaptureAsync(file, args);
            foreach (var line in SplitLines(outp).Where(l => l.Length > 0))
                WriteQ(line);
            if (!string.IsNullOrWhiteSpace(finishMsg))
                WriteQ(finishMsg, "OK");
        }

        private void SideButton_Click(object sender, RoutedEventArgs e)
        {
            if (_running) return;

            if (sender is Button b && b.Tag is string key && _actions.TryGetValue(key, out var a))
            {
                _current = a;

                PageTitle.Text = a.Title;
                PageSubtitle.Text = a.Sub;

                StatusCard.Visibility = Visibility.Visible;
                StatusTitle.Text = a.Title;
                StatusDesc.Text = a.Desc;

                SetBadge("PRONTO", "#0078D4", "#220078D4");

                BtnRun.IsEnabled = true;
                BtnRun.Content = "Executar";
                FooterText.Text = "Clique em Executar para iniciar.";

                WriteLog($"Acao selecionada: {a.Title}");
            }
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            if (_running || _current is null) return;

            _running = true;
            BtnRun.IsEnabled = false;
            BtnRun.Content = "Executando...";
            ProgressBar.Visibility = Visibility.Visible;

            SetBadge("EM EXECUCAO", "#F0A30A", "#22F0A30A");
            WriteLog("-----------------------------------");
            WriteLog("Iniciando: " + _current.Title);

            try
            {
                await _current.RunAsync();
                SetBadge("CONCLUIDO", "#2EC55A", "#2216C60A");
                FooterText.Text = "+ Concluido com sucesso.";
                WriteLog(_current.Title + " concluido com sucesso.", "OK");
            }
            catch (Exception ex)
            {
                SetBadge("ERRO", "#E81123", "#22E81123");
                FooterText.Text = "! Verifique o log para detalhes.";
                WriteLog("ERRO: " + ex.Message, "ERR");
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
                BtnRun.IsEnabled = true;
                BtnRun.Content = "Executar Novamente";
                _running = false;
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogText.Clear();
            WriteLog("Log limpo.");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void SetBadge(string txt, string fg, string bg)
        {
            BadgeText.Text = txt;
            BadgeText.Foreground = ToBrush(fg);
            BadgeBorder.Background = ToBrush(bg);
        }

        private Brush ToBrush(string c)
        {
            try { return _brushConv.ConvertFromString(c) as Brush ?? Brushes.Transparent; }
            catch { return Brushes.Transparent; }
        }

        private void WriteLog(string msg, string lvl = "INFO")
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            var sym = lvl switch
            {
                "OK" => "+",
                "ERR" => "!",
                "WARN" => "*",
                _ => "-"
            };

            LogText.AppendText($"[{ts}] {sym}  {msg}{Environment.NewLine}");
            LogText.ScrollToEnd();
        }

        // Para ações em background chamarem log com segurança:
        private void WriteQ(string msg, string lvl = "INFO")
        {
            Dispatcher.Invoke(() => WriteLog(msg, lvl));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    // tentativa agressiva, mas ignorando erros por arquivo em uso
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                        try { File.Delete(f); } catch { }
                    }

                    foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                    {
                        try { Directory.Delete(d, true); } catch { }
                    }

                    try { Directory.Delete(path, true); } catch { }
                }
                else if (File.Exists(path))
                {
                    try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
                    try { File.Delete(path); } catch { }
                }
            }
            catch
            {
                // ignore
            }
        }

        private static async Task RunProcessNoCaptureAsync(string file, string args)
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                WorkingDirectory = Environment.SystemDirectory
            };
            p.Start();
            await p.WaitForExitAsync();
        }

        private static async Task<string> RunProcessCaptureAsync(string file, string args)
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = ProcessEncoding,
                StandardErrorEncoding = ProcessEncoding,
                WorkingDirectory = Environment.SystemDirectory
            };
            p.Start();

            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();

            var all = (stdout + Environment.NewLine + stderr).Trim();
            return string.IsNullOrWhiteSpace(all) ? "(sem saida)" : all;
        }

        private static Encoding GetPreferredProcessEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try
            {
                // Utilitarios do Windows (ex.: ipconfig) normalmente escrevem em code page OEM.
                int oemCp = unchecked((int)GetOEMCP());
                return Encoding.GetEncoding(oemCp);
            }
            catch
            {
                try { return Encoding.GetEncoding(1252); }
                catch { return Encoding.UTF8; }
            }
        }
    }
}
