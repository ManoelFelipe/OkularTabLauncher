<p align="center">
  <img src="../assets/OkularTabLauncher.png" width="128" height="128" alt="Ícone do OkularTabLauncher">
</p>

# OkularTabLauncher

Abre um PDF como nova aba na janela existente do Okular no Windows.

[Read in English](../README.md)

> [!IMPORTANT]
> Os artefatos atuais do workflow são builds de desenvolvimento sem assinatura. Publicar o fonte e compilá-lo no GitHub não faz, por si só, o Smart App Control confiar no executável. A assinatura de código será uma etapa protegida e separada, descrita em [SIGNING_POLICY.md](../SIGNING_POLICY.md).

## Por que este projeto existe

Em algumas instalações do Windows, `okular.exe --unique arquivo.pdf` ainda abre uma segunda janela em vez de adicionar uma aba à janela existente. O OkularTabLauncher mantém o comportamento que funciona por meio de automação da interface do Okular:

1. restaura e ativa a maior janela visível do Okular;
2. envia `Ctrl+O`;
3. identifica o novo diálogo Abrir;
4. escreve diretamente o caminho Unicode completo no campo de nome do arquivo;
5. aciona o botão Abrir.

Se o Okular estiver fechado, o launcher o inicia normalmente com o PDF.

## Propriedades de segurança

- Aceita exatamente um caminho absoluto com extensão `.pdf`.
- Confirma que o arquivo existe.
- Não chama `cmd.exe`, PowerShell ou um shell para abrir o documento.
- Não usa o clipboard.
- Registra as janelas existentes antes do `Ctrl+O` e considera somente um diálogo criado depois.
- Serializa execuções simultâneas com `Local\OkularTabLauncherV2`.
- Não abre console durante o uso normal.

O launcher não interpreta o conteúdo do PDF. A segurança do documento continua sendo responsabilidade do Okular e de seu backend de PDF.

## Requisitos

- Windows 11 x64;
- Okular para Windows;
- .NET Framework 4.8 para executar;
- PowerShell 7 e o SDK .NET fixado somente para compilar.

Quando o Okular está fechado, o executável é procurado nesta ordem:

1. variável de ambiente `OKULAR_TAB_LAUNCHER_OKULAR_EXE`;
2. `D:\Scoop\apps\okular\current\bin\okular.exe`;
3. diretório Scoop padrão do usuário;
4. caminhos comuns em `%ProgramFiles%\Okular`.

## Compilar

```powershell
git clone <url-do-repositorio>
Set-Location OkularTabLauncher
pwsh -NoProfile -File .\scripts\build.ps1
```

O script restaura somente dependências travadas, faz duas compilações com estados intermediários limpos e separados, compara os executáveis por SHA-256 e gera:

```text
artifacts/OkularTabLauncher.exe
artifacts/OkularTabLauncher.exe.sha256
```

Para conferir:

```powershell
Get-FileHash -Algorithm SHA256 .\artifacts\OkularTabLauncher.exe
Get-Content .\artifacts\OkularTabLauncher.exe.sha256
```

O GitHub Actions usa o mesmo script. O SDK está fixado em `global.json`, as referências do .NET Framework estão travadas em `src/packages.lock.json` e as Actions estão fixadas por hash completo de commit.

## Testar sem alterar associações

Não mexa no launcher instalado. Execute diretamente o build de desenvolvimento:

```powershell
& .\artifacts\OkularTabLauncher.exe 'C:\caminho\completo\teste.pdf'
$LASTEXITCODE
```

Antes de uma release, testar:

- Okular fechado;
- Okular aberto com uma ou várias abas;
- Okular minimizado;
- caminhos com espaços, acentos, OneDrive e Unicode;
- dois PDFs abertos quase simultaneamente;
- diálogo Abrir de outro programa já visível;
- arquivo inexistente, caminho relativo e arquivo que não seja PDF.

Logs:

```text
%LOCALAPPDATA%\OkularTabLauncher\last-run.txt
%LOCALAPPDATA%\OkularTabLauncher\last-error.txt
```

Códigos de saída: `0` sucesso, `1` falha inesperada, `2` entrada inválida, `3` timeout do mutex, `4` Okular não encontrado e `5` falha da automação.

## Instalar

Não substitua um launcher funcional apenas para testar um artefato sem assinatura. Depois de validar uma release assinada:

1. faça backup do executável em `%LOCALAPPDATA%\OkularTabLauncher`;
2. confira o SHA-256 e a assinatura Authenticode da release;
3. copie o `OkularTabLauncher.exe` assinado para essa pasta;
4. execute-o diretamente com um PDF de teste;
5. somente então selecione-o em **Configurações > Aplicativos > Aplicativos padrão**.

O projeto não escreve diretamente o valor protegido `UserChoice` do Registro.

## Restaurar a associação anterior

Abra **Configurações > Aplicativos > Aplicativos padrão**, procure por `.pdf` e escolha o aplicativo utilizado anteriormente. Apenas remover o executável não restaura a associação de forma confiável.

## Limitações conhecidas

- A automação depende das regras de foco do Windows e do diálogo exposto pela versão instalada do Okular/Qt.
- O título do diálogo é reconhecido em português e inglês; classe e identificadores nativos fornecem sinais independentes do idioma quando disponíveis.
- A janela principal é escolhida pela maior área visível.
- Aplicativos em níveis de integridade diferentes podem ser isolados pelo UIPI do Windows.
- Builds sem assinatura ainda podem ser bloqueados pelo Smart App Control.

Algumas instalações do Okular para Windows podem ativar inesperadamente abas ou itens de menu durante o movimento do ponteiro. O mesmo comportamento foi reproduzido pelo fluxo manual **Ficheiro > Abrir** do Okular, sem nenhum processo do launcher envolvido. Consulte [Problemas conhecidos](KNOWN_ISSUES.md) para ver as evidências dos testes e as orientações para relatar o problema.

## Licença e marcas

O código e o ícone original do OkularTabLauncher usam a [licença MIT](../LICENSE).

Okular é um projeto KDE. OkularTabLauncher é um utilitário independente de interoperabilidade, sem afiliação, endosso ou distribuição pelo KDE ou pelo projeto Okular. Nenhum executável ou elemento gráfico do Okular está incluído.
