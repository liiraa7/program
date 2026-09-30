using Microsoft.Win32;

namespace PadronizadorTI.Sistema;

/// <summary>Verifica indicadores padrão do Windows de reinício pendente. Nunca reinicia.</summary>
public static class DetectorReinicio
{
    public static List<string> ObterPendencias()
    {
        var motivos = new List<string>();
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

        if (ChaveExiste(hklm, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
            motivos.Add("Manutenção de componentes do Windows (CBS)");
        if (ChaveExiste(hklm, @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
            motivos.Add("Windows Update");
        try
        {
            using var sm = hklm.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            if (sm?.GetValue("PendingFileRenameOperations") is string[] { Length: > 0 })
                motivos.Add("Arquivos aguardando substituição (PendingFileRenameOperations)");
        }
        catch { /* sem permissão de leitura: ignora */ }

        return motivos;
    }

    private static bool ChaveExiste(RegistryKey raiz, string caminho)
    {
        try
        {
            using var chave = raiz.OpenSubKey(caminho);
            return chave is not null;
        }
        catch
        {
            return false;
        }
    }
}
