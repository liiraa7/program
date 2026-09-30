using Microsoft.Win32;
using PadronizadorTI.Configuracao;

namespace PadronizadorTI.Sistema;

/// <param name="Instalado">true/false, ou null quando não foi possível determinar.</param>
public sealed record ResultadoDeteccao(bool? Instalado, string Detalhes);

/// <summary>Verificações somente leitura para saber se um aplicativo já está instalado.</summary>
public static class DetectorInstalacao
{
    private const string ChaveUninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static async Task<ResultadoDeteccao> VerificarAsync(Aplicativo app)
    {
        var d = app.Deteccao;
        if (d is null || d.Tipo == TipoDeteccao.Automatica)
        {
            return app.Tipo == TipoInstalador.WinGet
                ? await WinGet.EstaInstaladoAsync(app)
                : new ResultadoDeteccao(null, "Detecção não configurada.");
        }

        return d.Tipo switch
        {
            TipoDeteccao.ProgramaInstalado => ProgramaInstalado(d.NomeExibicao!, d.Comparacao),
            TipoDeteccao.CodigoProdutoMsi => CodigoProdutoMsi(d.CodigoProduto!),
            TipoDeteccao.Arquivo => Arquivo(d.Caminho!),
            TipoDeteccao.ChaveRegistro => ChaveRegistro(d.Caminho!, d.NomeValor),
            _ => new ResultadoDeteccao(null, $"Tipo de detecção desconhecido: {d.Tipo}"),
        };
    }

    private static ResultadoDeteccao ProgramaInstalado(string nome, Comparacao comparacao)
    {
        foreach (var (raiz, visao) in RaizesUninstall())
        {
            using var baseKey = RegistryKey.OpenBaseKey(raiz, visao);
            using var uninstall = baseKey.OpenSubKey(ChaveUninstall);
            if (uninstall is null) continue;

            foreach (var sub in uninstall.GetSubKeyNames())
            {
                using var chave = uninstall.OpenSubKey(sub);
                if (chave?.GetValue("DisplayName") is not string exibido) continue;

                bool corresponde = comparacao switch
                {
                    Comparacao.Igual => exibido.Equals(nome, StringComparison.OrdinalIgnoreCase),
                    Comparacao.IniciaCom => exibido.StartsWith(nome, StringComparison.OrdinalIgnoreCase),
                    _ => exibido.Contains(nome, StringComparison.OrdinalIgnoreCase),
                };
                if (corresponde)
                {
                    var versao = chave.GetValue("DisplayVersion") as string;
                    return new ResultadoDeteccao(true, $"Encontrado: \"{exibido}\"{(versao is null ? "" : $" versão {versao}")}.");
                }
            }
        }
        return new ResultadoDeteccao(false, $"\"{nome}\" não encontrado em Programas e Recursos.");
    }

    private static ResultadoDeteccao CodigoProdutoMsi(string codigo)
    {
        var guid = Guid.Parse(codigo).ToString("B").ToUpperInvariant();
        foreach (var (raiz, visao) in RaizesUninstall())
        {
            using var baseKey = RegistryKey.OpenBaseKey(raiz, visao);
            using var chave = baseKey.OpenSubKey($@"{ChaveUninstall}\{guid}");
            if (chave is not null)
                return new ResultadoDeteccao(true, $"ProductCode {guid} encontrado ({chave.GetValue("DisplayName")} {chave.GetValue("DisplayVersion")}).");
        }
        return new ResultadoDeteccao(false, $"ProductCode {guid} não encontrado.");
    }

    private static ResultadoDeteccao Arquivo(string caminho)
    {
        var expandido = Environment.ExpandEnvironmentVariables(caminho);
        return File.Exists(expandido)
            ? new ResultadoDeteccao(true, $"Arquivo encontrado: {expandido}")
            : new ResultadoDeteccao(false, $"Arquivo não encontrado: {expandido}");
    }

    private static ResultadoDeteccao ChaveRegistro(string caminho, string? nomeValor)
    {
        var separador = caminho.IndexOf('\\');
        if (separador < 0)
            return new ResultadoDeteccao(null, $"Caminho de registro inválido: {caminho}");

        RegistryHive? raiz = caminho[..separador].ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            _ => null,
        };
        if (raiz is null)
            return new ResultadoDeteccao(null, $"Use HKLM\\ ou HKCU\\ no início do caminho: {caminho}");

        var subcaminho = caminho[(separador + 1)..];
        foreach (var visao in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(raiz.Value, visao);
            using var chave = baseKey.OpenSubKey(subcaminho);
            if (chave is null) continue;
            if (string.IsNullOrEmpty(nomeValor) || chave.GetValue(nomeValor) is not null)
                return new ResultadoDeteccao(true, $"Registro encontrado: {caminho}{(string.IsNullOrEmpty(nomeValor) ? "" : $" [{nomeValor}]")}");
        }
        return new ResultadoDeteccao(false, $"Registro não encontrado: {caminho}{(string.IsNullOrEmpty(nomeValor) ? "" : $" [{nomeValor}]")}");
    }

    private static IEnumerable<(RegistryHive, RegistryView)> RaizesUninstall()
    {
        yield return (RegistryHive.LocalMachine, RegistryView.Registry64);
        yield return (RegistryHive.LocalMachine, RegistryView.Registry32);
        yield return (RegistryHive.CurrentUser, RegistryView.Default);
    }
}
