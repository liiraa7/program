using System.Runtime.InteropServices;

namespace PadronizadorTI.Sistema;

/// <summary>
/// Conexão a pastas de rede protegidas por usuário e senha.
/// A senha é digitada pelo técnico na hora, fica só na memória, não é gravada em
/// arquivo nem no log, e a conexão é desfeita ao final da execução.
/// </summary>
public static class CompartilhamentoRede
{
    /// <summary>Retorna "\\servidor\compartilhamento" de um caminho UNC, ou null se não for UNC.</summary>
    public static string? ObterRaiz(string caminho)
    {
        var expandido = Environment.ExpandEnvironmentVariables(caminho);
        if (!expandido.StartsWith(@"\\", StringComparison.Ordinal)) return null;

        var partes = expandido.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length >= 2 ? $@"\\{partes[0]}\{partes[1]}" : null;
    }

    public static bool EstaAcessivel(string raiz)
    {
        try { return Directory.Exists(raiz); }
        catch { return false; }
    }

    /// <summary>Conecta sem letra de unidade e sem guardar a credencial. Retorna null em caso de sucesso ou a mensagem de erro.</summary>
    public static string? Conectar(string raiz, string usuario, string senha)
    {
        var recurso = new NetResource { dwType = RESOURCETYPE_DISK, lpRemoteName = raiz };
        int codigo = WNetAddConnection2(ref recurso, senha, usuario, 0);
        return codigo switch
        {
            0 => null,
            86 or 1326 => "Usuário ou senha incorretos.",
            53 or 67 => "Servidor ou compartilhamento não encontrado.",
            1219 => "Já existe uma conexão com esse servidor usando outro usuário. Feche-a (net use \\\\servidor /delete) e tente de novo.",
            1909 => "A conta está bloqueada.",
            _ => $"Erro {codigo} do Windows ao conectar.",
        };
    }

    public static void Desconectar(string raiz)
    {
        try { WNetCancelConnection2(raiz, 0, true); } catch { /* já desconectado */ }
    }

    private const int RESOURCETYPE_DISK = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NetResource
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(ref NetResource recurso, string? senha, string? usuario, int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string nome, int flags, bool forcar);
}
