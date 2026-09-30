using System.Text.Json;
using System.Text.Json.Serialization;

namespace PadronizadorTI.Configuracao;

public sealed class ErroConfiguracaoException(string mensagem, Exception? interna = null)
    : Exception(mensagem, interna);

/// <summary>
/// Lê o padronizacao.json e valida cada aplicativo. Itens com configuração
/// incompleta NÃO bloqueiam o programa: são convertidos em pendentes com o motivo.
/// </summary>
public static class CarregadorConfiguracao
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static ConfiguracaoPadronizacao Carregar(string caminho, out List<string> avisos)
    {
        if (!File.Exists(caminho))
            throw new ErroConfiguracaoException(
                $"Arquivo de configuração não encontrado:\n{caminho}\n\n" +
                "Copie config\\padronizacao.exemplo.json para essa pasta com o nome padronizacao.json.");

        ConfiguracaoPadronizacao? config;
        try
        {
            config = JsonSerializer.Deserialize<ConfiguracaoPadronizacao>(File.ReadAllText(caminho), Opcoes);
        }
        catch (JsonException ex)
        {
            throw new ErroConfiguracaoException(
                $"Erro de sintaxe no arquivo de configuração (linha {ex.LineNumber + 1}):\n{ex.Message}", ex);
        }

        if (config is null)
            throw new ErroConfiguracaoException("O arquivo de configuração está vazio.");
        if (config.Departamentos.Count == 0)
            throw new ErroConfiguracaoException("Nenhum departamento cadastrado em \"departamentos\".");

        avisos = [];
        var fontes = new HashSet<string>(config.FontesWinGetPermitidas, StringComparer.OrdinalIgnoreCase);

        foreach (var app in config.AplicativosComuns)
            Validar(app, "Comum", fontes, avisos);

        foreach (var depto in config.Departamentos)
        {
            if (string.IsNullOrWhiteSpace(depto.Nome))
                throw new ErroConfiguracaoException("Existe um departamento sem \"nome\".");
            foreach (var app in depto.Aplicativos)
                Validar(app, depto.Nome, fontes, avisos);
        }

        return config;
    }

    private static void Validar(Aplicativo app, string origem, HashSet<string> fontes, List<string> avisos)
    {
        if (string.IsNullOrWhiteSpace(app.Nome))
            app.Nome = "(sem nome)";

        if (app.Pendente)
        {
            if (string.IsNullOrWhiteSpace(app.MotivoPendencia))
                app.MotivoPendencia = "Marcado como pendente na configuração.";
            return;
        }

        var problema = ObterProblema(app, fontes);
        if (problema is null)
            return;

        app.Pendente = true;
        app.MotivoPendencia = "Configuração incompleta: " + problema;
        avisos.Add($"[{origem}] {app.Nome}: {problema}");
    }

    private static string? ObterProblema(Aplicativo app, HashSet<string> fontes)
    {
        if (app.TempoLimiteMinutos <= 0)
            return "\"tempoLimiteMinutos\" deve ser maior que zero.";

        if (app.Tipo == TipoInstalador.WinGet)
        {
            if (string.IsNullOrWhiteSpace(app.IdWinGet))
                return "informe \"idWinGet\".";
            if (!fontes.Contains(app.Fonte))
                return $"a fonte \"{app.Fonte}\" não está em \"fontesWinGetPermitidas\".";
            if (!string.IsNullOrWhiteSpace(app.Escopo) && app.Escopo is not ("machine" or "user"))
                return "\"escopo\" deve ser \"machine\", \"user\" ou vazio.";
            return ProblemaDeteccao(app.Deteccao, permiteAutomatica: true);
        }

        if (string.IsNullOrWhiteSpace(app.Caminho))
            return "informe \"caminho\" do instalador.";
        if (app.Caminho.Contains("://", StringComparison.Ordinal))
            return "\"caminho\" deve ser local ou de rede (UNC). Downloads por URL não são permitidos.";

        var extensao = Path.GetExtension(app.Caminho);
        if (app.Tipo == TipoInstalador.Msi && !extensao.Equals(".msi", StringComparison.OrdinalIgnoreCase))
            return "para \"tipo\": \"Msi\" o caminho deve terminar em .msi.";
        if (app.Tipo == TipoInstalador.Exe && !extensao.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return "para \"tipo\": \"Exe\" o caminho deve terminar em .exe.";
        if (!string.IsNullOrWhiteSpace(app.Sha256) && app.Sha256.Trim().Length != 64)
            return "\"sha256\" deve ter 64 caracteres hexadecimais.";

        return ProblemaDeteccao(app.Deteccao, permiteAutomatica: false);
    }

    private static string? ProblemaDeteccao(Deteccao? d, bool permiteAutomatica)
    {
        if (d is null || d.Tipo == TipoDeteccao.Automatica)
            return permiteAutomatica
                ? null
                : "instaladores .exe/.msi precisam de \"deteccao\" (ProgramaInstalado, CodigoProdutoMsi, Arquivo ou ChaveRegistro) para evitar reinstalação.";

        return d.Tipo switch
        {
            TipoDeteccao.ProgramaInstalado when string.IsNullOrWhiteSpace(d.NomeExibicao)
                => "a detecção ProgramaInstalado precisa de \"nomeExibicao\".",
            TipoDeteccao.CodigoProdutoMsi when !Guid.TryParse(d.CodigoProduto, out _)
                => "a detecção CodigoProdutoMsi precisa de \"codigoProduto\" no formato {GUID}.",
            TipoDeteccao.Arquivo when string.IsNullOrWhiteSpace(d.Caminho)
                => "a detecção Arquivo precisa de \"caminho\".",
            TipoDeteccao.ChaveRegistro when string.IsNullOrWhiteSpace(d.Caminho)
                => "a detecção ChaveRegistro precisa de \"caminho\" (ex.: HKLM\\SOFTWARE\\Empresa\\Produto).",
            _ => null,
        };
    }
}
