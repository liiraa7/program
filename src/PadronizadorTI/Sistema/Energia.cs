using System.Text.RegularExpressions;
using PadronizadorTI.Configuracao;

namespace PadronizadorTI.Sistema;

/// <summary>
/// Ajustes de energia via powercfg.exe (ferramenta nativa do Windows).
/// Valores "AC" = ligado à tomada; "DC" = bateria.
/// </summary>
public static partial class Energia
{
    /// <summary>GUID padrão do Windows para o plano "Alto desempenho".</summary>
    public const string GuidAltoDesempenho = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    /// <summary>
    /// GUID fixo usado quando o plano precisa ser recriado a partir do modelo do Windows.
    /// Por ser fixo, execuções repetidas não criam planos duplicados.
    /// </summary>
    private const string GuidCopiaAltoDesempenho = "4b8e3c0e-7f2a-4c1d-9e3b-5a6d7c8e9f01";

    private static readonly TimeSpan TempoLimite = TimeSpan.FromMinutes(1);
    private static string Powercfg => Path.Combine(Environment.SystemDirectory, "powercfg.exe");

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex RegexGuid();

    public static async Task<string> DescreverPlanoAtivoAsync()
    {
        var r = await ExecutorProcesso.ExecutarAsync(Powercfg, "/getactivescheme", TempoLimite);
        return r.Saida.Trim();
    }

    public static async Task<ResultadoAcao> AtivarAltoDesempenhoAsync(bool simular)
    {
        var ativo = await ExecutorProcesso.ExecutarAsync(Powercfg, "/getactivescheme", TempoLimite);
        var guidAtivo = RegexGuid().Match(ativo.Saida).Value;
        if (EhAltoDesempenho(guidAtivo))
            return new ResultadoAcao(true, false, $"Já ativo: {ativo.Saida.Trim()}", SemAlteracao: true);

        var lista = await ExecutorProcesso.ExecutarAsync(Powercfg, "/list", TempoLimite);
        var existentes = RegexGuid().Matches(lista.Saida).Select(m => m.Value).ToList();
        var destino = existentes.FirstOrDefault(EhAltoDesempenho);

        if (simular)
        {
            return new ResultadoAcao(true, false, destino is not null
                ? $"Seria ativado o plano {destino}. Plano atual: {ativo.Saida.Trim()}"
                : $"O plano Alto desempenho não aparece na lista; seria recriado a partir do modelo do Windows " +
                  $"(powercfg /duplicatescheme {GuidAltoDesempenho} {GuidCopiaAltoDesempenho}) e ativado. Plano atual: {ativo.Saida.Trim()}");
        }

        if (destino is null)
        {
            var dup = await ExecutorProcesso.ExecutarAsync(
                Powercfg, $"/duplicatescheme {GuidAltoDesempenho} {GuidCopiaAltoDesempenho}", TempoLimite);
            if (dup.CodigoSaida != 0)
                return new ResultadoAcao(false, false,
                    "Este computador não oferece o plano Alto desempenho (comum em equipamentos com Modern Standby). " +
                    $"Os tempos de tela/suspensão serão aplicados no plano atual. Detalhe: {dup.Resumo()}");
            destino = GuidCopiaAltoDesempenho;
        }

        var set = await ExecutorProcesso.ExecutarAsync(Powercfg, $"/setactive {destino}", TempoLimite);
        return set.CodigoSaida == 0
            ? new ResultadoAcao(true, false, $"Plano Alto desempenho ativado ({destino}).")
            : new ResultadoAcao(false, false, $"powercfg /setactive retornou {set.CodigoSaida}: {set.Resumo()}");
    }

    /// <summary>Monta os argumentos do powercfg /change para tomada (AC) ou bateria (DC).</summary>
    public static List<string> ComandosTempos(TemposEnergia tempos, bool tomada)
    {
        var sufixo = tomada ? "ac" : "dc";
        var comandos = new List<string>();
        if (tempos.DesligarTelaMinutos is int tela) comandos.Add($"/change monitor-timeout-{sufixo} {tela}");
        if (tempos.SuspenderMinutos is int susp) comandos.Add($"/change standby-timeout-{sufixo} {susp}");
        if (tempos.HibernarMinutos is int hib) comandos.Add($"/change hibernate-timeout-{sufixo} {hib}");
        return comandos;
    }

    public static string DescreverTempos(TemposEnergia t)
    {
        static string F(int? v) => v switch { null => "não alterar", 0 => "Nunca", _ => $"{v} min" };
        return $"Desligar tela: {F(t.DesligarTelaMinutos)}; Suspender: {F(t.SuspenderMinutos)}; Hibernar: {F(t.HibernarMinutos)}";
    }

    public static async Task<ResultadoAcao> AplicarTemposAsync(TemposEnergia tempos, bool tomada, bool simular)
    {
        var comandos = ComandosTempos(tempos, tomada);
        if (comandos.Count == 0)
            return new ResultadoAcao(true, false, "Nenhum tempo configurado para alterar.", SemAlteracao: true);
        if (simular)
            return new ResultadoAcao(true, false, "Seriam executados: " + string.Join(" | ", comandos.Select(c => "powercfg " + c)));

        var falhas = new List<string>();
        foreach (var cmd in comandos)
        {
            var r = await ExecutorProcesso.ExecutarAsync(Powercfg, cmd, TempoLimite);
            if (r.CodigoSaida != 0)
                falhas.Add($"powercfg {cmd} → código {r.CodigoSaida} {r.Resumo(3)}");
        }

        return falhas.Count == 0
            ? new ResultadoAcao(true, false, $"Aplicado no plano ativo: {DescreverTempos(tempos)}")
            : new ResultadoAcao(false, false, "Alguns ajustes falharam: " + string.Join(" ; ", falhas));
    }

    private static bool EhAltoDesempenho(string guid) =>
        guid.Equals(GuidAltoDesempenho, StringComparison.OrdinalIgnoreCase) ||
        guid.Equals(GuidCopiaAltoDesempenho, StringComparison.OrdinalIgnoreCase);
}
