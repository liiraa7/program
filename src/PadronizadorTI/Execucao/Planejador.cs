using PadronizadorTI.Configuracao;
using PadronizadorTI.Sistema;

namespace PadronizadorTI.Execucao;

/// <summary>Monta a lista de etapas: configurações comuns, aplicativos comuns e aplicativos do departamento.</summary>
public static class Planejador
{
    public static List<Etapa> Montar(ConfiguracaoPadronizacao config, Departamento departamento)
    {
        var etapas = new List<Etapa>();
        int n = 0;
        var energia = config.Energia;

        if (energia.Aplicar)
        {
            if (energia.AtivarAltoDesempenho)
                etapas.Add(new Etapa
                {
                    Numero = ++n, Nome = "Plano de energia: Alto desempenho", Origem = "Comum",
                    Tipo = TipoEtapa.PlanoEnergia, Acao = "powercfg /setactive (Alto desempenho)",
                });

            etapas.Add(new Etapa
            {
                Numero = ++n, Nome = "Energia na tomada", Origem = "Comum", Tipo = TipoEtapa.TemposTomada,
                Acao = Energia.DescreverTempos(energia.Tomada),
            });

            var bateria = new Etapa
            {
                Numero = ++n, Nome = "Energia na bateria", Origem = "Comum", Tipo = TipoEtapa.TemposBateria,
                Acao = energia.Bateria.Aplicar
                    ? Energia.DescreverTempos(energia.Bateria)
                    : "Não alterar (\"bateria.aplicar\" = false)",
            };
            if (!energia.Bateria.Aplicar)
            {
                bateria.Status = StatusEtapa.NaoAlterado;
                bateria.Detalhes = "Configurações de bateria mantidas como estão. Ajuste manualmente se necessário.";
            }
            etapas.Add(bateria);
        }

        foreach (var app in config.AplicativosComuns)
            etapas.Add(CriarEtapaAplicativo(++n, app, "Comum"));
        foreach (var app in departamento.Aplicativos)
            etapas.Add(CriarEtapaAplicativo(++n, app, departamento.Nome));

        return etapas;
    }

    private static Etapa CriarEtapaAplicativo(int numero, Aplicativo app, string origem)
    {
        var etapa = new Etapa
        {
            Numero = numero, Nome = app.Nome, Origem = origem, Tipo = TipoEtapa.Aplicativo, Aplicativo = app,
            Acao = app.Pendente ? "—" : DescreverInstalacao(app),
        };
        if (app.Pendente)
        {
            etapa.Status = StatusEtapa.Pendente;
            etapa.Detalhes = app.MotivoPendencia ?? "";
        }
        return etapa;
    }

    public static string DescreverInstalacao(Aplicativo app) => app.Tipo switch
    {
        TipoInstalador.WinGet => $"winget {WinGet.MontarArgumentosInstalacao(app)}",
        _ => InstaladorLocal.DescreverComando(app),
    };
}
