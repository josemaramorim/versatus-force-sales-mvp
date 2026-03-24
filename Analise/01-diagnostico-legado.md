# Diagnostico tecnico do legado

## Resumo executivo

O sistema atual nao deve ser modernizado com uma migracao direta de projeto para projeto.

O motivo principal e arquitetural:

- o servidor atual expõe objetos remotos via .NET Remoting;
- as regras de negocio herdam de `MarshalByRefObject`;
- o cliente consome factories e objetos remotos de forma muito acoplada e "chatty";
- a camada de interfaces mistura contratos de negocio com dependencias de desktop;
- existe um framework proprio de persistencia (`Gentle`) e uso de SQL direto.

Conclusao: a estrategia correta e um estrangulamento por dominio, nao um port automatico para .NET Core.

## Evidencias levantadas no codigo

### 1. O transporte atual e .NET Remoting

No servidor, o canal de comunicacao e criado com `TcpChannel` ou `IpcChannel` e registrado por `ChannelServices`.

Arquivos relevantes:

- `projeto_tag_1906/servidor/framework/servidor.framework/CanalServidor.cs`
- `projeto_tag_1906/cliente/cliente.framework/CanalCliente.cs`

Isso caracteriza o uso de .NET Remoting, tecnologia obsoleta e sem suporte em .NET Core / .NET 5+.

### 2. As regras de negocio vivem como objetos remotos

A base de negocio do servidor herda de `MarshalByRefObject` e usa `InitializeLifetimeService`, `ILease` e sponsor remoto.

Arquivos relevantes:

- `projeto_tag_1906/servidor/framework/servidor.framework/ObjetoNegocio.cs`
- `projeto_tag_1906/cliente/cliente.framework/SponsorManager.cs`

Esse modelo nao e portavel para .NET 8.

### 3. O cliente atual depende de proxies remotos

O cliente instancia servicos remotos com `Activator.GetObject`, inclusive ambiente e factories.

Arquivos relevantes:

- `projeto_tag_1906/cliente/cliente.aplicativo/MultiTierFactory.cs`

Isso significa que a fronteira atual da aplicacao nao e HTTP nem API orientada a caso de uso; ela e uma malha de objetos remotos.

### 4. A camada de interfaces nao esta limpa para reaproveitamento

O projeto de interfaces referencia componentes desktop e bibliotecas antigas, inclusive `System.Windows.Forms`, `System.Drawing` e `DevExpress`.

Arquivo relevante:

- `projeto_tag_1906/servidor/servidor.interface/Servidor.Interface.csproj`

Isso impede tratar a camada de contratos atual como um pacote compartilhavel para .NET 8 sem refatoracao previa.

### 5. Existe uma factory remota generica muito ampla

`IObjetoNegocioFactory` expõe consulta generica, criacao de objetos, transacao, SQL direto e atualizacao reflexiva de propriedades.

Arquivo relevante:

- `projeto_tag_1906/servidor/servidor.interface/framework/IObjetoNegocioFactory.cs`

Esse estilo funciona em remoting, mas e um desenho ruim para API moderna. API HTTP deve expor casos de uso e recursos de negocio, nao uma super factory generica.

### 6. O host do servidor ainda e Windows Service .NET Framework

O projeto `Servidor.Service` e um Windows Service em .NET Framework 4.7 e referencia `System.Runtime.Remoting`.

Arquivo relevante:

- `projeto_tag_1906/servidor/Servidor.Service/Servidor.Service.csproj`

## Tamanho relativo dos dominios de negocio

Contagem aproximada de arquivos `.cs` por modulo dentro de `servidor/objeto de negocio`:

| Modulo | Arquivos C# |
| --- | ---: |
| acesso.global | 419 |
| SPED.Fiscal | 258 |
| gestao.financeira | 184 |
| faturamento | 166 |
| gestao.tributo | 157 |
| NFe | 142 |
| Gestao.Transporte | 140 |
| gestao.material | 127 |
| MDFe | 108 |
| SPED.PisCofins | 99 |
| gestao.compra | 92 |
| Base.Distribuicao | 87 |
| Gestao.RH | 86 |
| NFSe | 67 |

Observacao: tamanho nao significa prioridade isoladamente. Serve para estimar esforco e dependencias.

## Principais bloqueadores da modernizacao

1. Acoplamento entre UI desktop, interfaces e negocio.
2. Dependencia estrutural de .NET Remoting.
3. Contratos orientados a objeto remoto em vez de contratos de API.
4. Persistencia legada via Gentle e SQL direto espalhado.
5. Dominio grande, com muitos modulos fiscais e financeiros sensiveis.
6. Possivel baixa testabilidade de regras de negocio por dependencia de ambiente remoto.

## Conclusao tecnica

### O que nao recomendo

- tentar portar todo o `servidor/objeto de negocio` para .NET 8 de uma vez;
- tentar reproduzir Remoting em .NET Core;
- criar uma API HTTP que apenas replique `IObjetoNegocioFactory` como endpoint generico;
- quebrar tudo em microservicos logo no inicio.

### O que recomendo

- criar uma nova solucao em .NET 8 com ASP.NET Core;
- usar um monolito modular como destino inicial;
- migrar por dominio e por caso de uso, mantendo coexistencia temporaria com o legado;
- encapsular o legado atras de uma borda anti-corruption enquanto os modulos novos nascem nativamente em .NET 8.