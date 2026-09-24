using System.Security.Cryptography;
using PadronizadorTI.Configuracao;

namespace PadronizadorTI.Sistema;

/// <summary>Executa instaladores .exe e .msi que estão no disco local ou em uma pasta de rede (UNC).</summary>
public static class InstaladorLocal
{
    /// <summary>Confere se o instalador existe e, se configurado, se o SHA-256 confere. Não executa nada.</summary>
    public static async Task<(bool Ok, string Detalhes)> VerificarArquivoAsync(Aplicativo app)
    {
        var caminho = Environment.ExpandEnvironmentVariables(app.Caminho!);
        if (!File.Exists(caminho))
            return (false, $"Instalador não encontrado ou sem acesso: {caminho}" +
                           (caminho.StartsWith(@"\\") ? " (confira a permissão do usuário administrador no compartilhamento)." : "."));

        if (string.IsNullOrWhiteSpace(app.Sha256))
            return (true, $"Instalador encontrado: {caminho} (sem verificação de SHA-256 configurada).");

        var hash = await CalcularSha256Async(caminho);
        return hash.Equals(app.Sha256.Trim(), StringComparison.OrdinalIgnoreCase)
            ? (true, $"Instalador encontrado e SHA-256 confere: {caminho}")
            : (false, $"SHA-256 NÃO confere para {caminho}. Esperado {app.Sha256.Trim().ToUpperInvariant()}, obtido {hash}. Instalação bloqueada.");
    }

    public static string DescreverComando(Aplicativo app, string? pastaLogs = null) => app.Tipo == TipoInstalador.Msi
        ? $"msiexec.exe {ArgumentosMsi(Environment.ExpandEnvironmentVariables(app.Caminho!), app, pastaLogs)}"
        : $"\"{Environment.ExpandEnvironmentVariables(app.Caminho!)}\" {app.Argumentos}".TrimEnd();

    public static async Task<ResultadoAcao> InstalarAsync(Aplicativo app, string pastaLogs)
    {
        var (ok, detalhesArquivo) = await VerificarArquivoAsync(app);
        if (!ok)
            return new ResultadoAcao(false, false, detalhesArquivo);

        var original = Environment.ExpandEnvironmentVariables(app.Caminho!);
        var caminho = original;
        string? pastaTemp = null;
        try
        {
            if (app.CopiarParaTemp)
            {
                pastaTemp = Path.Combine(Path.GetTempPath(), "PadronizadorTI", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(pastaTemp);
                caminho = Path.Combine(pastaTemp, Path.GetFileName(original));
                File.Copy(original, caminho);

                // Confere de novo depois da cópia, para garantir que o arquivo executado é o verificado.
                if (!string.IsNullOrWhiteSpace(app.Sha256) &&
                    !(await CalcularSha256Async(caminho)).Equals(app.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                    return new ResultadoAcao(false, false, "SHA-256 da cópia temporária não confere. Instalação bloqueada.");
            }

            var tempoLimite = TimeSpan.FromMinutes(app.TempoLimiteMinutos);
            var r = app.Tipo == TipoInstalador.Msi
                ? await ExecutorProcesso.ExecutarAsync(Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                                                       ArgumentosMsi(caminho, app, pastaLogs), tempoLimite)
                : await ExecutorProcesso.ExecutarAsync(caminho, app.Argumentos ?? "", tempoLimite, Path.GetDirectoryName(caminho));

            if (r.TempoEsgotado)
                return new ResultadoAcao(false, false, $"Tempo limite de {app.TempoLimiteMinutos} min esgotado; processo encerrado.");

            if (app.CodigosReinicio.Contains(r.CodigoSaida))
                return new ResultadoAcao(true, true, $"Código {r.CodigoSaida}: instalado, reinício necessário.");
            if (app.CodigosSucesso.Contains(r.CodigoSaida))
                return new ResultadoAcao(true, false, $"Código {r.CodigoSaida}: instalação concluída.");

            var dica = r.CodigoSaida switch
            {
                1618 => " (outra instalação do Windows Installer está em andamento)",
                1603 => " (erro fatal do MSI; veja o log detalhado do msiexec)",
                1619 => " (pacote MSI não pôde ser aberto)",
                1639 => " (argumentos inválidos para o msiexec)",
                _ => "",
            };
            var saida = r.Resumo();
            return new ResultadoAcao(false, false,
                $"Instalador retornou código {r.CodigoSaida}{dica}.{(saida.Length > 0 ? Environment.NewLine + saida : "")}");
        }
        finally
        {
            if (pastaTemp is not null)
                try { Directory.Delete(pastaTemp, recursive: true); } catch { /* arquivo ainda em uso: ignora */ }
        }
    }

    private static string ArgumentosMsi(string caminho, Aplicativo app, string? pastaLogs)
    {
        var args = $"/i \"{caminho}\" /qn /norestart";
        if (pastaLogs is not null)
        {
            var nomeLog = $"msi_{Environment.MachineName}_{SomenteLetras(app.Nome)}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            args += $" /L*v \"{Path.Combine(pastaLogs, nomeLog)}\"";
        }
        return string.IsNullOrWhiteSpace(app.Argumentos) ? args : $"{args} {app.Argumentos}";
    }

    private static string SomenteLetras(string texto) =>
        new(texto.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    private static async Task<string> CalcularSha256Async(string caminho)
    {
        await using var fluxo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        return Convert.ToHexString(await SHA256.HashDataAsync(fluxo));
    }
}
