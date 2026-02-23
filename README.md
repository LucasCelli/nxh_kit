# NHX_Kit

Toolkit de Comandos de Sistema para Windows (WPF/.NET 8), com interface simples para tarefas de rede, limpeza e manutenção.

## Funcionalidades

- Comandos de rede:
  - Flush DNS (`ipconfig /flushdns`)
  - Release IP (`ipconfig /release`)
  - Renew IP (`ipconfig /renew`)
  - Reset Winsock (`netsh winsock reset`)
  - Reset de protocolo IP (`netsh int ip reset`)
  - Reset de proxy WinHTTP (`netsh winhttp reset proxy`)
  - Diagnóstico completo (`ipconfig /all`)
  - Cadeia de rede (execução sequencial dos principais comandos)
- Limpeza:
  - Arquivos temporários
  - Lixeira
  - Cache de ícones
  - Cache de miniaturas
  - Limpeza Temp + Prefetch
- Sistema:
  - Reset de Windows Update
  - SFC (`sfc /scannow`)
  - CHKDSK (somente leitura)
  - DISM CheckHealth
- Desempenho:
  - Coleta de memória .NET (`GC.Collect`)

## Requisitos

- Windows 10/11
- .NET 8 Desktop Runtime (para versão framework-dependent)
- Execução como Administrador (o app já solicita elevação no manifest)

## Rodar em desenvolvimento

```powershell
dotnet restore
dotnet run
```

## Build local

```powershell
dotnet build -c Release
```

## Publicar versão mais leve (dependente do .NET)

Gera uma publicação framework-dependent (sem embutir runtime):

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false
```

Saída padrão:

- `bin/Release/net8.0-windows/win-x64/publish/`

## Estrutura

- `MainWindow.xaml`: UI principal
- `MainWindow.xaml.cs`: lógica dos comandos
- `assets/nhx-kit.ico`: ícone da aplicação
- `MaintenanceTool.csproj`: configuração do projeto/build

## Avisos

- Alguns comandos podem requerer reinício do sistema para efeito completo (ex.: Winsock/IP reset).
- Use em ambiente confiável e, de preferência, com backup/ponto de restauração.
