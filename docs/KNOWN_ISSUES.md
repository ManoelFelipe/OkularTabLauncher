# Known issues

## Unexpected tab or menu activation in Okular on Windows

On some Windows systems, moving the pointer inside Okular can unexpectedly activate tabs or menu entries without an intentional click. The behavior may become visible after opening another document.

This is an external Okular/Qt for Windows behavior, not an OkularTabLauncher input action. During validation it was reproduced:

- after opening a PDF manually through **File > Open**, without running OkularTabLauncher;
- with different physical mice;
- with a clean Okular profile;
- in Okular 25.12 with Qt 6.10.2;
- in Okular 26.04 and a newer development build with Qt 6.11.1.

OkularTabLauncher only automates the normal Open dialog when an Okular window already exists. It does not remain running after the document is opened and cannot control subsequent pointer processing inside Okular.

No launcher-side workaround is included. Experiments that changed Qt pointer options or injected synthetic input-release events did not correct the behavior and were deliberately excluded from the public source because they add complexity and can affect unrelated user input.

If this occurs:

1. update Windows and Okular when maintenance releases become available;
2. close and reopen Okular to clear the transient interaction state;
3. confirm the behavior using **File > Open** before reporting it as a launcher problem;
4. report a reproducible case to the Okular/KDE issue tracker with the Windows build, Okular version, Qt version, and a short screen recording.

---

# Problemas conhecidos

## Ativação inesperada de abas ou menus no Okular para Windows

Em alguns sistemas Windows, mover o ponteiro dentro do Okular pode ativar abas ou itens de menu sem um clique intencional. O comportamento pode aparecer depois da abertura de outro documento.

Esse é um comportamento externo do Okular/Qt para Windows, não uma ação de entrada do OkularTabLauncher. Durante a validação, ele foi reproduzido:

- depois de abrir um PDF manualmente por **Ficheiro > Abrir**, sem executar o OkularTabLauncher;
- com mouses físicos diferentes;
- com um perfil limpo do Okular;
- no Okular 25.12 com Qt 6.10.2;
- no Okular 26.04 e em uma compilação de desenvolvimento mais recente com Qt 6.11.1.

O OkularTabLauncher apenas automatiza o diálogo Abrir normal quando já existe uma janela do Okular. Ele não permanece em execução depois que o documento é aberto e não controla o processamento posterior do ponteiro dentro do Okular.

Nenhum contorno foi incorporado ao launcher. Experimentos com opções de ponteiro do Qt e eventos sintéticos de liberação dos botões não corrigiram o comportamento e foram excluídos deliberadamente do código público, pois acrescentariam complexidade e poderiam interferir em outras entradas do usuário.

Caso isso aconteça:

1. atualize o Windows e o Okular quando novas versões de manutenção estiverem disponíveis;
2. feche e reabra o Okular para limpar o estado transitório de interação;
3. reproduza o comportamento por **Ficheiro > Abrir** antes de atribuí-lo ao launcher;
4. envie um relato reproduzível ao rastreador do Okular/KDE, incluindo a compilação do Windows, as versões do Okular e do Qt e uma gravação curta da tela.
