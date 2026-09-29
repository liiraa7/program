namespace PadronizadorTI;

/// <summary>Pede usuário e senha para uma pasta de rede. Nada é gravado.</summary>
public sealed class DialogoCredencial : Form
{
    private readonly TextBox _txtUsuario = new() { Width = 280 };
    private readonly TextBox _txtSenha = new() { Width = 280, UseSystemPasswordChar = true };

    public string Usuario => _txtUsuario.Text.Trim();
    public string Senha => _txtSenha.Text;

    public DialogoCredencial(string raiz, string? erroAnterior)
    {
        Text = "Acesso à pasta de rede";
        Font = new Font("Segoe UI", 9);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var btnOk = new Button { Text = "Conectar", DialogResult = DialogResult.OK, AutoSize = true };
        var btnPular = new Button { Text = "Pular esta pasta", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = btnOk;
        CancelButton = btnPular;

        var painel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(12) };
        painel.Controls.Add(new Label
        {
            Text = $"A pasta {raiz} pede usuário e senha.\n" +
                   "Exemplo de usuário: SRV-DOMINIO\\usuario ou DOMINIO\\usuario.\n" +
                   "A senha não é gravada e a conexão é desfeita ao final.",
            AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(3, 3, 3, 10),
        }, 0, 0);
        painel.SetColumnSpan(painel.GetControlFromPosition(0, 0)!, 2);

        int linha = 1;
        if (!string.IsNullOrEmpty(erroAnterior))
        {
            var erro = new Label { Text = erroAnterior, ForeColor = Color.Firebrick, AutoSize = true, MaximumSize = new Size(420, 0) };
            painel.Controls.Add(erro, 0, linha);
            painel.SetColumnSpan(erro, 2);
            linha++;
        }

        painel.Controls.Add(new Label { Text = "Usuário:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, linha);
        painel.Controls.Add(_txtUsuario, 1, linha++);
        painel.Controls.Add(new Label { Text = "Senha:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, linha);
        painel.Controls.Add(_txtSenha, 1, linha++);

        var botoes = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        botoes.Controls.Add(btnPular);
        botoes.Controls.Add(btnOk);
        painel.Controls.Add(botoes, 0, linha);
        painel.SetColumnSpan(botoes, 2);

        Controls.Add(painel);
    }
}
