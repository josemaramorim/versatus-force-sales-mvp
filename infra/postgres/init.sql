# Script de inicialização do banco de dados

-- Criar schema de infraestrutura
CREATE SCHEMA IF NOT EXISTS infra;

-- Tabela de assinaturas/tenants
CREATE TABLE IF NOT EXISTS infra.assinaturas (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL UNIQUE,
    nome_empresa VARCHAR(200) NOT NULL,
    max_usuarios_simultaneos INT NOT NULL DEFAULT 2,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    criado_em TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    atualizado_em TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Seed de tenant de desenvolvimento/demo
INSERT INTO infra.assinaturas (tenant_id, nome_empresa, max_usuarios_simultaneos)
VALUES
    ('00000000-0000-0000-0000-000000000001', 'Demo Empresa Ltda', 4),
    ('00000000-0000-0000-0000-000000000002', 'Empresa Teste SA', 2)
ON CONFLICT (tenant_id) DO NOTHING;
