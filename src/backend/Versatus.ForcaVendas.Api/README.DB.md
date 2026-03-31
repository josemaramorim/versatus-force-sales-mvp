Banco PostgreSQL - instruções rápidas

1) Nome recomendado do banco: `versatus_forcavendas`

2) Configuração recomendada (appsettings):

Edite `src/backend/Versatus.ForcaVendas.Api/appsettings.json` (ou `appsettings.Development.json`) e ajuste a seção `ConnectionStrings` para apontar para o seu banco:

```json
{
	"ConnectionStrings": {
		"DefaultConnection": "Host=209.50.229.205;Port=5432;Database=versatus_forcavendas;Username=<seu_usuario>;Password=<sua_senha>"
	}
}
```

3) Rodando localmente:

```powershell
# a partir da raiz do repo
dotnet run --project src/backend/Versatus.ForcaVendas.Api
```

4) Segurança:
- Não comite arquivos com credenciais.
- Em produção prefira mecanismos de secrets (Key Vault, environment variables configuradas no ambiente de execução, etc.).

5) Observação:
- O backend usa `appsettings.json` por padrão para `ConnectionStrings:DefaultConnection`. Você pode configurar variáveis de ambiente no host/infra se necessário, mas a referência local é feita via `appsettings.Development.json`.
