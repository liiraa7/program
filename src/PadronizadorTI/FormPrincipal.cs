using System.Diagnostics;
using PadronizadorTI.Configuracao;
using PadronizadorTI.Execucao;
using PadronizadorTI.Sistema;

namespace PadronizadorTI;

public sealed class FormPrincipal : Form
{
    private readonly ComboBox _cboDepartamento = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly CheckBox _chkSimulacao = new() { Text = "Modo simulação (não altera nada no computador)", Checked = true, AutoSize = true };
    private readonly Label _lblConfig = new() { AutoSize = true, ForeColor = SystemColors.GrayText };
    private readonly Label _lblAdmin = new() { AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
    private readonly Label _lblResumo = new() { AutoSize = true };
    private readonly Label _lblProgresso = new() { AutoSize = true, Text = "Pronto." };
    private readonly ProgressBar _barra = new() { Dock = DockStyle.Fill, Height = 20 };
    private readonly ListView _lista = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
    private readonly TextBox _txtLog = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9) };
    private readonly Button _btnExecutar = new() { Text = "Iniciar simulação", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
    private readonly Button _btnCancelar = new() { Text = "Cancelar", AutoSize = true, Enabled = false };
    private readonly Button _btnRecarregar = new() { Text = "Recarregar configuração", AutoSize = true };
    private readonly Button _btnAbrirConfig = new() { Text = "Abrir outra configuração...", AutoSize = true };
    private readonly Button _btnLogs = new() { Text = "Abrir pasta de logs", AutoSize = true };
    private readonly Button _btnElevar = new() { Text = "Reabrir como administrador", AutoSize = true };

    private string _caminhoConfig;
    private ConfiguracaoPadronizacao? _config;
    private List<Etapa> _etapas = [];
    private CancellationTokenSource? _cancelamento;
    private string? _pastaUltimoLog;

    public FormPrincipal(string caminhoConfig)
    {
        _caminhoConfig = caminhoConfig;

        Text = $"Padronizador de Estações - TI  |  {Environment.MachineName}";
        Font = new Font("Segoe UI", 9);
        MinimumSize = new Size(900, 600);
        Size = new Size(1150, 760);
        StartPosition = FormStartPosition.CenterScreen;

        MontarLayout();

        _cboDepartamento.SelectedIndexChanged += (_, _) => AtualizarPlano();
        _chkSimulacao.CheckedChanged += (_, _) => AtualizarBotaoExecutar();
        _btnExecutar.Click += async (_, _) => await ExecutarAsync();
        _btnCancelar.Click += (_, _) => Cancelar();
        _btnRecarregar.Click += (_, _) => CarregarConfiguracao();
        _btnAbrirConfig.Click += (_, _) => EscolherConfiguracao();
        _btnLogs.Click += (_, _) => AbrirPastaLogs();
        _btnElevar.Click += (_, _) => Elevar();
        FormClosing += AoFechar;

        AtualizarStatusAdministrador();
        CarregarConfiguracao();
    }

    // ------------------------------------------------------------------ Layout

    private void MontarLayout()
    {
        _lista.Columns.Add("#", 40);
        _lista.Columns.Add("Etapa", 260);
        _lista.Columns.Add("Origem", 130);
        _lista.Columns.Add("Status", 170);
        _lista.Columns.Add("Ação / Detalhes", 900);

        var topo = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 4, 0, 4) };
        topo.Controls.Add(new Label { Text = "Departamento:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        topo.Controls.Add(_cboDepartamento);
        topo.Controls.Add(_btnRecarregar);
        topo.Controls.Add(_btnAbrirConfig);
        topo.Controls.Add(_btnLogs);

        var linhaAdmin = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _lblAdmin.Margin = new Padding(3, 7, 3, 3);
        linhaAdmin.Controls.Add(_lblAdmin);
        linhaAdmin.Controls.Add(_btnElevar);
        linhaAdmin.Controls.Add(_lblConfig);
        _lblConfig.Margin = new Padding(12, 7, 3, 3);

        var divisor = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        divisor.Panel1.Controls.Add(_lista);
        divisor.Panel2.Controls.Add(_txtLog);
        Load += (_, _) => divisor.SplitterDistance = (int)(divisor.Height * 0.6);

        var rodape = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _chkSimulacao.Margin = new Padding(3, 7, 20, 3);
        rodape.Controls.Add(_chkSimulacao);
        rodape.Controls.Add(_btnExecutar);
        rodape.Controls.Add(_btnCancelar);

        var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.Controls.Add(topo);
        raiz.Controls.Add(linhaAdmin);
        raiz.Controls.Add(_lblResumo);
        raiz.Controls.Add(divisor);
        raiz.Controls.Add(_lblProgresso);
        raiz.Controls.Add(_barra);
        raiz.Controls.Add(rodape);
        Controls.Add(raiz);
    }

    // ---------------------------------------------------------- Configuração

    private void CarregarConfiguracao()
    {
        var selecionado = (_cboDepartamento.SelectedItem as Departamento)?.Nome;
        _cboDepartamento.Items.Clear();
        _lista.Items.Clear();
        _etapas = [];
        _config = null;
        _lblConfig.Text = $"Configuração: {_caminhoConfig}";

        try
        {
            _config = CarregadorConfiguracao.Carregar(_caminhoConfig, out var avisos);
            foreach (var d in _config.Departamentos)
                _cboDepartamento.Items.Add(d);

            EscreverTela($"Configuração carregada: {_caminhoConfig}");
            foreach (var aviso in avisos)
                EscreverTela("  Aviso (item tratado como pendente): " + aviso);

            var anterior = _config.Departamentos.FindIndex(d => d.Nome == selecionado);
            if (anterior >= 0) _cboDepartamento.SelectedIndex = anterior;
            _lblResumo.Text = _cboDepartamento.SelectedIndex < 0 ? "Selecione o departamento para ver o resumo das ações." : _lblResumo.Text;
        }
        catch (ErroConfiguracaoException ex)
        {
            EscreverTela("ERRO: " + ex.Message);
            _lblResumo.Text = "Corrija o arquivo de configuração e clique em \"Recarregar configuração\".";
            MessageBox.Show(this, ex.Message, "Configuração inválida", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        AtualizarBotaoExecutar();
    }

    private void EscolherConfiguracao()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Selecione o arquivo de configuração",
            Filter = "Configuração (*.json)|*.json",
            InitialDirectory = Path.GetDirectoryName(_caminhoConfig),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _caminhoConfig = dlg.FileName;
        CarregarConfiguracao();
    }

    // ------------------------------------------------------------------ Plano

    private void AtualizarPlano()
    {
        if (_config is null || _cboDepartamento.SelectedItem is not Departamento depto) return;

        _etapas = Planejador.Montar(_config, depto);
        _lista.BeginUpdate();
        _lista.Items.Clear();
        foreach (var etapa in _etapas)
        {
            var item = new ListViewItem([etapa.Numero.ToString(), etapa.Nome, etapa.Origem, "", ""]) { Tag = etapa };
            _lista.Items.Add(item);
            AtualizarLinha(etapa);
        }
        _lista.EndUpdate();

        var apps = _etapas.Where(e => e.Tipo == TipoEtapa.Aplicativo).ToList();
        int pendentes = apps.Count(e => e.Status == StatusEtapa.Pendente);
        int configs = _etapas.Count(e => e.Tipo != TipoEtapa.Aplicativo && e.Status == StatusEtapa.Aguardando);
        _lblResumo.Text =
            $"Resumo para {depto.Nome}: {configs} configuração(ões) de energia, " +
            $"{apps.Count - pendentes} aplicativo(s) a verificar/instalar, {pendentes} pendente(s) de configuração. " +
            "Aplicativos já instalados serão mantidos.";
        _barra.Value = 0;
        _lblProgresso.Text = "Pronto.";
        AtualizarBotaoExecutar();
    }

    private void AtualizarLinha(Etapa etapa)
    {
        var item = _lista.Items.Cast<ListViewItem>().FirstOrDefault(i => i.Tag == etapa);
        if (item is null) return;

        item.SubItems[3].Text = Etapa.Texto(etapa.Status);
        item.SubItems[4].Text = string.IsNullOrEmpty(etapa.Detalhes) ? etapa.Acao : etapa.Detalhes.Replace(Environment.NewLine, " ");
        item.ForeColor = etapa.Status switch
        {
            StatusEtapa.Falha => Color.Firebrick,
            StatusEtapa.Pendente => Color.DarkOrange,
            StatusEtapa.Instalado or StatusEtapa.ConfiguracaoAplicada => Color.ForestGreen,
            StatusEtapa.JaExistente => Color.SteelBlue,
            StatusEtapa.NaoAlterado or StatusEtapa.Cancelada => SystemColors.GrayText,
            _ => SystemColors.WindowText,
        };
        if (etapa.Status == StatusEtapa.Executando) item.EnsureVisible();
    }

    // --------------------------------------------------------------- Execução

    private async Task ExecutarAsync()
    {
        if (_config is null || _cboDepartamento.SelectedItem is not Departamento depto)
        {
            MessageBox.Show(this, "Selecione o departamento.", "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool simulacao = _chkSimulacao.Checked;
        if (!simulacao)
        {
            if (!Administrador.EstaElevado())
            {
                MessageBox.Show(this,
                    "A execução real precisa de permissão de administrador.\n\nClique em \"Reabrir como administrador\". " +
                    "A simulação pode ser feita sem essa permissão.",
                    "Administrador necessário", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmar = MessageBox.Show(this,
                $"Confirma a padronização deste computador ({Environment.MachineName}) para o departamento {depto.Nome}?\n\n" +
                $"{_lblResumo.Text}\n\n" +
                "Itens pendentes não serão executados. O computador NÃO será reiniciado automaticamente.",
                "Confirmar execução", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (confirmar != DialogResult.Yes) return;
        }

        AtualizarPlano(); // recomeça com todas as etapas zeradas
        _txtLog.Clear();
        var pastasConectadas = ConectarPastasDeRede();
        DefinirEmExecucao(true);
        _cancelamento = new CancellationTokenSource();

        int concluidas = 0;
        _barra.Maximum = Math.Max(1, _etapas.Count);
        _barra.Value = 0;

        var progresso = new Progress<ProgressoEtapa>(p =>
        {
            var etapa = p.Etapa;
            AtualizarLinha(etapa);
            if (p.Status == StatusEtapa.Executando)
            {
                _lblProgresso.Text = $"Etapa {concluidas + 1} de {_etapas.Count}: {etapa.Nome}...";
                return;
            }
            concluidas++;
            _barra.Value = Math.Min(concluidas, _barra.Maximum);
            _lblProgresso.Text = $"{concluidas} de {_etapas.Count} etapas concluídas.";
        });

        ResumoExecucao? resumo = null;
        try
        {
            using var log = new RegistroExecucao(_config.PastaLogs, depto.Nome, simulacao);
            _pastaUltimoLog = log.Pasta;
            log.LinhaEscrita += linha => BeginInvoke(() => EscreverTela(linha, comHora: false));

            var executor = new Executor(_config, log, simulacao);
            resumo = await Task.Run(() => executor.ExecutarAsync(_etapas, depto, _caminhoConfig, progresso, _cancelamento.Token));
        }
        catch (Exception ex)
        {
            EscreverTela("ERRO GERAL: " + ex);
            MessageBox.Show(this, ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            foreach (var raiz in pastasConectadas)
                CompartilhamentoRede.Desconectar(raiz);
            DefinirEmExecucao(false);
            _cancelamento?.Dispose();
            _cancelamento = null;
        }

        if (resumo is null) return;
        _lblProgresso.Text = resumo.Simulacao ? "Simulação concluída." : "Execução concluída.";
        var icone = resumo.Falhas > 0 ? MessageBoxIcon.Warning
            : resumo.MotivosReinicio.Count > 0 ? MessageBoxIcon.Exclamation
            : MessageBoxIcon.Information;
        MessageBox.Show(this, resumo.Texto(), "Resumo", MessageBoxButtons.OK, icone);
    }

    /// <summary>
    /// Para cada pasta de rede usada pelos instaladores e que não esteja acessível,
    /// pede usuário e senha ao técnico. Retorna as pastas conectadas, para desconectar no final.
    /// </summary>
    private List<string> ConectarPastasDeRede()
    {
        var conectadas = new List<string>();
        var raizes = _etapas
            .Where(e => e.Aplicativo is { Pendente: false, Tipo: not TipoInstalador.WinGet })
            .Select(e => CompartilhamentoRede.ObterRaiz(e.Aplicativo!.Caminho!))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var raiz in raizes)
        {
            if (CompartilhamentoRede.EstaAcessivel(raiz)) continue;

            string? erro = null;
            while (true)
            {
                using var dlg = new DialogoCredencial(raiz, erro);
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    EscreverTela($"Pasta de rede {raiz} sem acesso (o técnico optou por pular).");
                    break;
                }
                erro = CompartilhamentoRede.Conectar(raiz, dlg.Usuario, dlg.Senha);
                if (erro is null)
                {
                    EscreverTela($"Conectado à pasta de rede {raiz} como {dlg.Usuario}.");
                    conectadas.Add(raiz);
                    break;
                }
            }
        }
        return conectadas;
    }

    private void Cancelar()
    {
        _cancelamento?.Cancel();
        _btnCancelar.Enabled = false;
        _lblProgresso.Text = "Cancelando... a etapa atual será concluída antes de parar.";
    }

    private void DefinirEmExecucao(bool emExecucao)
    {
        _cboDepartamento.Enabled = !emExecucao;
        _chkSimulacao.Enabled = !emExecucao;
        _btnExecutar.Enabled = !emExecucao;
        _btnRecarregar.Enabled = !emExecucao;
        _btnAbrirConfig.Enabled = !emExecucao;
        _btnElevar.Enabled = !emExecucao && !Administrador.EstaElevado();
        _btnCancelar.Enabled = emExecucao;
        UseWaitCursor = emExecucao;
        if (!emExecucao) AtualizarBotaoExecutar();
    }

    private void AtualizarBotaoExecutar()
    {
        _btnExecutar.Text = _chkSimulacao.Checked ? "Iniciar simulação" : "Executar padronização";
        _btnExecutar.Enabled = _config is not null && _cboDepartamento.SelectedItem is Departamento;
    }

    // ------------------------------------------------------------ Utilidades

    private void AtualizarStatusAdministrador()
    {
        bool admin = Administrador.EstaElevado();
        _lblAdmin.Text = admin ? "✔ Executando como administrador" : "✖ Sem permissão de administrador (somente simulação)";
        _lblAdmin.ForeColor = admin ? Color.ForestGreen : Color.Firebrick;
        _btnElevar.Visible = !admin;
    }

    private void Elevar()
    {
        if (Administrador.ReabrirComoAdministrador(_caminhoConfig))
            Close();
        else
            MessageBox.Show(this, "A elevação foi cancelada.", "Administrador", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void AbrirPastaLogs()
    {
        var pasta = _pastaUltimoLog ?? Environment.ExpandEnvironmentVariables(_config?.PastaLogs ?? @"%ProgramData%\PadronizadorTI\Logs");
        if (!Directory.Exists(pasta))
        {
            MessageBox.Show(this, $"A pasta ainda não existe:\n{pasta}\n\nEla é criada na primeira execução.", "Logs");
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{pasta}\"") { UseShellExecute = true });
    }

    private void EscreverTela(string texto, bool comHora = true)
    {
        _txtLog.AppendText((comHora ? $"[{DateTime.Now:HH:mm:ss}] " : "") + texto.Replace("\n", Environment.NewLine).Replace("\r\r", "\r") + Environment.NewLine);
    }

    private void AoFechar(object? sender, FormClosingEventArgs e)
    {
        if (_cancelamento is null) return;
        MessageBox.Show(this, "Aguarde o término da execução (ou clique em Cancelar e aguarde a etapa atual).",
            "Execução em andamento", MessageBoxButtons.OK, MessageBoxIcon.Information);
        e.Cancel = true;
    }
}
