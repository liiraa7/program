using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace PadronizadorTI.Sistema;

public static class Administrador
{
    public static bool EstaElevado()
    {
        using var identidade = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identidade).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Reabre o programa pedindo elevação (UAC). Retorna false se o usuário recusar.</summary>
    public static bool ReabrirComoAdministrador(string caminhoConfiguracao)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = $"--config \"{caminhoConfiguracao}\"",
            });
            return true;
        }
        catch (Win32Exception) // 1223: usuário cancelou o UAC
        {
            return false;
        }
    }
}
