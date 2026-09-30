# Padronizador de Estações – TI

Programa para Windows que **padroniza computadores depois da formatação**. O técnico escolhe o
departamento (**Legal, Fiscal, Contábil ou Departamento Pessoal**), confere o resumo das ações, faz uma
**simulação** e, depois de confirmar, o programa:

1. aplica as configurações comuns de energia (plano **Alto desempenho**, e **tela e suspensão em "Nunca"
   quando ligado à tomada**). As configurações de bateria ficam separadas e **não** mudam automaticamente;
2. instala os **aplicativos comuns** (ex.: WinRAR) e os **aplicativos do departamento**, pelo **WinGet**
   ou por instaladores **`.exe`/`.msi`** locais ou em uma pasta de rede;
3. **não reinstala** o que já existe, registra erros e segue para o próximo item;
4. mostra o progresso e um resumo final (**instalados, já existentes, pendentes e com falha**);
5. grava **um log por execução**, com nome do computador, departamento, data e resultados;
6. avisa quando é preciso reiniciar, **sem reiniciar sozinho**.

---

## Por que C#/.NET (e não Java ou só PowerShell)

| Critério | Decisão |
|---|---|
| **Plataforma** | O alvo é só Windows. C# com **WinForms** é nativo e acessa diretamente o registro, processos, UAC e `powercfg`. |
| **Máquina recém-formatada** | O executável é publicado **autocontido**: um único `PadronizadorTI.exe`, sem precisar instalar .NET. Com Java seria preciso instalar um runtime antes, e o Java é justamente um dos itens que ainda estão pendentes. |
| **Manutenção pelo TI** | O dia a dia é editar **um arquivo JSON**, sem recompilar. O código é pequeno, está dividido por responsabilidade e comentado em português. |
| **PowerShell** | Não foi necessário na lógica do programa: WinGet, `msiexec` e `powercfg` são chamados diretamente. PowerShell aparece só como apoio no script de publicação (`scripts/publicar.ps1`). |
| **Versão** | **.NET 10 (LTS)**, com suporte até novembro de 2028. O .NET 8 perde suporte em novembro de 2026. |

---

## Estrutura do repositório

```
├── PadronizadorTI.sln
├── config/
│   └── padronizacao.exemplo.json      ← modelo de configuração (valores fictícios)
├── scripts/
│   └── publicar.ps1                   ← gera a versão distribuível (.exe + .zip)
├── src/PadronizadorTI/
│   ├── Program.cs                     ← ponto de entrada (aceita --config)
│   ├── FormPrincipal.cs               ← interface gráfica
│   ├── Configuracao/                  ← modelos e validação do JSON
│   ├── Execucao/                      ← plano de etapas, executor, resumo e log
│   ├── Sistema/                       ← WinGet, MSI/EXE, energia, detecção, admin, reinício
│   └── Properties/PublishProfiles/win-x64.pubxml
├── .github/workflows/build.yml        ← compila e publica no GitHub Actions
└── .gitignore
```

> O arquivo **real** `config/padronizacao.json` (com servidores e caminhos internos) está no
> `.gitignore` e **não vai para o GitHub**. Só o arquivo de exemplo é versionado.

---

## Requisitos

- **Para usar:** Windows 10 (1809+) ou Windows 11, 64 bits. Para itens do WinGet, o **Instalador de
  Aplicativo (App Installer)** precisa estar atualizado (ele já vem no Windows 11 e nas versões recentes do Windows 10).
- **Para compilar:** [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0), ou Visual Studio 2026 / 2022 17.14+
  com a carga de trabalho ".NET desktop".

---

## Compilar e executar (desenvolvimento)

```powershell
git clone https://github.com/<sua-conta>/<seu-repositorio>.git
cd <seu-repositorio>

# (opcional) crie a sua configuração real a partir do exemplo
copy config\padronizacao.exemplo.json config\padronizacao.json

dotnet build -c Release
dotnet run --project src\PadronizadorTI
```

Se `config\padronizacao.json` existir, é ele que vai para a pasta de saída. Se não existir, vai o exemplo.

Também é possível abrir o `PadronizadorTI.sln` no Visual Studio e pressionar **F5**.

### Executar com uma configuração em outro local

```powershell
PadronizadorTI.exe --config "\\SERVIDOR-EXEMPLO\TI$\PadronizadorTI\padronizacao.json"
```

Assim uma única configuração na rede serve para todas as máquinas. Na tela também existe o botão
**"Abrir outra configuração..."**.

---

## Gerar a versão distribuível

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publicar.ps1 -Versao 1.0.0
```

O script gera:

- `dist\PadronizadorTI\PadronizadorTI.exe`: executável único e autocontido (~50 MB), não depende do .NET instalado;
- `dist\PadronizadorTI\padronizacao.json`: configuração (a real, se existir; senão, o exemplo);
- `dist\PadronizadorTI-1.0.0.zip`: pacote para copiar para um pendrive ou para a pasta de rede do TI.

Sem o script, o comando equivalente é:

```powershell
dotnet publish src\PadronizadorTI -p:PublishProfile=win-x64
```

O GitHub Actions (`.github/workflows/build.yml`) faz o mesmo a cada *push* e deixa o executável
(com a configuração de exemplo) disponível como *artifact* da execução.

> **Dica:** o Windows SmartScreen pode avisar sobre um executável não assinado. Se a empresa tiver um
> certificado de assinatura de código, assine o `.exe` antes de distribuir.

---

## O arquivo de configuração

O arquivo `padronizacao.json` fica ao lado do executável. Ele aceita comentários (`// ...`).

**Regras importantes do JSON**

- Em caminhos, escreva a barra invertida **dobrada**: `C:\\Pasta\\app.msi`. O caminho de rede
  `\\SERVIDOR\Pasta` fica `"\\\\SERVIDOR\\Pasta"`.
- Variáveis do Windows são aceitas: `%ProgramFiles%`, `%ProgramFiles(x86)%`, `%ProgramData%`.
- Depois de editar, clique em **"Recarregar configuração"**. Se houver erro de sintaxe, o programa mostra a linha.

### Estrutura geral

```jsonc
{
  "pastaLogs": "%ProgramData%\\PadronizadorTI\\Logs",
  "pastaLogsRede": "",                       // opcional: cópia dos logs na rede
  "fontesWinGetPermitidas": [ "winget" ],    // só a fonte oficial da Microsoft
  "energia": { ... },
  "aplicativosComuns": [ ... ],              // todos os departamentos
  "departamentos": [
    { "nome": "Legal",                "aplicativos": [ ... ] },
    { "nome": "Fiscal",               "aplicativos": [ ... ] },
    { "nome": "Contábil",             "aplicativos": [ ... ] },
    { "nome": "Departamento Pessoal", "aplicativos": [ ... ] }
  ]
}
```

A ordem de execução é: energia, depois aplicativos comuns e, por último, aplicativos do departamento,
na ordem em que aparecem no arquivo.

### Energia

```jsonc
"energia": {
  "aplicar": true,
  "ativarAltoDesempenho": true,
  "tomada":  { "desligarTelaMinutos": 0, "suspenderMinutos": 0, "hibernarMinutos": 0 },  // 0 = Nunca
  "bateria": { "aplicar": false, "desligarTelaMinutos": 5, "suspenderMinutos": 15, "hibernarMinutos": 60 }
}
```

- Os valores de **tomada** usam `powercfg /change monitor-timeout-ac`, `standby-timeout-ac` e `hibernate-timeout-ac`.
- A **bateria** fica separada e com `"aplicar": false`. Assim ela aparece no resumo como "Não alterado".
  Só mude para `true` se decidir padronizar a bateria também.
- Use `null` em um tempo para não alterar aquele item.
- Alguns notebooks com *Modern Standby* não oferecem o plano Alto desempenho. Nesse caso a etapa é
  registrada como falha e os tempos de tomada são aplicados no plano atual.

### Como cadastrar um programa

Cada programa é um objeto na lista `aplicativosComuns` ou na lista `aplicativos` de um departamento.

#### a) Pelo WinGet (recomendado quando o programa existe no repositório oficial)

1. Em uma máquina de teste, descubra o ID exato:
   ```powershell
   winget search "nome do programa" --source winget
   winget show --id Fornecedor.Programa --source winget     # confira fornecedor e site
   ```
2. Cadastre:
   ```jsonc
   {
     "nome": "Nome amigável",
     "tipo": "WinGet",
     "idWinGet": "Fornecedor.Programa",
     "versao": "",            // vazio = mais recente; ou fixe, ex. "7.01"
     "escopo": "",            // opcional: "machine" (todos os usuários) ou "user"
     "deteccao": { "tipo": "ProgramaInstalado", "nomeExibicao": "Nome em Programas e Recursos" }
   }
   ```
   A `deteccao` é opcional no WinGet. Sem ela, o programa consulta `winget list --id ... --exact`.

O programa executa:
`winget install --id <ID> --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity`.

#### b) Instalador `.msi` interno

```jsonc
{
  "nome": "Sistema X",
  "tipo": "Msi",
  "caminho": "\\\\SERVIDOR-EXEMPLO\\Instaladores$\\SistemaX\\sistemax.msi",
  "argumentos": "ALLUSERS=1",            // /qn /norestart já são incluídos automaticamente
  "sha256": "",                          // opcional (recomendado)
  "deteccao": { "tipo": "CodigoProdutoMsi", "codigoProduto": "{GUID-DO-PRODUTO}" }
}
```

O comando fica: `msiexec /i "<caminho>" /qn /norestart /L*v "<pasta de logs>\msi_....log" <argumentos>`.
O log detalhado do MSI fica junto com os logs do programa.

#### c) Instalador `.exe` interno

```jsonc
{
  "nome": "Sistema Y",
  "tipo": "Exe",
  "caminho": "\\\\SERVIDOR-EXEMPLO\\Instaladores$\\SistemaY\\setup.exe",
  "argumentos": "/S",                    // silencioso: depende do fabricante
  "codigosSucesso":  [ 0 ],
  "codigosReinicio": [ 3010 ],
  "tempoLimiteMinutos": 30,
  "copiarParaTemp": false,               // true só se o instalador for um arquivo único
  "sha256": "",
  "deteccao": { "tipo": "Arquivo", "caminho": "%ProgramFiles%\\SistemaY\\sistemay.exe" }
}
```

Argumentos silenciosos comuns (**confirme com o fabricante**): NSIS `/S`, Inno Setup `/VERYSILENT /NORESTART`,
InstallShield `/s /v"/qn /norestart"`, WiX Burn `/quiet /norestart`. Inclua sempre a opção de **não reiniciar** quando o instalador tiver uma.

#### Pasta de rede protegida por usuário e senha

Se os instaladores estiverem numa pasta que pede login (ex.: `\\SERVIDOR\Instaladores`), **não coloque
usuário nem senha no JSON**. Ao iniciar a simulação ou a execução, o Padronizador detecta a pasta inacessível e
abre uma janela pedindo usuário e senha. A senha fica só na memória, não vai para o log, e a conexão é desfeita
ao final. Se o técnico clicar em **"Pular esta pasta"**, os itens dessa pasta ficam como falha "sem acesso".

> **Use sempre o caminho de rede (UNC), nunca a letra da unidade mapeada** (ex.: `I:\...`). Quando o
> programa roda como administrador, o Windows não enxerga as unidades mapeadas do usuário. Para descobrir o
> caminho real de uma letra, rode `net use I:` e veja o campo "Nome remoto".

#### Campos de todos os programas

| Campo | Uso |
|---|---|
| `nome` | Nome que aparece na tela e no log. |
| `tipo` | `WinGet`, `Msi` ou `Exe`. |
| `pendente` / `motivoPendencia` | `true` = aparece no resumo como **Pendente** e **nunca** é executado. |
| `caminho` | Caminho local ou UNC. **URLs (`http://`, `https://`) são recusadas.** |
| `argumentos` | Parâmetros de instalação silenciosa. |
| `sha256` | Hash esperado do instalador. Se não conferir, a instalação é bloqueada. Para obter: `Get-FileHash .\arquivo.msi -Algorithm SHA256`. |
| `codigosSucesso` / `codigosReinicio` | Códigos de saída tratados como sucesso ou como "reinício necessário". Padrão: `[0]` e `[3010, 1641]`. |
| `tempoLimiteMinutos` | Se o instalador passar desse tempo, ele é encerrado e marcado como falha. Padrão: 30. |
| `deteccao` | Como saber se já está instalado. **Obrigatória para `.exe`/`.msi`.** |

#### Tipos de detecção (evitam reinstalações)

| `tipo` | Campos | Quando usar |
|---|---|---|
| `ProgramaInstalado` | `nomeExibicao`, `comparacao` (`Contem`, `IniciaCom`, `Igual`) | Nome exibido em *Configurações > Aplicativos* / *Programas e Recursos*. |
| `CodigoProdutoMsi` | `codigoProduto` (`{GUID}`) | Pacotes MSI. É a forma mais precisa. |
| `Arquivo` | `caminho` | Um executável que o programa instala. |
| `ChaveRegistro` | `caminho` (`HKLM\...` ou `HKCU\...`), `nomeValor` opcional | Quando o fabricante grava uma chave própria. |
| `Automatica` | — | Só para WinGet (`winget list`). |

Para descobrir o nome exibido e o ProductCode de um programa já instalado na máquina de teste:

```powershell
Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*,
                 HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* |
  Where-Object DisplayName -like "*nome*" |
  Select-Object DisplayName, DisplayVersion, PSChildName
```

`PSChildName` é o ProductCode quando ele tem o formato `{GUID}`.

> Se um programa não puder ser verificado (por exemplo, o WinGet não está disponível), ele **não** é
> instalado "às cegas": a etapa é marcada como falha e o motivo vai para o log.

---

## Como testar em uma máquina de teste (comece pela simulação)

Use uma **máquina virtual** (Hyper-V/VirtualBox) ou um computador de testes com o Windows recém-instalado.
Se for VM, crie um *checkpoint/snapshot* antes de começar.

1. **Prepare a configuração.** Copie `padronizacao.exemplo.json` para `padronizacao.json`. Cadastre **um**
   programa real de um departamento e mantenha os demais como pendentes.
2. **Copie** a pasta `dist\PadronizadorTI` para a máquina de teste (ou rode direto da pasta de rede).
3. **Abra o `PadronizadorTI.exe` sem administrador.** O cabeçalho mostra "Sem permissão de administrador (somente simulação)".
4. **Selecione o departamento** e leia o resumo e a lista de etapas.
5. Com **"Modo simulação"** marcado, clique em **Iniciar simulação**. Nada é alterado. O programa:
   - verifica o que já está instalado;
   - confere se os instaladores de rede existem e se o SHA-256 confere;
   - mostra os comandos que **seriam** executados.
6. Revise o resultado e o log (**Abrir pasta de logs**). Corrija caminhos, argumentos ou detecções e clique em **Recarregar configuração**.
7. Clique em **Reabrir como administrador** e aceite o UAC.
8. Desmarque **"Modo simulação"**, clique em **Executar padronização** e confirme.
9. Ao final, confira o resumo. **Rode de novo:** os programas instalados devem aparecer como **"Já existente"**.
   Isso confirma que as regras de detecção estão corretas e que não haverá reinstalação.
10. Confira as opções de energia em *Painel de Controle > Opções de Energia* (tomada = Nunca; bateria sem alteração).
11. Se o resumo pedir reinício, reinicie **manualmente** quando for conveniente.
12. Repita para cada departamento. Só depois leve o programa para as máquinas dos usuários.

---

## Logs

- Local padrão: `C:\ProgramData\PadronizadorTI\Logs`. Se não houver permissão, é usado
  `%LOCALAPPDATA%\PadronizadorTI\Logs`.
- Nome: `NOMEPC_Departamento_aaaa-MM-dd_HH-mm-ss_SIMULACAO.log` ou `..._EXECUCAO.log`.
- Conteúdo: computador, usuário, se é administrador, versão do Windows, departamento, modo, data/hora,
  local do WinGet, reinícios já pendentes, cada etapa com comando, detecção e resultado, e o resumo final.
- Logs detalhados do `msiexec` ficam na mesma pasta (`msi_*.log`).
- Com `pastaLogsRede` preenchido, uma cópia de cada log vai para a rede. Se a cópia falhar, a execução não é interrompida.

---

## Segurança: o que o programa **não** faz

- **Não** desativa o Microsoft Defender nem qualquer outra proteção (firewall, UAC, SmartScreen).
- **Não** baixa arquivos de URLs. Instaladores vêm de caminhos locais/UNC ou do WinGet, e só das fontes em
  `fontesWinGetPermitidas` (padrão: apenas a fonte oficial `winget`).
- **Não** ativa licenças, não aplica chaves de produto e não guarda senhas. Não coloque credenciais no JSON.
  Para pastas de rede protegidas, o programa pede usuário e senha na hora e não os grava.
- **Não** reinicia o computador. Só avisa.
- **Não** executa itens marcados como pendentes nem itens com configuração incompleta.
- **Não** exige administrador para simular. A execução real verifica a permissão antes de começar.

---

## Itens pendentes: o que você precisa me informar

Os itens abaixo estão cadastrados como **pendentes** no arquivo de exemplo.

### 1. Antivírus corporativo
- Produto e fabricante (nome exato);
- versão aprovada;
- instalador: `.msi` ou `.exe`, e o caminho **na rede** onde ele ficará (ex.: `\\SERVIDOR\Instaladores$\Antivirus\...`);
- argumentos de instalação silenciosa indicados pelo fabricante (e se o instalador precisa de um arquivo
  de configuração/grupo do console ao lado dele);
- como detectar: nome exibido em *Programas e Recursos* ou ProductCode `{GUID}` do MSI;
- se o instalador exige reinício e quais códigos de saída ele usa;
- (opcional) o SHA-256 do instalador.

> Tokens/chaves de registro no console do antivírus **não** devem ir para o GitHub. Se o instalador
> precisar de um, deixe-o embutido no pacote gerado pelo console, na pasta de rede, e não no JSON versionado.

### 2. Java
- Distribuição: **Oracle Java** (exige licença comercial paga) ou **OpenJDK** (ex.: Eclipse Temurin, Microsoft Build of OpenJDK);
- versão exigida pelos sistemas (ex.: 8, 11, 17, 21) e se é JRE ou JDK;
- arquitetura: 32 ou 64 bits. Alguns sistemas fiscais/bancários antigos exigem 32 bits;
- forma de instalação: ID WinGet (ex.: conferir com `winget search Temurin`) ou instalador interno com caminho e argumentos;
- se algum sistema exige uma versão **exata** (neste caso, preencha `"versao"`).

### 3. Programas de cada departamento (Legal, Fiscal, Contábil, Departamento Pessoal)
Para **cada** programa:
- nome;
- origem: ID WinGet **ou** caminho do `.exe`/`.msi` na rede;
- argumentos silenciosos (para `.exe`/`.msi`);
- regra de detecção (nome exibido, ProductCode, arquivo ou chave de registro);
- se precisa de reinício, se depende de outro programa (a ordem no arquivo é a ordem de instalação) e se é para todos os usuários.

### 4. Opcional
- caminho de rede para cópia centralizada dos logs (`pastaLogsRede`);
- se o WinRAR deve ser instalado em uma versão específica ou em português. O ID WinGet usado é `RARLab.WinRAR`,
  do fabricante. A **licença do WinRAR não é aplicada** pelo programa. Uso comercial exige licença.

---

## Solução de problemas

| Sintoma | Causa provável / solução |
|---|---|
| "WinGet não encontrado" | Atualize o *Instalador de Aplicativo* pela Microsoft Store ou pelo Windows Update. Em máquinas recém-formatadas, faça o Windows Update primeiro. |
| Instalador de rede "não encontrado" como administrador | Unidades mapeadas (ex.: `Z:`) não aparecem na sessão elevada. Use o caminho UNC `\\SERVIDOR\...` e confira a permissão no compartilhamento. |
| Código 1618 | Outra instalação MSI (geralmente o Windows Update) está em andamento. Aguarde e execute de novo; os itens já instalados serão pulados. |
| Código 1603 | Erro do MSI. Veja o `msi_*.log` na pasta de logs. |
| "Detecção não encontrou o programa depois da instalação" | Os argumentos não instalaram de verdade ou a regra de detecção está errada. Corrija e rode a simulação de novo. |
| Tempo limite esgotado | O instalador provavelmente abriu uma janela esperando resposta. Revise os argumentos silenciosos ou aumente `tempoLimiteMinutos`. |
| Plano Alto desempenho não disponível | Comum em notebooks com *Modern Standby*. Os tempos de tomada ainda são aplicados. |
