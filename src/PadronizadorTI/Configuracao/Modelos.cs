namespace PadronizadorTI.Configuracao;

/// <summary>Forma de instalação de um aplicativo.</summary>
public enum TipoInstalador
{
    /// <summary>Pacote do WinGet (fonte oficial "winget" da Microsoft).</summary>
    WinGet,
    /// <summary>Instalador .exe local ou em pasta de rede.</summary>
    Exe,
    /// <summary>Pacote .msi local ou em pasta de rede (executado com msiexec /qn /norestart).</summary>
    Msi,
}

/// <summary>Como descobrir se o aplicativo já está instalado.</summary>
public enum TipoDeteccao
{
    /// <summary>Somente para WinGet: usa "winget list --id ... --exact".</summary>
    Automatica,
    /// <summary>Procura o nome em "Programas e Recursos" (chaves Uninstall do registro).</summary>
    ProgramaInstalado,
    /// <summary>Procura o ProductCode {GUID} de um MSI nas chaves Uninstall.</summary>
    CodigoProdutoMsi,
    /// <summary>Verifica se um arquivo existe (aceita variáveis como %ProgramFiles%).</summary>
    Arquivo,
    /// <summary>Verifica se uma chave (e opcionalmente um valor) do registro existe.</summary>
    ChaveRegistro,
}

public enum Comparacao
{
    Contem,
    IniciaCom,
    Igual,
}

/// <summary>Raiz do arquivo padronizacao.json.</summary>
public sealed class ConfiguracaoPadronizacao
{
    public string PastaLogs { get; set; } = @"%ProgramData%\PadronizadorTI\Logs";

    /// <summary>Pasta de rede opcional para onde uma cópia do log é enviada ao final.</summary>
    public string? PastaLogsRede { get; set; }

    /// <summary>Fontes do WinGet aceitas. Qualquer outra fonte é recusada.</summary>
    public List<string> FontesWinGetPermitidas { get; set; } = ["winget"];

    public ConfiguracaoEnergia Energia { get; set; } = new();

    public List<Aplicativo> AplicativosComuns { get; set; } = [];

    public List<Departamento> Departamentos { get; set; } = [];
}

public sealed class Departamento
{
    public string Nome { get; set; } = "";
    public List<Aplicativo> Aplicativos { get; set; } = [];

    public override string ToString() => Nome;
}

public sealed class Aplicativo
{
    public string Nome { get; set; } = "";
    public TipoInstalador Tipo { get; set; } = TipoInstalador.WinGet;

    /// <summary>Itens pendentes aparecem no resumo, mas nunca são executados.</summary>
    public bool Pendente { get; set; }
    public string? MotivoPendencia { get; set; }
    public string? Observacao { get; set; }

    // ---- WinGet ----
    public string? IdWinGet { get; set; }
    /// <summary>Versão fixa (opcional). Vazio = versão mais recente da fonte.</summary>
    public string? Versao { get; set; }
    public string Fonte { get; set; } = "winget";
    /// <summary>"machine", "user" ou vazio (padrão do pacote).</summary>
    public string? Escopo { get; set; }

    // ---- Exe / Msi ----
    /// <summary>Caminho local (C:\...) ou UNC (\\servidor\compartilhamento\...). URLs não são aceitas.</summary>
    public string? Caminho { get; set; }
    /// <summary>Argumentos de instalação silenciosa. Para MSI, são acrescentados após /qn /norestart.</summary>
    public string? Argumentos { get; set; }
    /// <summary>Hash SHA-256 esperado do instalador (opcional, recomendado).</summary>
    public string? Sha256 { get; set; }
    /// <summary>Copia o instalador para a pasta temporária antes de executar (só para instaladores de arquivo único).</summary>
    public bool CopiarParaTemp { get; set; }
    public List<int> CodigosSucesso { get; set; } = [0];
    public List<int> CodigosReinicio { get; set; } = [3010, 1641];
    public int TempoLimiteMinutos { get; set; } = 30;

    public Deteccao? Deteccao { get; set; }

    public override string ToString() => Nome;
}

public sealed class Deteccao
{
    public TipoDeteccao Tipo { get; set; } = TipoDeteccao.Automatica;

    /// <summary>ProgramaInstalado: nome exibido em "Programas e Recursos".</summary>
    public string? NomeExibicao { get; set; }
    public Comparacao Comparacao { get; set; } = Comparacao.Contem;

    /// <summary>CodigoProdutoMsi: {GUID} do produto.</summary>
    public string? CodigoProduto { get; set; }

    /// <summary>Arquivo: caminho do arquivo. ChaveRegistro: ex. HKLM\SOFTWARE\Empresa\Produto.</summary>
    public string? Caminho { get; set; }

    /// <summary>ChaveRegistro: nome do valor que deve existir (opcional).</summary>
    public string? NomeValor { get; set; }
}

public sealed class ConfiguracaoEnergia
{
    public bool Aplicar { get; set; } = true;
    public bool AtivarAltoDesempenho { get; set; } = true;

    /// <summary>Tempos quando ligado à tomada (AC). 0 = nunca; null = não alterar.</summary>
    public TemposEnergia Tomada { get; set; } = new() { DesligarTelaMinutos = 0, SuspenderMinutos = 0, HibernarMinutos = 0 };

    /// <summary>Tempos na bateria (DC). Só são aplicados se "aplicar" for true.</summary>
    public TemposEnergiaBateria Bateria { get; set; } = new();
}

public class TemposEnergia
{
    public int? DesligarTelaMinutos { get; set; }
    public int? SuspenderMinutos { get; set; }
    public int? HibernarMinutos { get; set; }
}

public sealed class TemposEnergiaBateria : TemposEnergia
{
    /// <summary>Padrão false: as configurações de bateria NÃO são alteradas automaticamente.</summary>
    public bool Aplicar { get; set; }
}
