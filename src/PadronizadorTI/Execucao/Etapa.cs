using PadronizadorTI.Configuracao;

namespace PadronizadorTI.Execucao;

public enum TipoEtapa
{
    PlanoEnergia,
    TemposTomada,
    TemposBateria,
    Aplicativo,
}

public enum StatusEtapa
{
    Aguardando,
    Executando,
    Instalado,
    JaExistente,
    ConfiguracaoAplicada,
    NaoAlterado,
    SeriaInstalado,
    SeriaAplicado,
    Pendente,
    Falha,
    Cancelada,
}

/// <summary>Aviso de progresso com o status no momento do envio (a tela recebe de forma assíncrona).</summary>
public sealed record ProgressoEtapa(Etapa Etapa, StatusEtapa Status);

/// <summary>Uma linha do plano de ações mostrado na tela e registrado no log.</summary>
public sealed class Etapa
{
    public int Numero { get; init; }
    public required string Nome { get; init; }
    public required string Origem { get; init; }
    public required TipoEtapa Tipo { get; init; }
    public required string Acao { get; init; }
    public Aplicativo? Aplicativo { get; init; }

    public StatusEtapa Status { get; set; } = StatusEtapa.Aguardando;
    public string Detalhes { get; set; } = "";
    public bool ExigeReinicio { get; set; }

    public static string Texto(StatusEtapa s) => s switch
    {
        StatusEtapa.Aguardando => "Aguardando",
        StatusEtapa.Executando => "Executando...",
        StatusEtapa.Instalado => "Instalado",
        StatusEtapa.JaExistente => "Já existente",
        StatusEtapa.ConfiguracaoAplicada => "Configuração aplicada",
        StatusEtapa.NaoAlterado => "Não alterado",
        StatusEtapa.SeriaInstalado => "Seria instalado (simulação)",
        StatusEtapa.SeriaAplicado => "Seria aplicado (simulação)",
        StatusEtapa.Pendente => "Pendente",
        StatusEtapa.Falha => "FALHA",
        StatusEtapa.Cancelada => "Cancelada",
        _ => s.ToString(),
    };
}
