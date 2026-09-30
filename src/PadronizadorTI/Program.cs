namespace PadronizadorTI;

internal static class Program
{
    public const string NomeArquivoConfiguracao = "padronizacao.json";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.Run(new FormPrincipal(ObterCaminhoConfiguracao(args)));
    }

    /// <summary>
    /// Usa "--config caminho\arquivo.json" quando informado; caso contrário,
    /// procura padronizacao.json na mesma pasta do executável.
    /// </summary>
    private static string ObterCaminhoConfiguracao(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--config", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(args[i + 1]);
        }

        return Path.Combine(AppContext.BaseDirectory, NomeArquivoConfiguracao);
    }
}
