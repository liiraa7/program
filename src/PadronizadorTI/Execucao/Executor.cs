using System.Text;
using PadronizadorTI.Configuracao;
using PadronizadorTI.Sistema;

namespace PadronizadorTI.Execucao;

public sealed class ResumoExecucao
{
    public required bool Simulacao { get; init; }
    public required string CaminhoLog { get; init; }
    public int Instalados { get; set; }
    public int JaExistentes { get; set; }
    public int ConfiguracoesAplicadas { get; set; }
    public int SeriamExecutados { get; set; }
    public int Pendentes { get; set; }
    public int Falhas { get; set; }
    public int Canceladas { get; set; }
    public List<string> MotivosReinicio { get; } = [];
    public List<string> ItensPendentes { get; } = [];
    public List<string> ItensComFalha { get; } = [];

    public string Texto()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Simulacao ? "SIMULAÇÃO concluída — nada foi alterado." : "Execução concluída.");
        sb.AppendLine();
        if (Simulacao)
            sb.AppendLine($"Seriam instalados/aplicados: {SeriamExecutados}");
        else
        {
            sb.AppendLine($"Instalados: {Instalados}");
            sb.AppendLine($"Configurações aplicadas: {ConfiguracoesAplicadas}");
        }
        sb.AppendLine($"Já existentes: {JaExistentes}");
        sb.AppendLine($"Pendentes: {Pendentes}");
        sb.AppendLine($"Com falha: {Falhas}");
        if (Canceladas > 0) sb.AppendLine($"Canceladas: {Canceladas}");

        if (ItensComFalha.Count > 0)
        {
            sb.AppendLine().AppendLine("Falhas:");
            foreach (var f in ItensComFalha) sb.AppendLine("  • " + f);
        }
        if (ItensPendentes.Count > 0)
        {
            sb.AppendLine().AppendLine("Pendentes de configuração:");
            foreach (var p in ItensPendentes) sb.AppendLine("  • " + p);
        }
        if (MotivosReinicio.Count > 0)
        {
            sb.AppendLine().AppendLine("⚠ REINÍCIO NECESSÁRIO (o programa NÃO reinicia automaticamente):");
            foreach (var m in MotivosReinicio.Distinct()) sb.AppendLine("  • " + m);
        }
        sb.AppendLine().AppendLine($"Log: {CaminhoLog}");
        return sb.ToString();
    }
}

/// <summary>
/// Executa as etapas em ordem. Uma falha em um item é registrada e a execução segue
/// para o próximo item, pois as instalações são independentes entre si.
/// O cancelamento só acontece entre etapas, nunca no meio de uma instalação.
/// </summary>
public sealed class Executor(ConfiguracaoPadronizacao config, RegistroExecucao log, bool simulacao)
{
    public async Task<ResumoExecucao> ExecutarAsync(
        IReadOnlyList<Etapa> etapas, Departamento departamento, string caminhoConfig,
        IProgress<ProgressoEtapa> progresso, CancellationToken cancelamento)
    {
        var reinicioAntes = DetectorReinicio.ObterPendencias();
        EscreverCabecalho(departamento, caminhoConfig, etapas, reinicioAntes);

        foreach (var etapa in etapas)
        {
            if (cancelamento.IsCancellationRequested && etapa.Status == StatusEtapa.Aguardando)
            {
                etapa.Status = StatusEtapa.Cancelada;
                etapa.Detalhes = "Cancelada pelo usuário antes de iniciar.";
                progresso.Report(new ProgressoEtapa(etapa, etapa.Status));
                continue;
            }

            if (etapa.Status != StatusEtapa.Aguardando)
            {
                log.Escrever($"#{etapa.Numero} {etapa.Nome}: {Etapa.Texto(etapa.Status)} — {etapa.Detalhes}");
                progresso.Report(new ProgressoEtapa(etapa, etapa.Status));
                continue;
            }

            etapa.Status = StatusEtapa.Executando;
            progresso.Report(new ProgressoEtapa(etapa, etapa.Status));
            log.Escrever($"#{etapa.Numero} {etapa.Nome} [{etapa.Origem}] — {etapa.Acao}");

            try
            {
                await ExecutarEtapaAsync(etapa);
            }
            catch (Exception ex)
            {
                etapa.Status = StatusEtapa.Falha;
                etapa.Detalhes = $"Erro inesperado: {ex.Message}";
            }

            log.Escrever($"    → {Etapa.Texto(etapa.Status)}: {etapa.Detalhes}");
            progresso.Report(new ProgressoEtapa(etapa, etapa.Status));
        }

        var resumo = MontarResumo(etapas, reinicioAntes);
        EscreverRodape(etapas, resumo);
        var copia = log.CopiarParaRede(config.PastaLogsRede);
        if (copia is not null) log.Escrever(copia);
        return resumo;
    }

    private async Task ExecutarEtapaAsync(Etapa etapa)
    {
        switch (etapa.Tipo)
        {
            case TipoEtapa.PlanoEnergia:
                Aplicar(etapa, await Energia.AtivarAltoDesempenhoAsync(simulacao));
                break;
            case TipoEtapa.TemposTomada:
                Aplicar(etapa, await Energia.AplicarTemposAsync(config.Energia.Tomada, tomada: true, simulacao));
                break;
            case TipoEtapa.TemposBateria:
                Aplicar(etapa, await Energia.AplicarTemposAsync(config.Energia.Bateria, tomada: false, simulacao));
                break;
            case TipoEtapa.Aplicativo:
                await InstalarAplicativoAsync(etapa, etapa.Aplicativo!);
                break;
        }
    }

    private void Aplicar(Etapa etapa, ResultadoAcao r)
    {
        etapa.Detalhes = r.Detalhes;
        etapa.ExigeReinicio = r.ExigeReinicio;
        etapa.Status = !r.Sucesso ? StatusEtapa.Falha
            : r.SemAlteracao ? StatusEtapa.JaExistente
            : simulacao ? StatusEtapa.SeriaAplicado
            : StatusEtapa.ConfiguracaoAplicada;
    }

    private async Task InstalarAplicativoAsync(Etapa etapa, Aplicativo app)
    {
        // 1) Já está instalado? (somente leitura)
        var antes = await DetectorInstalacao.VerificarAsync(app);
        log.Escrever($"    Detecção: {antes.Detalhes}");

        if (antes.Instalado == true)
        {
            etapa.Status = StatusEtapa.JaExistente;
            etapa.Detalhes = antes.Detalhes;
            return;
        }
        if (antes.Instalado is null)
        {
            // Sem certeza, não instala: evita reinstalações e sobreposições.
            etapa.Status = StatusEtapa.Falha;
            etapa.Detalhes = "Não foi possível verificar se já está instalado; instalação não realizada. " + antes.Detalhes;
            return;
        }

        // 2) Simulação: só confere se o instalador está acessível.
        if (simulacao)
        {
            if (app.Tipo == TipoInstalador.WinGet)
            {
                etapa.Status = StatusEtapa.SeriaInstalado;
                etapa.Detalhes = "Seria executado: " + etapa.Acao;
                return;
            }
            var (ok, detalhes) = await InstaladorLocal.VerificarArquivoAsync(app);
            etapa.Status = ok ? StatusEtapa.SeriaInstalado : StatusEtapa.Falha;
            etapa.Detalhes = ok ? $"{detalhes} Seria executado: {etapa.Acao}" : detalhes;
            return;
        }

        // 3) Instalação real.
        var r = app.Tipo == TipoInstalador.WinGet
            ? await WinGet.InstalarAsync(app)
            : await InstaladorLocal.InstalarAsync(app, log.Pasta);

        etapa.ExigeReinicio = r.ExigeReinicio;
        if (!r.Sucesso)
        {
            etapa.Status = StatusEtapa.Falha;
            etapa.Detalhes = r.Detalhes;
            return;
        }

        // 4) Confirma pela mesma regra de detecção.
        var depois = await DetectorInstalacao.VerificarAsync(app);
        if (depois.Instalado == false && !r.ExigeReinicio)
        {
            etapa.Status = StatusEtapa.Falha;
            etapa.Detalhes = $"{r.Detalhes} Porém a detecção não encontrou o programa depois da instalação — " +
                             $"revise os argumentos silenciosos e a regra de detecção. ({depois.Detalhes})";
            return;
        }

        etapa.Status = StatusEtapa.Instalado;
        etapa.Detalhes = $"{r.Detalhes} {depois.Detalhes}";
    }

    private ResumoExecucao MontarResumo(IReadOnlyList<Etapa> etapas, List<string> reinicioAntes)
    {
        var resumo = new ResumoExecucao { Simulacao = simulacao, CaminhoLog = log.CaminhoArquivo };
        foreach (var e in etapas)
        {
            switch (e.Status)
            {
                case StatusEtapa.Instalado: resumo.Instalados++; break;
                case StatusEtapa.JaExistente: resumo.JaExistentes++; break;
                case StatusEtapa.ConfiguracaoAplicada: resumo.ConfiguracoesAplicadas++; break;
                case StatusEtapa.SeriaInstalado or StatusEtapa.SeriaAplicado: resumo.SeriamExecutados++; break;
                case StatusEtapa.Cancelada: resumo.Canceladas++; break;
                case StatusEtapa.Pendente:
                    resumo.Pendentes++;
                    resumo.ItensPendentes.Add($"{e.Nome} ({e.Origem}): {e.Detalhes}");
                    break;
                case StatusEtapa.Falha:
                    resumo.Falhas++;
                    resumo.ItensComFalha.Add($"{e.Nome} ({e.Origem}): {Primeira(e.Detalhes)}");
                    break;
            }
            if (e.ExigeReinicio)
                resumo.MotivosReinicio.Add($"Instalação de {e.Nome}");
        }

        if (!simulacao)
        {
            foreach (var motivo in DetectorReinicio.ObterPendencias())
                resumo.MotivosReinicio.Add(reinicioAntes.Contains(motivo) ? $"{motivo} (já pendente antes da execução)" : motivo);
        }
        else if (reinicioAntes.Count > 0)
        {
            resumo.MotivosReinicio.AddRange(reinicioAntes.Select(m => $"{m} (já pendente antes da simulação)"));
        }
        return resumo;
    }

    private void EscreverCabecalho(Departamento depto, string caminhoConfig, IReadOnlyList<Etapa> etapas, List<string> reinicioAntes)
    {
        var sb = new StringBuilder();
        sb.AppendLine(new string('=', 78));
        sb.AppendLine(" PADRONIZADOR DE ESTAÇÕES - TI");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine($" Computador     : {Environment.MachineName}");
        sb.AppendLine($" Usuário        : {Environment.UserDomainName}\\{Environment.UserName}");
        sb.AppendLine($" Administrador  : {(Administrador.EstaElevado() ? "Sim" : "Não")}");
        sb.AppendLine($" Windows        : {Environment.OSVersion.VersionString}");
        sb.AppendLine($" Departamento   : {depto.Nome}");
        sb.AppendLine($" Modo           : {(simulacao ? "SIMULAÇÃO (nenhuma alteração)" : "EXECUÇÃO")}");
        sb.AppendLine($" Data/hora      : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($" Configuração   : {caminhoConfig}");
        sb.AppendLine($" WinGet         : {WinGet.Localizar() ?? "NÃO ENCONTRADO"}");
        sb.AppendLine($" Reinício pend. : {(reinicioAntes.Count == 0 ? "Não" : string.Join("; ", reinicioAntes))}");
        sb.AppendLine($" Etapas         : {etapas.Count}");
        sb.AppendLine(new string('-', 78));
        log.EscreverBloco(sb.ToString());
    }

    private void EscreverRodape(IReadOnlyList<Etapa> etapas, ResumoExecucao resumo)
    {
        var sb = new StringBuilder();
        sb.AppendLine(new string('-', 78));
        sb.AppendLine(" RESULTADO POR ETAPA");
        sb.AppendLine(new string('-', 78));
        foreach (var e in etapas)
            sb.AppendLine($" {e.Numero,3}. {Etapa.Texto(e.Status),-28} {e.Nome} [{e.Origem}]");
        sb.AppendLine(new string('-', 78));
        sb.AppendLine(resumo.Texto());
        sb.AppendLine(new string('=', 78));
        log.EscreverBloco(sb.ToString());
    }

    private static string Primeira(string texto) => texto.Split('\n')[0].Trim();
}
