using System.Text;

namespace PadronizadorTI.Execucao;

/// <summary>
/// Log de uma execução. Um arquivo por execução:
/// NOMEPC_Departamento_aaaa-MM-dd_HH-mm-ss_SIMULACAO|EXECUCAO.log
/// </summary>
public sealed class RegistroExecucao : IDisposable
{
    private readonly StreamWriter _escritor;
    private readonly object _trava = new();

    public string CaminhoArquivo { get; }
    public string Pasta { get; }
    public event Action<string>? LinhaEscrita;

    public RegistroExecucao(string pastaConfigurada, string departamento, bool simulacao)
    {
        Pasta = PrepararPasta(pastaConfigurada);
        var modo = simulacao ? "SIMULACAO" : "EXECUCAO";
        var nome = $"{Environment.MachineName}_{Limpar(departamento)}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{modo}.log";
        CaminhoArquivo = Path.Combine(Pasta, nome);
        _escritor = new StreamWriter(CaminhoArquivo, append: false, new UTF8Encoding(true)) { AutoFlush = true };
    }

    public void Escrever(string mensagem)
    {
        var linha = $"[{DateTime.Now:HH:mm:ss}] {mensagem}";
        lock (_trava) _escritor.WriteLine(linha);
        LinhaEscrita?.Invoke(linha);
    }

    public void EscreverBloco(string texto)
    {
        lock (_trava) _escritor.WriteLine(texto);
        LinhaEscrita?.Invoke(texto);
    }

    /// <summary>Copia o log para a pasta de rede, se configurada. Falhas não interrompem nada.</summary>
    public string? CopiarParaRede(string? pastaRede)
    {
        if (string.IsNullOrWhiteSpace(pastaRede)) return null;
        try
        {
            var destino = Environment.ExpandEnvironmentVariables(pastaRede);
            Directory.CreateDirectory(destino);
            var arquivo = Path.Combine(destino, Path.GetFileName(CaminhoArquivo));
            lock (_trava)
            {
                _escritor.Flush();
                File.Copy(CaminhoArquivo, arquivo, overwrite: true);
            }
            return $"Cópia do log enviada para {arquivo}";
        }
        catch (Exception ex)
        {
            return $"Não foi possível copiar o log para a rede ({pastaRede}): {ex.Message}";
        }
    }

    public void Dispose()
    {
        lock (_trava) _escritor.Dispose();
    }

    private static string PrepararPasta(string configurada)
    {
        var pasta = Environment.ExpandEnvironmentVariables(configurada);
        try
        {
            Directory.CreateDirectory(pasta);
            return pasta;
        }
        catch
        {
            // Sem permissão na pasta configurada (ex.: simulação sem administrador): usa a pasta do usuário.
            var alternativa = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PadronizadorTI", "Logs");
            Directory.CreateDirectory(alternativa);
            return alternativa;
        }
    }

    private static string Limpar(string texto)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        return new string(texto.Select(c => invalidos.Contains(c) || c == ' ' ? '-' : c).ToArray());
    }
}
