using System.Text;
using PadronizadorTI.Configuracao;

namespace PadronizadorTI.Sistema;

/// <param name="SemAlteracao">true quando o item já estava no estado desejado.</param>
public sealed record ResultadoAcao(bool Sucesso, bool ExigeReinicio, string Detalhes, bool SemAlteracao = false);

/// <summary>
/// Integração com o WinGet (Gerenciador de Pacotes do Windows). Usa somente as fontes
/// liberadas em "fontesWinGetPermitidas" (por padrão, apenas a fonte oficial "winget").
/// </summary>
public static class WinGet
{
    // Códigos de retorno documentados do WinGet (https://aka.ms/winget-returncodes)
    private const int NenhumPacoteEncontrado = unchecked((int)0x8A150014);
    private const int PacoteJaInstalado = unchecked((int)0x8A150061);
    private const int ReinicioNecessarioParaConcluir = unchecked((int)0x8A150109);

    private static readonly TimeSpan TempoLimiteConsulta = TimeSpan.FromMinutes(3);
    private static string? _caminho;

    public static string? Localizar()
    {
        if (_caminho is not null && File.Exists(_caminho))
            return _caminho;

        var candidatos = new List<string>();
        foreach (var pasta in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            candidatos.Add(Path.Combine(pasta.Trim(), "winget.exe"));
        candidatos.Add(Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe"));
        try
        {
            var windowsApps = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\WindowsApps");
            candidatos.AddRange(Directory.GetDirectories(windowsApps, "Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe")
                                         .OrderDescending()
                                         .Select(p => Path.Combine(p, "winget.exe")));
        }
        catch { /* pasta WindowsApps protegida: ignora */ }

        _caminho = candidatos.FirstOrDefault(File.Exists);
        return _caminho;
    }

    public static async Task<ResultadoDeteccao> EstaInstaladoAsync(Aplicativo app)
    {
        var winget = Localizar();
        if (winget is null)
            return new ResultadoDeteccao(null, MensagemAusente);

        var args = $"list --id \"{app.IdWinGet}\" --exact --source {app.Fonte} --accept-source-agreements --disable-interactivity";
        var r = await ExecutorProcesso.ExecutarAsync(winget, args, TempoLimiteConsulta, codificacao: Encoding.UTF8);

        if (r.TempoEsgotado)
            return new ResultadoDeteccao(null, "Tempo esgotado ao consultar o WinGet.");
        // Com --id e --exact, código 0 significa que o pacote foi encontrado entre os instalados.
        if (r.CodigoSaida == 0)
            return new ResultadoDeteccao(true, $"WinGet: pacote {app.IdWinGet} já instalado.");
        if (r.CodigoSaida == NenhumPacoteEncontrado)
            return new ResultadoDeteccao(false, $"WinGet: pacote {app.IdWinGet} não instalado.");

        return new ResultadoDeteccao(null, $"WinGet retornou {Hex(r.CodigoSaida)} ao consultar {app.IdWinGet}:{Environment.NewLine}{r.Resumo()}");
    }

    public static string MontarArgumentosInstalacao(Aplicativo app)
    {
        var sb = new StringBuilder($"install --id \"{app.IdWinGet}\" --exact --source {app.Fonte}");
        if (!string.IsNullOrWhiteSpace(app.Versao)) sb.Append($" --version \"{app.Versao}\"");
        if (!string.IsNullOrWhiteSpace(app.Escopo)) sb.Append($" --scope {app.Escopo}");
        sb.Append(" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity");
        return sb.ToString();
    }

    public static async Task<ResultadoAcao> InstalarAsync(Aplicativo app)
    {
        var winget = Localizar();
        if (winget is null)
            return new ResultadoAcao(false, false, MensagemAusente);

        var r = await ExecutorProcesso.ExecutarAsync(
            winget, MontarArgumentosInstalacao(app), TimeSpan.FromMinutes(app.TempoLimiteMinutos), codificacao: Encoding.UTF8);

        if (r.TempoEsgotado)
            return new ResultadoAcao(false, false, $"Tempo limite de {app.TempoLimiteMinutos} min esgotado; processo encerrado.");

        return r.CodigoSaida switch
        {
            0 => new ResultadoAcao(true, false, "WinGet concluiu a instalação."),
            PacoteJaInstalado => new ResultadoAcao(true, false, "WinGet informou que o pacote já estava instalado."),
            ReinicioNecessarioParaConcluir => new ResultadoAcao(true, true, "Instalado; o Windows precisa reiniciar para concluir."),
            _ => new ResultadoAcao(false, false, $"WinGet retornou {Hex(r.CodigoSaida)}:{Environment.NewLine}{r.Resumo()}"),
        };
    }

    public const string MensagemAusente =
        "WinGet não encontrado. Atualize o \"Instalador de Aplicativo\" (App Installer) pela Microsoft Store " +
        "ou pelo Windows Update e execute novamente.";

    private static string Hex(int codigo) => $"0x{codigo:X8} ({codigo})";
}
