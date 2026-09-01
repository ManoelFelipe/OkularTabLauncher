# OkularSessionLauncher

OkularSessionLauncher is an optional companion application that automatically saves and restores the PDF tabs in the primary Okular window on Windows.

## How it works

The background monitor observes the largest visible Okular window through Windows UI Automation. When the tab list changes, it visits each tab briefly, reads the full PDF path from the window title, restores the originally selected tab, and writes an atomic session snapshot.

After Okular closes, the last valid snapshot is preserved. When Okular starts again, the monitor combines the saved session with a PDF that may have been opened explicitly. If restoration is needed, the initial window closes and Okular starts once with every path in the combined session. A brief window reopen is therefore expected.

The monitor never reads PDF contents. It stores only local file paths in the current user's profile.

## Requirements

- Windows 11 x64;
- Okular configured to open new files in tabs;
- Okular configured to display the full file path in the title bar;
- .NET 10 Desktop Runtime x64;
- `OkularSessionLauncher.exe` and a startup shortcut created by the installation script.

The Okular executable is resolved in this order:

1. `OKULAR_SESSION_LAUNCHER_OKULAR_EXE`;
2. `OKULAR_TAB_LAUNCHER_OKULAR_EXE`;
3. the Scoop roots in `SCOOP`, the default user Scoop directory, and `SCOOP_GLOBAL`;
4. common Program Files locations;
5. directories in `PATH`.

## Install the monitor

Build the repository first, then run:

```powershell
pwsh -NoProfile -File .\scripts\Install-SessionMonitor.ps1
```

The script installs only for the current user, preserves existing session data, backs up a previous executable, creates `Okular Session Monitor.lnk` in the Startup folder, and starts the monitor without a console window. Administrator privileges are not required.

## Command-line modes

```text
OkularSessionLauncher.exe --monitor   Monitor and automatically save/restore
OkularSessionLauncher.exe --save      Save the current session once
OkularSessionLauncher.exe --restore   Start Okular with the saved session
OkularSessionLauncher.exe --clear     Delete the saved session
```

With no argument, the application behaves as `--restore`. Only one monitor instance runs per Windows user session.

## Local data

```text
%LOCALAPPDATA%\OkularSessionLauncher\last-session.txt
%LOCALAPPDATA%\OkularSessionLauncher\session-log.txt
```

These files are never part of repository artifacts. `last-session.txt` contains private local paths and should not be attached to a public issue without review.

## Safety behavior and limitations

- An empty or failed capture never overwrites the last valid session.
- UI Automation calls run on background STA threads with timeouts, so a stale Okular process cannot permanently block the monitor.
- Automatic restoration does not close a newly detected window that already contains multiple tabs, avoiding Okular's multi-tab close confirmation and unexpected data loss.
- The largest visible Okular window is treated as primary; multiple independent Okular windows are not preserved as separate sessions.
- The selected page, zoom, annotations, unsaved changes, and per-document view state are managed by Okular and are not serialized by this utility.
- Unsigned builds can still be blocked by Smart App Control.

## Exit codes

`0` means success, `1` an unexpected error, `2` an invalid mode, `3` that Okular was already running during manual restore, `4` that Okular could not be found, and `5` that a session could not be captured.

---

# OkularSessionLauncher — Português do Brasil

O OkularSessionLauncher é um aplicativo complementar opcional que salva e restaura automaticamente as abas de PDF da janela principal do Okular no Windows.

## Funcionamento

O monitor em segundo plano observa a maior janela visível do Okular pela Automação de Interface do Windows. Quando a lista de abas muda, ele visita brevemente cada aba, lê o caminho completo do PDF no título da janela, retorna à aba originalmente selecionada e grava uma sessão de forma atômica.

Depois que o Okular fecha, a última sessão válida é preservada. Quando ele abre novamente, o monitor combina a sessão salva com um PDF que tenha sido aberto diretamente. Se a restauração for necessária, a janela inicial fecha e o Okular é iniciado uma única vez com todos os caminhos. Portanto, uma breve reabertura da janela é esperada.

O monitor não lê o conteúdo dos PDFs. Ele armazena somente caminhos de arquivos locais no perfil do usuário atual.

## Requisitos

- Windows 11 x64;
- Okular configurado para abrir novos arquivos em abas;
- Okular configurado para mostrar o caminho completo na barra de título;
- .NET 10 Desktop Runtime x64;
- `OkularSessionLauncher.exe` e o atalho de inicialização criado pelo instalador.

A localização do Okular segue esta ordem:

1. `OKULAR_SESSION_LAUNCHER_OKULAR_EXE`;
2. `OKULAR_TAB_LAUNCHER_OKULAR_EXE`;
3. raízes do Scoop em `SCOOP`, diretório padrão do usuário e `SCOOP_GLOBAL`;
4. locais comuns em Program Files;
5. diretórios presentes em `PATH`.

## Instalação do monitor

Compile o repositório e execute:

```powershell
pwsh -NoProfile -File .\scripts\Install-SessionMonitor.ps1
```

O script instala o aplicativo somente para o usuário atual, preserva os dados existentes, cria um backup do executável anterior, adiciona `Okular Session Monitor.lnk` à pasta Inicializar e inicia o monitor sem console. Não são necessários privilégios de administrador.

## Modos de linha de comando

```text
OkularSessionLauncher.exe --monitor   Monitora, salva e restaura automaticamente
OkularSessionLauncher.exe --save      Salva a sessão atual uma vez
OkularSessionLauncher.exe --restore   Inicia o Okular com a sessão salva
OkularSessionLauncher.exe --clear     Apaga a sessão salva
```

Sem argumento, o comportamento equivale a `--restore`. Somente um monitor é executado por sessão do usuário no Windows.

## Dados locais e privacidade

```text
%LOCALAPPDATA%\OkularSessionLauncher\last-session.txt
%LOCALAPPDATA%\OkularSessionLauncher\session-log.txt
```

Esses arquivos nunca fazem parte dos artefatos do repositório. `last-session.txt` contém caminhos locais privados e deve ser revisado antes de ser anexado a um relato público.

## Segurança e limitações

- Uma captura vazia ou malsucedida nunca substitui a última sessão válida.
- As consultas de Automação de Interface usam threads STA em segundo plano com timeout, impedindo que um processo antigo do Okular bloqueie permanentemente o monitor.
- A restauração automática não fecha uma janela recém-detectada que já contenha várias abas; isso evita a confirmação de fechamento do Okular e perda inesperada de dados.
- A maior janela visível do Okular é considerada principal; várias janelas independentes não são preservadas como sessões separadas.
- Página selecionada, zoom, anotações, alterações não salvas e estado de visualização pertencem ao Okular e não são serializados pelo monitor.
- Builds sem assinatura ainda podem ser bloqueados pelo Smart App Control.

## Códigos de saída

`0` indica sucesso, `1` erro inesperado, `2` modo inválido, `3` Okular já aberto durante uma restauração manual, `4` Okular não encontrado e `5` impossibilidade de capturar uma sessão.
