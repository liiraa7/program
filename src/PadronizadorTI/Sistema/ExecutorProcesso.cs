using System.Diagnostics;
using System.Text;

namespace PadronizadorTI.Sistema;

public sealed record ResultadoProcesso(int CodigoSaida, string Saida, bool TempoEsgotado)
{
    /// <summary>Últimas linhas da saída, para o log não ficar enorme.</summary>
    public string Resumo(int maxLinhas = 15)
    {
        var linhas = Saida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .Where(l => l.Length > 1 && !l.All(c => c is '-' or '\\' or '|' or '/' or '█' or '▒' or ' '))
                          .ToArray();
        return string.Join(Environment.NewLine, linhas.TakeLast(maxLinhas));
    }
}

/// <summary>Executa processos sem janela, capturando a saída e respeitando um tempo limite.</summary>
public static class ExecutorProcesso
{
    public static async Task<ResultadoProcesso> ExecutarAsync(
        string arquivo, string argumentos, TimeSpan tempoLimite, string? pastaTrabalho = null, Encoding? codificacao = null)
    {
        var inicio = new ProcessStartInfo(arquivo, argumentos)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = pastaTrabalho ?? Environment.SystemDirectory,
        };
        if (codificacao is not null)
        {
            inicio.StandardOutputEncoding = codificacao;
            inicio.StandardErrorEncoding = codificacao;
        }

        var saida = new StringBuilder();
        using var processo = new Process { StartInfo = inicio };
        processo.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (saida) saida.AppendLine(e.Data); };
        processo.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (saida) saida.AppendLine(e.Data); };

        processo.Start();
        processo.BeginOutputReadLine();
        processo.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(tempoLimite);
        try
        {
            await processo.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { processo.Kill(entireProcessTree: true); } catch { /* processo já terminou */ }
            lock (saida) return new ResultadoProcesso(-1, saida.ToString(), TempoEsgotado: true);
        }

        processo.WaitForExit(); // garante que toda a saída assíncrona foi lida
        lock (saida) return new ResultadoProcesso(processo.ExitCode, saida.ToString(), TempoEsgotado: false);
    }
}
